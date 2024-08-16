using BookScraper.Console;
using BookScraper.Console.MessageBus;

var baseUrl = "https://books.toscrape.com/";
var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, "ScrapedSite");

var messageBus = new LinkMessageBus();
var producer = new Producer(messageBus);
var consumer = new Consumer(messageBus, baseDirectoryPath, baseUrl);

AddRootPageToQueue(baseUrl, baseDirectoryPath, producer);

await consumer.Process();

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
