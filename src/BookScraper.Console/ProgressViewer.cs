using System.Diagnostics;
using BookScraper.Console.MessageBus;

internal class ProgressViewer
{
    private readonly string _baseDirectory;
    private readonly LinkMessageBus _messageBus;
    private readonly Stopwatch _stopwatch;

    public ProgressViewer(LinkMessageBus messageBus, string baseDirectory, Stopwatch stopwatch)
    {
        _baseDirectory = baseDirectory;
        _messageBus = messageBus;
        _stopwatch = stopwatch;
    }

    internal async Task Start(CancellationToken token)
    {
        while (true)
        {
            await Task.Delay(1_000, CancellationToken.None);

            if (token.IsCancellationRequested)
            {
                return;
            }

            int fileCount;

            if (Directory.Exists(_baseDirectory))
            {
                try
                {
                    fileCount = Directory.GetFiles(_baseDirectory, "*.*", SearchOption.AllDirectories).Length;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exiting, could not get current file count. Error message: " + ex.Message);
                    return;
                }
            }
            else
            {
                continue;
            }

            Console.Clear();
            Console.WriteLine($"The queue currently has {_messageBus.Count()} urls to process");
            Console.WriteLine($"Until now {fileCount} files have been downloaded");
            Console.WriteLine($"Elapesd time: {_stopwatch.ElapsedMilliseconds} ms");
        }
    }
}