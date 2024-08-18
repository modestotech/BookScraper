using System.Diagnostics;
using BookScraper.Console;
using BookScraper.Console.MessageBus;

var baseUrl = "https://books.toscrape.com/";
var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, "ScrapedSite");

var messageBus = new LinkMessageBus();
var producer = new Producer(messageBus);

var stopwatch = new Stopwatch();

var progressViewer = new ProgressViewer(messageBus, baseDirectoryPath, stopwatch);
var queueMonitor = new QueueMonitor(messageBus);

AddRootPageToQueue(baseUrl, baseDirectoryPath, producer);

int threadCount = (int)(Environment.ProcessorCount * 2.5);
var semaphore = new SemaphoreSlim(threadCount, threadCount);

var queueMonitorTask = queueMonitor.Start();

await StartLog(threadCount, stopwatch);

var consumerTasks = Enumerable.Range(0, threadCount)
    .Select(_ =>
    {
        var consumer = new Consumer(messageBus, baseDirectoryPath, semaphore, queueMonitor.CompletedCancellationToken);
        return consumer.Process();
    });

Task progressTask = progressViewer.Start(queueMonitor.CompletedCancellationToken);

await Task.WhenAll(new List<Task>(consumerTasks) { progressTask, queueMonitorTask });

await StopLog(baseDirectoryPath, stopwatch);

Environment.Exit(0);

static void AddRootPageToQueue(string baseUrl, string baseDirectoryPath, Producer producer)
{
    if (Directory.Exists(baseDirectoryPath))
    {
        try
        {
            Directory.Delete(baseDirectoryPath, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exiting, could not delete base directory, might be in use. Error message: " + ex.Message);
        }
    }

    producer.SendMessage(UrlUtils.CleanUrl(baseUrl));
}

static async Task StartLog(int threadCount, Stopwatch stopwatch)
{
    Console.WriteLine($"Will start to process with {threadCount} threads in...");
    await Task.Delay(1_500);
    Console.WriteLine($"Three");
    await Task.Delay(1_000);
    Console.WriteLine($"Two");
    await Task.Delay(1_000);
    Console.WriteLine($"One");
    await Task.Delay(1_000);
    Console.WriteLine($"Go!");
    stopwatch.Start();
}

static async Task StopLog(string baseDirectoryPath, Stopwatch stopwatch)
{
    Console.Clear();
    stopwatch.Stop();
    Console.WriteLine($"Processing finished, the site can be found at:\n{baseDirectoryPath}.");
    Console.WriteLine($"Elapesd time: {stopwatch.ElapsedMilliseconds} ms");
    await Task.Delay(2_000);
}
