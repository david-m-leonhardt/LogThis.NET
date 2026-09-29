using LogThis.Attributes;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var sink = new RecordingLogger();
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddProvider(sink));
services.AddLogThisConfiguration(options => options.LogCorrelationId = false);
using var provider = services.BuildServiceProvider();
using var scope = provider.UseLogThis();
var sample = new Sample(sink);

void Expect(params string[] messages)
{
    if (!sink.Messages.SequenceEqual(messages))
        throw new Exception($"Expected [{string.Join(", ", messages)}], got [{string.Join(", ", sink.Messages)}]");
    sink.Messages.Clear();
}

if (sample.Excluded() != 42) throw new Exception("Return value changed.");
Expect("explicit", "Entered | Method: Included", "Exited | Method: Included");
if (await sample.ExcludedAsync() != 43) throw new Exception("Async return value changed.");
Expect("async explicit", "Entered | Method: Included", "Exited | Method: Included");
sample.Both();
Expect("both explicit");
new MethodOnly(sink).Both();
Expect("method explicit");
new MethodOnly(sink).NoOp();
Expect("no-op explicit");
sample.Excluded(1);
Expect("Entered | Method: Excluded", "Exited | Method: Excluded");
var failure = new InvalidOperationException("original");
try { sample.Fail(failure); throw new Exception("Exception was swallowed."); }
catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
Expect("failure explicit");
try { await sample.FailAsync(failure); throw new Exception("Async exception was swallowed."); }
catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }
Expect("async failure explicit");
await DataExclusionChecks.RunAsync();
await NamedArgumentChecks.RunAsync();
await DurationChecks.RunAsync();
scope.Dispose();
await CorrelationChecks.RunAsync();
ComponentChecks.Run();
await MaskingChecks.RunAsync();
Console.WriteLine("All opt-out integration checks passed.");

[LogThis]
public class Sample(ILogger logger)
{
    [DontLogThis]
    public int Excluded() { logger.LogInformation("explicit"); Included(); return 42; }

    public void Excluded(int value) { }

    [DontLogThis]
    public async Task<int> ExcludedAsync()
    {
        await Task.Yield();
        logger.LogInformation("async explicit");
        Included();
        return 43;
    }

    public void Included() { }

    [LogThis, DontLogThis]
    public void Both() => logger.LogInformation("both explicit");

    [DontLogThis]
    public void Fail(Exception exception) { logger.LogInformation("failure explicit"); throw exception; }

    [DontLogThis]
    public async Task FailAsync(Exception exception)
    {
        await Task.Yield();
        logger.LogInformation("async failure explicit");
        throw exception;
    }
}

public class MethodOnly(ILogger logger)
{
    [LogThis, DontLogThis]
    public void Both() => logger.LogInformation("method explicit");

    [DontLogThis]
    public void NoOp() => logger.LogInformation("no-op explicit");
}

public sealed class RecordingLogger : ILogger, ILoggerProvider
{
    public List<string> Messages { get; } = [];
    public ILogger CreateLogger(string categoryName) => this;
    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public void Dispose() { }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
