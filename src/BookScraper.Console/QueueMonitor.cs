using BookScraper.Console.MessageBus;

internal class QueueChecker
{
    private readonly CancellationTokenSource _completedCancellationTokenSource;
    private readonly LinkMessageBus _messageBus;

    public QueueChecker(LinkMessageBus messageBus)
    {
        _completedCancellationTokenSource = new CancellationTokenSource();
        _messageBus = messageBus;
    }

    public CancellationToken CompletedCancellationToken { get { return _completedCancellationTokenSource.Token; } }

    internal async Task Start()
    {
        await Task.Delay(5_000);

        while (true)
        {
            await Task.Delay(1_000);

            if (_messageBus.Count() > 0)
            {
                continue;
            }

            _completedCancellationTokenSource.Cancel();
            return;
        }
    }
}