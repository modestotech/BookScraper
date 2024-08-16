using System.Collections.Concurrent;

namespace BookScraper.Console.MessageBus;

internal interface IMessageBus<T>
{
    void Add(T url);
    bool Fetch(out T url);
    int Count();
}

internal class LinkMessageBus : IMessageBus<string>
{
    private readonly ConcurrentQueue<string> _queue = new();
    public void Add(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        _queue.Enqueue(url);
    }

    public int Count()
    {
        return _queue.Count;
    }

    public bool Fetch(out string url)
    {
        return _queue.TryDequeue(out url!);
    }
}
