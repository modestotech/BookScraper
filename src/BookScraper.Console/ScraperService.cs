using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BookScraper.Console;
using HtmlAgilityPack;

internal class ScraperService
{
    private readonly string _baseUrl;
    private readonly string _baseDirectoryPath;
    private readonly HttpClient _client = new();
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly ConcurrentDictionary<string, bool> _processedLinks = new();
    // Why a dictionary and a bool here? Because there's no ConcurrentSet in C#, so the bool is just a dummy to enable using ConcurrentDictionary.
    private readonly SemaphoreSlim _semaphore;

    internal ScraperService(string baseUrl, string baseDirectoryName = "ScrapedSite", int threadCount = 5)
    {
        _baseUrl = baseUrl;
        _baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, baseDirectoryName);
        _semaphore = new SemaphoreSlim(threadCount);
    }

    internal async Task StartAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            Console.WriteLine("Exiting, base URL was not provided.");
            Environment.Exit(-1);
        }

        if (!UrlUtils.UrlIsValid(_baseUrl))
        {
            Console.WriteLine("Exiting, a valid base URL is not provided");
            Environment.Exit(-1);
        }

        if (Directory.Exists(_baseDirectoryPath))
        {
            try
            {
                Directory.Delete(_baseDirectoryPath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exiting, could not delete base directory, might be in use. Error message: " + ex.Message);
            }
        }

        _queue.Enqueue(UrlUtils.CleanUrl(_baseUrl));
        _processedLinks.TryAdd(_baseUrl, true);

        var tasks = new List<Task>();

        while (_queue.TryDequeue(out var url))
        {
            var currentUrl = url;
            tasks.Add(ProcessUrlAsync(currentUrl));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessUrlAsync(string url)
    {
        await _semaphore.WaitAsync();

        try
        {
            var uri = UrlUtils.GetUri(url);

            var savePath = UrlUtils.GetPath(_baseDirectoryPath, uri.AbsolutePath);

            var isImageContent = Regex.IsMatch(url, @"\.(gif|jpe?g|tiff?|png|webp|bmp)$", RegexOptions.IgnoreCase);

            if (isImageContent)
            {
                await SaveBinaryFileToDisk(uri, savePath);
                return;
            }

            var html = await SaveHtmlFileToDiskAndReturnHtml(uri, savePath);

            var newLinks = ExtractLinks(html);

            foreach (var link in newLinks)
            {
                if (_processedLinks.TryAdd(link, true))
                {
                    _queue.Enqueue(UrlUtils.CleanUrl(link));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {url}: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> SaveHtmlFileToDiskAndReturnHtml(Uri uri, string savePath)
    {
        var textContent = await _client.GetStringAsync(uri);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        await File.WriteAllTextAsync(savePath, textContent);
        return textContent;
    }

    private async Task SaveBinaryFileToDisk(Uri uri, string savePath)
    {
        byte[] imageBytes = await _client.GetByteArrayAsync(uri);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        await File.WriteAllBytesAsync(savePath, imageBytes);
    }

    private List<string> ExtractLinks(string html)
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
                string resolvedUrl = new Uri(new Uri(_baseUrl), imageUrl).ToString();
                links.Add(resolvedUrl);
            }
        }

        var linkNodes = htmlDoc.DocumentNode.SelectNodes("//a");

        if (linkNodes != null)
        {
            foreach (var link in linkNodes)
            {
                string hrefValue = link.GetAttributeValue("href", string.Empty);
                string resolvedUrl = new Uri(new Uri(_baseUrl), hrefValue).ToString();
                links.Add(resolvedUrl);
            }
        }

        return links;
    }
}
