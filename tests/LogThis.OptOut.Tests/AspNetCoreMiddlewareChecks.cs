using System.Collections.Concurrent;
using System.Net;
using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class AspNetCoreMiddlewareChecks
{
    public static async Task RunAsync()
    {
        await CheckHostAsync(null);
        await CheckHostAsync("RequestCategory");
        Console.WriteLine("ASP.NET Core middleware HTTP checks passed.");
    }

    private static async Task CheckHostAsync(string? category)
    {
        RequestLogger sink = new();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(sink);
        builder.Services.AddLogThisConfiguration();
        await using WebApplication app = builder.Build();
        ConcurrentDictionary<string, bool> restored = new();
        ConcurrentDictionary<string, Exception> failures = new();
        TaskCompletionSource bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int entered = 0;

        // Observe the scope from outside the middleware, including its exception unwind.
        app.Use(async (context, next) =>
        {
            LogThisRuntime? before = LogThisRuntimeContext.Current;
            try
            {
                await next(context);
            }
            catch (RequestFailure exception)
            {
                failures[context.Request.Path.Value!] = exception;
                context.Response.StatusCode = 500;
            }
            finally
            {
                restored[context.Request.Path.Value!] =
                    before == null && ReferenceEquals(before, LogThisRuntimeContext.Current);
            }
        });
        if (category == null) app.UseLogThis();
        else app.UseLogThis(category);

        app.MapGet("/{id}", async (HttpContext context, string id) =>
        {
            // No explicit logging scope: the HTTP adapter must provide it.
            Require(LogThisRuntimeContext.Current != null, "Endpoint has no LogThis scope.");
            Task barrier = Task.CompletedTask;
            if (id.StartsWith("parallel", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref entered) == 2) bothEntered.TrySetResult();
                barrier = bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            string correlation = await ExecuteAsync(barrier, id == "failure");
            await context.Response.WriteAsync(correlation);
        });

        await app.StartAsync();
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
            string first = await client.GetStringAsync("first");
            string second = await client.GetStringAsync("second");
            string[] parallel = await Task.WhenAll(client.GetStringAsync("parallel1"), client.GetStringAsync("parallel2"));
            using HttpResponseMessage failed = await client.GetAsync("failure");
            Require(failed.StatusCode == HttpStatusCode.InternalServerError, "Middleware changed failure propagation.");
            string afterFailure = await client.GetStringAsync("afterFailure");

            string[] successfulIds = [first, second, .. parallel, afterFailure];
            Require(successfulIds.All(id => id.Length == 16) && successfulIds.Distinct().Count() == 5,
                "Sequential or overlapping requests shared correlation IDs.");
            Require(restored.Count == 6 && restored.Values.All(value => value), "Request scope was not restored.");
            RequestEvent[] events = sink.Events.ToArray();
            Require(events.Length == 12, "Expected entry and completion for each request.");
            Require(events.All(e => e.Category == (category ?? "LogThis")), "HTTP logger category was lost.");
            Require(events.Select(e => e.Correlation).Distinct().Count() == 6, "Failure request shared a correlation ID.");
            foreach (IGrouping<string, RequestEvent> chain in events.GroupBy(e => e.Correlation))
            {
                Require(chain.Count() == 2 && chain.Count(e => e.Kind == "LogThisOnEntry") == 1 &&
                    chain.Count(e => e.Kind is "LogThisOnExit" or "LogThisOnException") == 1,
                    "Request chain lost entry/completion correlation.");
            }
            foreach (string id in successfulIds)
                Require(events.Any(e => e.Correlation == id && e.Kind == "LogThisOnExit"), "Response and logs disagree on correlation.");
            Require(failures.TryGetValue("/failure", out Exception? original) &&
                ReferenceEquals(events.Single(e => e.Kind == "LogThisOnException").Exception, original),
                "Middleware changed the original exception.");
        }
        finally
        {
            await app.StopAsync();
        }
        Require(LogThisRuntimeContext.Current == null, "HTTP tests leaked scope into their caller.");
    }

    [LogThis]
    private static async Task<string> ExecuteAsync(Task barrier, bool fail)
    {
        string correlation = LogThisRuntimeContext.Current!.CorrelationId!;
        await barrier;
        Require(LogThisRuntimeContext.Current!.CorrelationId == correlation, "Correlation changed across await.");
        if (fail) throw new RequestFailure();
        return correlation;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RequestFailure : Exception;
    private sealed record RequestEvent(string Category, string Correlation, string Kind, Exception? Exception);

    private sealed class RequestLogger : ILoggerProvider
    {
        internal ConcurrentQueue<RequestEvent> Events { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Events);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(string category, ConcurrentQueue<RequestEvent> events) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> properties) return;
            Dictionary<string, object?> fields = properties.ToDictionary(p => p.Key, p => p.Value);
            if (!fields.TryGetValue("LogThisMethod", out object? method) || !Equals(method, nameof(ExecuteAsync))) return;
            string kind = fields.Keys.Single(key => key is "LogThisOnEntry" or "LogThisOnExit" or "LogThisOnException");
            events.Enqueue(new RequestEvent(category, (string)fields["LogThisCorrelationId"]!, kind, exception));
        }
    }
}
