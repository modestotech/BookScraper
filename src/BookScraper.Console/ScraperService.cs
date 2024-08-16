using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BookScraper.Console;

internal class ScraperService
{
    private readonly string _baseUrl;
    private readonly HttpClient _client = new();
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly ConcurrentDictionary<string, bool> _processedLinks = new();
    private readonly SemaphoreSlim _semaphore;

    public ScraperService(string baseUrl, int threadCount = 5)
    {
        _baseUrl = baseUrl;
        _semaphore = new SemaphoreSlim(threadCount);
    }

    public async Task StartAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            throw new ArgumentNullException(nameof(_baseUrl));
        }

        if (!UrlUtils.UrlIsValid(_baseUrl))
        {
            throw new ArgumentException("A valid base URL is not provided");
        }

        _queue.Enqueue(UrlUtils.CleanUrl(_baseUrl));
        _processedLinks.TryAdd(_baseUrl, true);

        using (_semaphore)
        {
            var tasks = new List<Task>();

            while (_queue.TryDequeue(out var url))
            {
                await _semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ProcessUrlAsync(url);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessUrlAsync(string url)
    {
        try
        {
            var uri = UrlUtils.GetUri(url);
            string savePath = Path.Combine(AppContext.BaseDirectory, uri.AbsolutePath);

            var isImageContent = Regex.IsMatch(url, @"\.(gif|jpe?g|tiff?|png|webp|bmp)$", RegexOptions.IgnoreCase);

            if (isImageContent)
            {
                await SaveBinaryFileToDisk(uri, savePath);
                return;
            }

            var html = await SaveHtmlFileToDiskAndReturnHtml(uri, savePath);

            AddNewLinksToQueue(html);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {url}: {ex.Message}");
        }
    }

    private async Task<string> SaveHtmlFileToDiskAndReturnHtml(Uri uri, string savePath)
    {
        var textContent = await _client.GetStringAsync(uri);
        await File.WriteAllTextAsync(savePath, textContent);
        return textContent;
    }

    private async Task SaveBinaryFileToDisk(Uri uri, string savePath)
    {
        byte[] imageBytes = await _client.GetByteArrayAsync(uri);
        await File.WriteAllBytesAsync(savePath, imageBytes);
    }

    private void AddNewLinksToQueue(string html)
    {
        var newLinks = ExtractLinks(html);

        foreach (var link in newLinks)
        {
            // Add new links to the queue if not already processed
            if (_processedLinks.TryAdd(link, true))
            {
                _queue.Enqueue(link);
            }
        }
    }

    private IEnumerable<string> ExtractLinks(string html)
    {
        // Use a parser like HtmlAgilityPack or AngleSharp to extract links
        return new List<string>(); // Placeholder
    }
}
