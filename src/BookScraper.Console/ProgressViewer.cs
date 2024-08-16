using BookScraper.Console.MessageBus;

internal class ProgressViewer
{
    private readonly string _baseDirectory;
    private readonly LinkMessageBus _messageBus;

    public ProgressViewer(LinkMessageBus messageBus, string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        _messageBus = messageBus;
    }

    internal async Task Start()
    {
        await Task.Delay(2000);

        while (true)
        {
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
            Console.WriteLine($"The queue currently has {_messageBus.Count()} items");
            Console.WriteLine($"Until now {fileCount} files have been processed");
        }
    }
}