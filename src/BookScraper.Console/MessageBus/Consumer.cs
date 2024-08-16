using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace BookScraper.Console.MessageBus;

internal interface IConsumer
{
    Task Process();
}

internal class Consumer : IConsumer
{
    private static readonly object _fileLock = new();
    private static readonly ConcurrentDictionary<string, bool> _processedLinks = new();

    private readonly string _baseDirectoryPath;
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationToken _token;
    private readonly HttpClient _client = new();
    private readonly IMessageBus<string> _messageBus;
    private readonly string _errorLogPath;

    public Consumer(IMessageBus<string> messageBus, string baseDirectoryPath, SemaphoreSlim semaphore, CancellationToken token)
    {
        _messageBus = messageBus;
        _baseDirectoryPath = baseDirectoryPath;
        _semaphore = semaphore;
        _token = token;
        _errorLogPath = Path.Combine(baseDirectoryPath, "errorLog.txt");
    }

    public async Task Process()
    {
        while (true)
        {
            if (_token.IsCancellationRequested)
            {
                return;
            }

            if (_messageBus.Fetch(out string url))
            {
                await _semaphore.WaitAsync();
                try
                {

                    var uri = UrlUtils.GetUri(url);

                    var savePath = UrlUtils.GetPath(_baseDirectoryPath, uri.AbsolutePath);

                    var isImageContent = Regex.IsMatch(url, @"\.(gif|jpe?g|tiff?|png|webp|bmp|ico)$", RegexOptions.IgnoreCase);

                    if (isImageContent)
                    {
                        await SaveBinaryFileToDisk(uri, savePath);
                        continue;
                    }

                    var html = await SaveHtmlFileToDiskAndReturnHtml(uri, savePath);

                    if (html is null)
                    {
                        continue;
                    }

                    var newLinks = ExtractLinks(html, uri);

                    foreach (var link in newLinks)
                    {
                        var cleanedLink = UrlUtils.CleanUrl(link);

                        if (_processedLinks.TryAdd(cleanedLink, true))
                        {
                            _messageBus.Add(cleanedLink);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error processing {url}: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }
    }

    private static List<string> ExtractLinks(string html, Uri currentBaseUri)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        List<string> links = [];

        var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img");

        if (imageNodes != null)
        {
            foreach (var img in imageNodes)
            {
                string imageSrc = img.GetAttributeValue("src", string.Empty);

                if (UrlIsAbsolute(imageSrc))
                {
                    continue;
                }

                string resolvedUrl = GetResolvedUrl(currentBaseUri, imageSrc);
                links.Add(resolvedUrl);
            }
        }

        var linkRelNodes = htmlDoc.DocumentNode.SelectNodes("//link");

        if (linkRelNodes != null)
        {
            foreach (var styleSheet in linkRelNodes)
            {
                string linkRelHref = styleSheet.GetAttributeValue("href", string.Empty);

                if (UrlIsAbsolute(linkRelHref))
                {
                    continue;
                }

                string resolvedUrl = GetResolvedUrl(currentBaseUri, linkRelHref);
                links.Add(resolvedUrl);
            }
        }

        var scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script");

        if (scriptNodes != null)
        {
            foreach (var scriptNode in scriptNodes)
            {
                string scriptNodeSrc = scriptNode.GetAttributeValue("src", string.Empty);

                if (UrlIsAbsolute(scriptNodeSrc))
                {
                    continue;
                }

                string resolvedUrl = GetResolvedUrl(currentBaseUri, scriptNodeSrc);
                links.Add(resolvedUrl);
            }
        }

        var linkNodes = htmlDoc.DocumentNode.SelectNodes("//a");

        if (linkNodes != null)
        {
            foreach (var link in linkNodes)
            {
                string linkNodeHref = link.GetAttributeValue("href", string.Empty);

                if (UrlIsAbsolute(linkNodeHref))
                {
                    continue;
                }

                string resolvedUrl = GetResolvedUrl(currentBaseUri, linkNodeHref);
                links.Add(resolvedUrl);
            }
        }

        return links;

        static string GetResolvedUrl(Uri currentBaseUri, string imageUrl)
        {
            return new Uri(currentBaseUri, imageUrl).ToString();
        }
    }

    private static bool UrlIsAbsolute(string imageUrl)
    {
        return Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute);
    }

    private void WriteToErrorLog(Uri uri, string message)
    {
        var logEntry = "Could not fetch link " + uri + ". Error message: " + message;
        System.Console.WriteLine(message);

        lock (_fileLock)
        {
            if (!File.Exists(_errorLogPath))
            {
                using StreamWriter sw = File.CreateText(_errorLogPath);
                sw.WriteLine(logEntry);
            }
            else
            {
                using StreamWriter sw = File.AppendText(_errorLogPath);
                sw.WriteLine(logEntry);
            }
        }
    }

    private async Task<string?> SaveHtmlFileToDiskAndReturnHtml(Uri uri, string savePath)
    {
        string textContent;

        try
        {
            textContent = await _client.GetStringAsync(uri);
        }
        catch (Exception ex)
        {
            WriteToErrorLog(uri, ex.Message);
            return default;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        await File.WriteAllTextAsync(savePath, textContent);
        return textContent;
    }

    private async Task SaveBinaryFileToDisk(Uri uri, string savePath)
    {
        byte[] imageBytes;

        try
        {
            imageBytes = await _client.GetByteArrayAsync(uri);
        }
        catch (Exception ex)
        {
            WriteToErrorLog(uri, ex.Message);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        await File.WriteAllBytesAsync(savePath, imageBytes);
    }
}
