// Smoke coverage for method-level and class-level aspects, async results, exceptions, and nested scopes.
using LogThis.Attributes;
using LogThis.Interfaces;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

List<string> firstHostLogs = [];
List<string> secondHostLogs = [];

using ServiceProvider firstHost = CreateHost(firstHostLogs);
using ServiceProvider secondHost = CreateHost(secondHostLogs);
TestMethods methods = new();
WeatherForecastController controller = new();

// Nested hosts verify that disposing an inner scope restores the outer runtime.
using (firstHost.GetRequiredService<ILogThisScopeFactory>().BeginScope("First"))
{
    Require(methods.Succeed("first") == "first", "Synchronous result changed.");
    Require(await methods.SucceedAsync("awaited") == "awaited", "Asynchronous result changed.");

    using (secondHost.GetRequiredService<ILogThisScopeFactory>().BeginScope("Second"))
    {
        Require(methods.Succeed("second") == "second", "Nested result changed.");
    }

    Require(methods.Succeed("restored") == "restored", "Outer scope was not restored.");
    Require(await controller.GetAsync() == "forecast", "Class-level aspect changed the result.");

    try
    {
        await controller.FailAsync();
        throw new Exception("Expected controller failure.");
    }
    catch (InvalidOperationException)
    {
    }

    try
    {
        await methods.FailAsync();
        throw new Exception("Expected method failure.");
    }
    catch (InvalidOperationException)
    {
    }
}

Require(firstHostLogs.Count == 12, $"Expected 12 first-host events, got {firstHostLogs.Count}.");
Require(secondHostLogs.Count == 2, $"Expected 2 second-host events, got {secondHostLogs.Count}.");
Require(firstHostLogs.Any(log => log.Contains("Exception", StringComparison.Ordinal)), "Missing exception log.");
Require(firstHostLogs.Any(log => log.Contains("GetAsync", StringComparison.Ordinal)), "Class-level method was not logged.");
Require(secondHostLogs.All(log => log.Contains("second", StringComparison.Ordinal)), "Host logs were mixed.");

Console.WriteLine("LogThis smoke test passed.");

// Give each host its own capture sink and LogThis configuration.
static ServiceProvider CreateHost(List<string> logs)
{
    ServiceCollection services = new();
    services.AddLogging(builder => builder.AddProvider(new CaptureProvider(logs)));
    services.AddLogThisConfiguration(LogMethodArguments: true, LogMethodReturnValue: true);
    return services.BuildServiceProvider();
}

// Fail the smoke run immediately when a behavior changes.
static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

/// <summary>Method-level aspect fixtures for sync, async, and exception paths.</summary>
internal sealed class TestMethods
{
    /// <summary>Checks preservation of a synchronous return value.</summary>
    [LogThis]
    public string Succeed(string value) => value;

    /// <summary>Checks logging after an asynchronous completion.</summary>
    [LogThis]
    public async Task<string> SucceedAsync(string value)
    {
        await Task.Yield();
        return value;
    }

    /// <summary>Checks logging of an asynchronously raised exception.</summary>
    [LogThis]
    public async Task FailAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("Expected failure");
    }
}

/// <summary>Class-level aspect fixture covering all public concrete methods.</summary>
[LogThis]
internal sealed class WeatherForecastController
{
    /// <summary>Checks a successful class-level async override.</summary>
    public async Task<string> GetAsync()
    {
        await Task.Yield();
        return "forecast";
    }

    /// <summary>Checks a failing class-level async override.</summary>
    public async Task FailAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("Controller failure");
    }
}

/// <summary>Minimal logger provider that captures formatted messages for assertions.</summary>
internal sealed class CaptureProvider(List<string> messages) : ILoggerProvider
{
    /// <summary>Creates a logger backed by this provider's message list.</summary>
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);
    /// <summary>No unmanaged resources are owned by this test provider.</summary>
    public void Dispose() { }

    /// <summary>Formats events into the shared assertion list.</summary>
    private sealed class CaptureLogger(List<string> messages) : ILogger
    {
        /// <summary>Returns a no-op scope because scope behavior is tested by LogThis itself.</summary>
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        /// <summary>Accepts every level in this smoke test.</summary>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Adds the provider-formatted message to the assertion list.</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    /// <summary>Disposable placeholder for the capture logger's scope API.</summary>
    private sealed class NullScope : IDisposable
    {
        /// <summary>Shared no-op scope instance.</summary>
        public static readonly NullScope Instance = new();
        /// <summary>Does nothing because the capture logger maintains no scope state.</summary>
        public void Dispose() { }
    }
}
