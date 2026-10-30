using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies production cancellation metadata for method and consumer token combinations.</summary>
public static class CancellationAttributeChecks
{
    #region Public Methods

    /// <summary>Checks linked tokens, lazy execution, cancellation timing, and disposal for both attribute placements.</summary>
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogThisProfile("Iterator", _ => { });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        foreach (var sample in new IProductionIteratorSample[] { new MethodIteratorSample(), new ClassIteratorSample() })
        foreach (string scenario in new[] { "method", "consumer", "both-method", "both-consumer" })
        {
            using CancellationTokenSource method = new();
            using CancellationTokenSource consumer = new();
            CancellationToken methodToken = scenario == "consumer" ? default : method.Token;
            CancellationToken consumerToken = scenario == "method" ? default : consumer.Token;
            var state = new IteratorState();
            IAsyncEnumerable<int> sequence = sample.AsyncSequence(state, methodToken);
            if (state.Reads != 0 || state.Disposals != 0) throw new Exception("Iterator ran at creation.");
            await using (IAsyncEnumerator<int> iterator = sequence.GetAsyncEnumerator(consumerToken))
            {
                if (state.Reads != 0) throw new Exception("Requesting an enumerator ran its body.");
                if (!await iterator.MoveNextAsync() || iterator.Current != 1 || state.Reads != 1)
                    throw new Exception("First item changed.");
                if (scenario is "method" or "both-method") method.Cancel();
                else consumer.Cancel();
                try
                {
                    await iterator.MoveNextAsync();
                    throw new Exception("Cancellation did not reach iterator: " + scenario);
                }
                catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested) { }
            }
            if (state.Reads != 1 || state.Disposals != 1) throw new Exception("Production/disposal changed.");
        }
    }

    #endregion
}
