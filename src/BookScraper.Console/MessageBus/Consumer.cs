using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace BookScraper.Console.MessageBus;

internal class Consumer : IConsumer
{
    private readonly string _baseDirectoryPath;
    private readonly string _baseUrl;
    private readonly HttpClient _client = new();
    private readonly IMessageBus<string> _messageBus;
    private readonly ConcurrentDictionary<string, bool> _processedLinks = new();

    public Consumer(IMessageBus<string> messageBus, string baseDirectoryPath, string baseUrl)
    {
        _messageBus = messageBus;
        _baseDirectoryPath = baseDirectoryPath;
        _baseUrl = baseUrl;
    }

    public async Task Process()
    {
        string url;
        while (true)
        {
            if (_messageBus.Fetch(out url))
            {
                try
                {
                    var uri = UrlUtils.GetUri(url);

                    var savePath = UrlUtils.GetPath(_baseDirectoryPath, uri.AbsolutePath);

                    var isImageContent = Regex.IsMatch(url, @"\.(gif|jpe?g|tiff?|png|webp|bmp)$", RegexOptions.IgnoreCase);

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
                        if (_processedLinks.TryAdd(link, true))
                        {
                            _messageBus.Add(UrlUtils.CleanUrl(link));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error processing {url}: {ex.Message}");
                }

                // await _fulfillmentService.Fulfill(order);
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
            System.Console.WriteLine("Could not fetch link " + uri + ". Error message: " + ex.Message);
            return default;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
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
            System.Console.WriteLine("Could not fetch link " + uri + ". Error message: " + ex.Message);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
        await File.WriteAllBytesAsync(savePath, imageBytes);
    }

    private List<string> ExtractLinks(string html, Uri currentBaseUri)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        List<string> links = [];

        var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img");

        if (imageNodes != null)
        {
            foreach (var img in imageNodes)
            {
                string imageUrl = img.GetAttributeValue("src", string.Empty);
                string resolvedUrl = GetResolvedUrl(currentBaseUri, imageUrl);
                links.Add(resolvedUrl);
            }
        }

        var linkRelNodes = htmlDoc.DocumentNode.SelectNodes("//link");

        if (linkRelNodes != null)
        {
            foreach (var styleSheet in linkRelNodes)
            {
                string linkRelUrl = styleSheet.GetAttributeValue("href", string.Empty);
                string resolvedUrl = GetResolvedUrl(currentBaseUri, linkRelUrl);
                links.Add(resolvedUrl);
            }
        }


        var linkNodes = htmlDoc.DocumentNode.SelectNodes("//a");

        if (linkNodes != null)
        {
            foreach (var link in linkNodes)
            {
                string hrefValue = link.GetAttributeValue("href", string.Empty);
                string resolvedUrl = GetResolvedUrl(currentBaseUri, hrefValue);
                links.Add(resolvedUrl);
            }
        }

        return links;

        static string GetResolvedUrl(Uri currentBaseUri, string imageUrl)
        {
            return new Uri(currentBaseUri, imageUrl).ToString();
        }
    }
}

internal interface IConsumer
{
    Task Process();
}