﻿using System.Diagnostics;
using BookScraper.Console;
using BookScraper.Console.MessageBus;

var baseUrl = "https://books.toscrape.com/";
var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, "ScrapedSite");

var messageBus = new LinkMessageBus();
var producer = new Producer(messageBus);

var stopwatch = new Stopwatch();

var progressViewer = new ProgressViewer(messageBus, baseDirectoryPath, stopwatch);
var queueChecker = new QueueChecker(messageBus);

AddRootPageToQueue(baseUrl, baseDirectoryPath, producer);

int threadCount = 5;
var semaphore = new SemaphoreSlim(threadCount, threadCount);

await StartLog(threadCount, stopwatch);

var consumerTasks = Enumerable.Range(0, threadCount)
    .Select(_ =>
    {
        var consumer = new Consumer(messageBus, baseDirectoryPath, semaphore, queueChecker.CompletedCancellationToken);
        return consumer.Process();
    });

var queueCheckerTask = queueChecker.Start();
Task progressTask = progressViewer.Start(queueChecker.CompletedCancellationToken);

await Task.WhenAll(new List<Task>(consumerTasks) { progressTask });

StopLog(baseDirectoryPath, stopwatch);

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
    await Task.Delay(1500);
    Console.WriteLine($"Three");
    await Task.Delay(1000);
    Console.WriteLine($"Two");
    await Task.Delay(1000);
    Console.WriteLine($"One");
    stopwatch.Start();
}

static void StopLog(string baseDirectoryPath, Stopwatch stopwatch)
{
    Console.Clear();
    stopwatch.Stop();
    Console.WriteLine($"\nProcessing finished, the site can be found at {baseDirectoryPath}.");
    Console.WriteLine($"Elapesd time: {stopwatch.ElapsedMilliseconds} ms");
}