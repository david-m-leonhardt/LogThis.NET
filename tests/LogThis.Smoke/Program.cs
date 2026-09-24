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

// An exception has no return value; a successful null result does.
List<string> returnValueLogs = [];
using (ServiceProvider returnValueHost = CreateHost(returnValueLogs))
using (returnValueHost.GetRequiredService<ILogThisScopeFactory>().BeginScope("ReturnValues"))
{
    Require(methods.ReturnNull() == null, "A null result changed.");
    try
    {
        await methods.FailAsync();
        throw new Exception("Expected method failure for the return-value check.");
    }
    catch (InvalidOperationException)
    {
    }
}

Require(returnValueLogs.Count == 4, "Expected entry and exit/exception return-value events.");
Require(returnValueLogs.Any(log => log.Contains("ReturnValue: null", StringComparison.Ordinal)),
    "A successful null result was not logged as null.");
Require(returnValueLogs.Any(log => log.Contains("Exception", StringComparison.Ordinal)
    && log.EndsWith("ReturnValue: ", StringComparison.Ordinal)),
    "An exception event did not leave the return value blank.");

// Property-name masking must cover argument and return-value arrays at every depth.
List<string> maskingLogs = [];
ServiceCollection maskingServices = new();
maskingServices.AddLogging(builder => builder.AddProvider(new CaptureProvider(maskingLogs)));
maskingServices.AddLogThisConfiguration(
    JsonFieldsToMask: ["password"],
    JsonMaskValue: "[REDACTED]",
    LogMethodArguments: true,
    LogMethodReturnValue: true);
using (ServiceProvider maskingHost = maskingServices.BuildServiceProvider())
using (maskingHost.GetRequiredService<ILogThisScopeFactory>().BeginScope("Masking"))
{
    List<Credential> credentials =
    [
        new("outer-secret", "outer-visible",
        [new("inner-secret", "inner-visible", [])]),
        new("other-secret", "other-visible", [])
    ];
    Require(methods.Echo(credentials) == credentials, "Masking changed the method result.");
    Require(credentials[0].Password == "outer-secret", "Masking changed the original object.");
}

Require(maskingLogs.Count == 2, "Expected entry and exit masking events.");
Require(maskingLogs.All(log => log.Contains("[REDACTED]", StringComparison.Ordinal)),
    "Mask value is missing from a masking event.");
Require(maskingLogs.All(log => log.Contains("outer-visible", StringComparison.Ordinal)
    && log.Contains("inner-visible", StringComparison.Ordinal)
    && log.Contains("other-visible", StringComparison.Ordinal)),
    "Masking removed an unlisted property or missed a nested list.");
Require(maskingLogs.All(log => !log.Contains("outer-secret", StringComparison.Ordinal)
    && !log.Contains("inner-secret", StringComparison.Ordinal)
    && !log.Contains("other-secret", StringComparison.Ordinal)),
    "A nested password value appeared in a log event.");

// Filtering out a level must skip value serialization as well as provider emission.
List<string> filteredLogs = [];
ServiceCollection filteredServices = new();
filteredServices.AddLogging(builder =>
    builder.SetMinimumLevel(LogLevel.Critical).AddProvider(new CaptureProvider(filteredLogs)));
filteredServices.AddLogThisConfiguration(LogMethodArguments: true, LogMethodReturnValue: true);
using (ServiceProvider filteredHost = filteredServices.BuildServiceProvider())
using (filteredHost.GetRequiredService<ILogThisScopeFactory>().BeginScope("Filtered"))
{
    SerializationProbe probe = new();
    Require(methods.UseProbe(probe) == "ok", "Filtering changed the method result.");
    Require(probe.ReadCount == 0, "A filtered event serialized its arguments.");
    Require(filteredLogs.Count == 0, "A filtered event reached the provider.");
}

// A failing application provider must not prevent the method from running or replace its exception.
ServiceCollection failingServices = new();
failingServices.AddLogging(builder => builder.AddProvider(new ThrowingProvider()));
failingServices.AddLogThisConfiguration(DebugLogThis: true);
using (ServiceProvider failingHost = failingServices.BuildServiceProvider())
using (failingHost.GetRequiredService<ILogThisScopeFactory>().BeginScope("FailingProvider"))
{
    Require(methods.Succeed("still running") == "still running", "A provider failure changed the method result.");

    try
    {
        await methods.FailAsync();
        throw new Exception("Expected method failure with a failing provider.");
    }
    catch (InvalidOperationException exception)
    {
        Require(exception.Message == "Expected failure", "A provider failure replaced the method exception.");
    }
}

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

    /// <summary>Checks that a successful null result remains distinct from an exception.</summary>
    [LogThis]
    public string? ReturnNull() => null;

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

    /// <summary>Supplies an observable argument for the filtered-level serialization check.</summary>
    [LogThis]
    public string UseProbe(SerializationProbe probe) => "ok";

    /// <summary>Preserves a nested collection while its logged representation is masked.</summary>
    [LogThis]
    public List<Credential> Echo(List<Credential> credentials) => credentials;
}

/// <summary>Fixture with a sensitive field and a nested list.</summary>
internal sealed record Credential(string Password, string Visible, List<Credential> Children);

/// <summary>Counts reads of a property that JSON serialization would access.</summary>
internal sealed class SerializationProbe
{
    public int ReadCount { get; private set; }

    public string Value
    {
        get
        {
            ReadCount++;
            return "observed";
        }
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

/// <summary>Provider fixture that fails on every log call to exercise console-only diagnostics.</summary>
internal sealed class ThrowingProvider : ILoggerProvider
{
    /// <summary>Creates a logger that throws when asked to emit an event.</summary>
    public ILogger CreateLogger(string categoryName) => new ThrowingLogger();

    /// <summary>Owns no resources.</summary>
    public void Dispose() { }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException("Configured provider failed");
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
