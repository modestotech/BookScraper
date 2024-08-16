using BookScraper.Console.MessageBus;

internal class QueueMonitor
{
    private readonly CancellationTokenSource _completedCancellationTokenSource;
    private readonly LinkMessageBus _messageBus;

    public QueueMonitor(LinkMessageBus messageBus)
    {
        _completedCancellationTokenSource = new CancellationTokenSource();
        _messageBus = messageBus;
    }

    public CancellationToken CompletedCancellationToken
    {
        get { return _completedCancellationTokenSource.Token; }
    }

    internal Task Start()
    {
        var task = Task.Factory.StartNew(
            async () =>
        {
            await Task.Delay(6_000);

            while (true)
            {
                if (_messageBus.Count() > 0)
                {
                    continue;
                }

                _completedCancellationTokenSource.Cancel();
            }
        }, TaskCreationOptions.LongRunning);

        return task;
    }
}