using System.Collections.Concurrent;

namespace BookScraper.Console.MessageBus;

internal interface IMessageBus<T>
{
    void Add(T url);
    bool Fetch(out T url);
}

internal class LinkMessageBus : IMessageBus<string>
{
    private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
    public void Add(string url)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        _queue.Enqueue(url);
    }

    public bool Fetch(out string url)
    {
        return _queue.TryDequeue(out url);
    }
}
