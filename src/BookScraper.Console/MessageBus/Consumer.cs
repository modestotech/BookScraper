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
    private static readonly SemaphoreSlim _errorFileSemaphore = new SemaphoreSlim(1, 1);
    private static readonly SemaphoreSlim _pageFilesSemaphore = new SemaphoreSlim(1, 1);
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

    private static IEnumerable<string> ExtractLinks(string html, Uri currentBaseUri)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        var links = GetLinks("//img", "src", currentBaseUri)
            .Concat(GetLinks("//link", "href", currentBaseUri))
            .Concat(GetLinks("//script", "src", currentBaseUri))
            .Concat(GetLinks("//a", "href", currentBaseUri));

        return links;

        static string GetResolvedUrl(Uri currentBaseUri, string imageUrl)
        {
            return new Uri(currentBaseUri, imageUrl).ToString();
        }

        List<string> GetLinks(string nodeType, string attributeValue, Uri currentBaseUri)
        {
            List<string> links = [];

            var nodes = htmlDoc.DocumentNode.SelectNodes(nodeType);

            if (nodes != null)
            {
                foreach (var img in nodes)
                {
                    string resourceUrl = img.GetAttributeValue(attributeValue, string.Empty);

                    if (UrlIsAbsolute(resourceUrl))
                    {
                        continue;
                    }

                    string resolvedUrl = GetResolvedUrl(currentBaseUri, resourceUrl);

                    links.Add(resolvedUrl);
                }
            }

            return links;
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

        _errorFileSemaphore.Wait();

        try
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
        finally
        {
            _errorFileSemaphore.Release();
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

        await _pageFilesSemaphore.WaitAsync();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            await File.WriteAllTextAsync(savePath, textContent);
        }
        finally
        {
            _pageFilesSemaphore.Release(); // Release the semaphore
        }

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

        await _pageFilesSemaphore.WaitAsync();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            await File.WriteAllBytesAsync(savePath, imageBytes);
        }
        finally
        {
            _pageFilesSemaphore.Release(); // Release the semaphore
        }
    }
}
