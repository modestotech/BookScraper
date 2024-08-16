using BookScraper.Console.MessageBus;

internal interface IProducer
{
    void SendMessage(string url);
}

internal class Producer : IProducer
{
    private readonly IMessageBus<string> _messageBus;

    public Producer(IMessageBus<string> messageBus)
    {
        _messageBus = messageBus;
    }

    public void SendMessage(string url)
    {
        _messageBus.Add(url);
    }
}
