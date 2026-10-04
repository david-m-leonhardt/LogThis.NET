# LogThis.NET

LogThis.NET adds configurable method-entry, method-exit, and exception logging
through the `[LogThis]` attribute. It uses `Microsoft.Extensions.Logging`, so
applications keep control of their logging provider, and Metalama to weave the
logging code at build time.

> [!IMPORTANT]
> This documentation targets LogThis.NET 1.1.0-beta.1. LogThis.NET 1.0.0 is
> the latest stable release.

## Requirements

- .NET 10 SDK
- A configured `Microsoft.Extensions.Logging` provider
- `Metalama.Framework`, included transitively by the LogThis.NET packages

Two packages are available:

- `LogThis.NET` contains the attribute, configuration, and explicit scopes for
  console applications, workers, and other non-HTTP hosts.
- `LogThis.NET.AspNetCore` adds per-request middleware and includes
  `LogThis.NET` transitively.

## Installation

For a console application, worker, or other non-HTTP host:

```powershell
dotnet add package LogThis.NET --version 1.1.0-beta.1
```

For an ASP.NET Core application:

```powershell
dotnet add package LogThis.NET.AspNetCore --version 1.1.0-beta.1
```

Applications using `[LogThis]` must be built with the .NET 10 SDK so Metalama
can weave the logging code.

## ASP.NET Core quick start

Register LogThis.NET and add its middleware before mapping endpoints that call
marked methods:

```csharp
using LogThis.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogThisConfiguration();
builder.Services.AddControllers();

WebApplication app = builder.Build();

app.UseLogThis();
app.MapControllers();

app.Run();
```

`UseLogThis()` opens an independent logging scope for each request. To use a
category other than the default `LogThis` category, pass it to the middleware:

```csharp
app.UseLogThis("MyApplication.Methods");
```

Apply `[LogThis]` to a method that should emit entry, exit, and exception
events:

```csharp
using LogThis.Attributes;

public class OrdersService
{
    [LogThis]
    public async Task<Order> SubmitOrderAsync(OrderRequest request)
    {
        // Method implementation
    }
}
```

See [Attributes](#attributes) for class-level logging, exclusions, and
sensitive-property masking.

## Logging provider setup

LogThis.NET writes through `Microsoft.Extensions.Logging`; it does not require
an `ILogger` instance to be passed during registration. Configure a logging
provider on the host, and then call `AddLogThisConfiguration()`. When
`UseLogThis()` opens a request scope, LogThis uses the DI-managed
`ILoggerFactory` to create its category logger.

The following examples show the relevant startup registration for common
providers. After any ASP.NET Core example, build the application and add the
LogThis middleware as shown in the quick start:

```csharp
WebApplication app = builder.Build();

app.UseLogThis();
app.MapControllers();

app.Run();
```

### Built-in console logger

The console provider is useful for a minimal setup and local development:

```csharp
using LogThis.Middleware;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);

builder.Services.AddLogThisConfiguration();
builder.Services.AddControllers();
```

ASP.NET Core includes the console provider. A non-hosted console application can
register it with `services.AddLogging(logging => logging.AddConsole())`, as
shown in the next section.

### log4net

Install `Microsoft.Extensions.Logging.Log4Net.AspNetCore`, add a
`log4net.config` file, and copy that file to the output directory:

```csharp
using LogThis.Middleware;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddLog4Net();

builder.Services.AddLogThisConfiguration();
builder.Services.AddControllers();
```

The logging provider's default configuration filename is `log4net.config`.

### Serilog

Install `Serilog.AspNetCore` and the sinks required by the application. This
example uses the console sink:

```csharp
using LogThis.Middleware;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddLogThisConfiguration();
builder.Services.AddControllers();
```

`AddSerilog()` routes events from `Microsoft.Extensions.Logging`, including
LogThis events, through the Serilog pipeline.

### NLog

Install `NLog.Web.AspNetCore` and add an `NLog.config` file:

```csharp
using LogThis.Middleware;
using NLog.Web;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddLogThisConfiguration();
builder.Services.AddControllers();
```

`UseNLog()` connects the host's `Microsoft.Extensions.Logging` pipeline to
NLog. Provider-specific targets, levels, and formatting remain the
application's responsibility.

## Console applications and background services

Non-HTTP applications must register LogThis.NET and keep an explicit scope
active while marked methods run. A generic host already provides
`IConfiguration` and logging; register LogThis.NET with its service collection:

```csharp
services.AddLogThisConfiguration();
```

For a plain console application, register configuration and logging before
building the service provider. This example requires
`Microsoft.Extensions.Configuration.Json` and an `appsettings.json` file that
is copied to the output directory:

```csharp
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

ServiceCollection services = new();
services.AddSingleton(configuration);
services.AddLogging(logging => logging.AddConsole());
services.AddLogThisConfiguration();

using ServiceProvider serviceProvider = services.BuildServiceProvider();
```

Open a scope around the operation that calls marked methods:

```csharp
using (serviceProvider.UseLogThis("MyApplication.Worker"))
{
    await RunApplicationAsync();
}
```

The same operation is available by resolving `ILogThisScopeFactory` and calling
`BeginScope()`. The scope flows across `await`, making it suitable for a
background service's `ExecuteAsync` method. Marked methods called without an
active scope do not produce LogThis events.

## Attributes

LogThis.NET provides three attributes with distinct purposes:

- `[LogThis]` enables method-boundary logging.
- `[DontLogThis]` excludes a method or captured value.
- `[MaskThisInLogs]` replaces a captured property value with safe text.

### `[LogThis]`

As shown in the quick start, `[LogThis]` can target one method. Apply it to a
class to wrap each public, non-abstract method:

```csharp
using LogThis.Attributes;

[LogThis]
public class OrdersService
{
    public void SubmitOrder(OrderRequest request) { }

    public Task<Order> GetOrderAsync(string orderId) =>
        throw new NotImplementedException();
}
```

Awaitable methods log their exit after completion and log exceptions thrown
during the await.

Each access point is enabled by default with the following level and
`AccessPointMessage`:

| Event | Level | Message |
| --- | --- | --- |
| Entry | `Information` | `Entered` |
| Exit | `Information` | `Exited` |
| Exception | `Error` | `Exception` |

`Entered`, `Exited`, and `Exception` are defaults, not fixed labels. Change
them independently through `OnEntryConfig.AccessPointMessage`,
`OnExitConfig.AccessPointMessage`, and
`OnExceptionConfig.AccessPointMessage`.

The method name is included by default, and message components are separated by
` | `. Class names, arguments, return values, duration, and custom properties
are omitted until enabled. Correlation IDs and readable component labels are
enabled by default.

### `[DontLogThis]`

`[DontLogThis]` opts specific methods or data out of LogThis without changing
application behavior. Apply it to:

- a method to suppress that method's entry, exit, and exception events
- a parameter to omit it from captured arguments
- a property to omit it whenever its containing object is captured

Exclusion means omission. It does not replace a value with masking text, disable
logging in called methods, or affect explicit application `ILogger` calls.

#### Excluding a method

Use `[DontLogThis]` on a method that would otherwise be included by a
class-level or method-level `[LogThis]`:

```csharp
[LogThis]
public class OrderService(ILogger<OrderService> logger)
{
    [DontLogThis]
    public void SubmitOrder()
    {
        logger.LogInformation("Submitting order");
        ValidateOrder();
    }

    public void ValidateOrder() { }
}
```

LogThis emits no events for `SubmitOrder`, but the explicit log statement still
runs and `ValidateOrder` follows its own instrumentation rules. Return values,
async behavior, and exception propagation are unchanged. If an instrumented
caller observes an exception from an excluded method, that caller can still log
its own exception event.

A method exclusion takes precedence over `[LogThis]` on the same method and
applies only to that method. Annotate overloads, overrides, and interface
implementations separately. Classes and constructors cannot be marked with
`[DontLogThis]`.

#### Excluding parameters and properties

The same `[DontLogThis]` attribute can omit a parameter from argument capture
or a property from object capture:

```csharp
using LogThis.Attributes;

public class OrderRequest
{
    public string OrderId { get; set; } = "";

    [DontLogThis]
    public string PaymentToken { get; set; } = "";
}

[LogThis]
public class CheckoutService
{
    public OrderRequest Submit(OrderRequest request, [DontLogThis] string apiKey)
    {
        // Normal application code can still use request.PaymentToken and apiKey.
        return request;
    }
}
```

With argument logging enabled, `apiKey` is absent from the argument JSON. If
every parameter is excluded, the component contains `{}`. `PaymentToken` is
omitted from any captured `OrderRequest`, including nested objects and
collection items.

LogThis excludes ordinary properties before serialization and does not invoke
their getters. It does not modify application objects, results, or exceptions.
Parameter and property exclusions matter only when the corresponding argument
or return-value capture is enabled.

`[DontLogThis]` governs LogThis capture only. It cannot remove a value that the
application writes through its own logger or embeds in exception text.

#### Named argument output

Parameter names come from method metadata at build time and are paired with
their values when the method is called. For example:

```csharp
[LogThis]
public void Authenticate(string username, [DontLogThis] string password, bool rememberMe)
{
    // Application code
}
```

With `LogMethodArguments = true`, calling `Authenticate("alice", "secret", true)`
produces this argument component on entry (and on an exception event if it throws):

```text
Arguments: {"username":"alice","rememberMe":true}
```

Null included arguments retain their names with a `null` value. Property
exclusions and `JsonFieldsToMask` still apply within each included value;
parameter names themselves do not expand the property-masking rules. Use
`[DontLogThis]` to omit a parameter. Disabling argument logging omits the entire
argument component and avoids serialization, while configured events continue.

The structured `{LogThisArguments}` property contains the complete JSON string,
not one logger property per parameter. Renaming a source parameter changes its
JSON key after rebuilding.

### `[MaskThisInLogs]`

Use `[MaskThisInLogs]` on a model property that may be captured but should not
expose its value:

```csharp
public class LoginRequest
{
    [MaskThisInLogs]
    public string Password { get; init; } = "";

    [MaskThisInLogs("[PRIVATE]")]
    public CustomerDetails Customer { get; init; } = new();
}
```

Without an argument, the attribute uses the configured `JsonMaskValue`
(`*****` by default). Passing a string uses that fixed replacement; an empty
string is valid. The replacement covers the property's entire value, including
objects and collections.

The attribute applies to properties captured as arguments, return values,
nested objects, or collection items. For ordinary properties, LogThis writes the
replacement without invoking the getter. It does not modify the original object
or sanitize explicit application logs and exception messages.

`[DontLogThis]` takes precedence and omits the property. An explicit
`[MaskThisInLogs("...")]` value takes precedence over configuration-based
masking.

## Sensitive-data masking

Use `JsonFieldsToMask` for models that cannot be annotated, or to apply a
shared rule by serialized property name:

```csharp
builder.Services.AddLogThisConfiguration(options =>
{
    options.JsonFieldsToMask = ["Password", "AuthToken"];
    options.JsonMaskValue = "*****";
    options.LogMethodArguments = true;
    options.LogMethodReturnValue = true;
});
```

Matching is case-insensitive and applies at any nesting depth, including objects
inside arrays. A match replaces the property's entire value. Dotted paths,
wildcards, and partial-value masking are not supported.

Masking precedence is:

1. `[DontLogThis]` omits the property.
2. `[MaskThisInLogs("replacement")]` uses its explicit replacement.
3. `[MaskThisInLogs]` or a configured name match uses `JsonMaskValue`.

For ordinary object properties, attribute and configured-name masking occur
without invoking the matched getter. Dictionary entries, existing JSON objects,
and custom converters can require serialization or traversal before a
configuration-based match is replaced. Disabled argument or return-value
capture performs no masking work.

Masking protects only LogThis-captured data. Unlisted properties remain
unchanged, and application-authored logs or exception messages can still expose
their values. Enable argument and return-value logging only when appropriate for
the application's data and volume.

## Configuration

### Configure in code

Pass a callback to override the defaults:

```csharp
using LogThis.Middleware;
using Microsoft.Extensions.Logging;

builder.Services.AddLogThisConfiguration(options =>
{
    options.LogClassName = true;
    options.LogMethodArguments = true;
    options.LogMethodReturnValue = true;
    options.LogMethodDuration = true;
    options.OnEntryConfig.LogLevel = LogLevel.Debug;
    options.OnEntryConfig.AccessPointMessage = "Starting";
    options.OnExceptionConfig.AccessPointMessage = "Failed";
    options.OnExitConfig.AccessPointMessage = "Finished";
    options.AddMessageComponents(new Dictionary<string, object>
    {
        ["Application"] = "Ordering"
    });
});
```

When application configuration is also available, LogThis binds it first and
then applies the callback.

### Configure with `appsettings.json`

LogThis automatically reads the `LogThis` section from a registered
`IConfiguration`:

```json
{
  "LogThis": {
    "LogClassName": true,
    "LogMethodArguments": true,
    "LogMethodReturnValue": true,
    "LogMethodDuration": true,
    "JsonFieldsToMask": ["Password", "AuthToken"],
    "JsonMaskValue": "[REDACTED]",
    "MessageComponents": {
      "Application": "Ordering"
    },
    "OnEntryConfig": {
      "LogLevel": "Debug",
      "AccessPointMessage": "Starting"
    },
    "OnExceptionConfig": {
      "LogLevel": "Error",
      "AccessPointMessage": "Failed"
    },
    "OnExitConfig": {
      "AccessPointMessage": "Finished"
    }
  }
}
```

Omitted settings keep their defaults. Unknown keys in an existing `LogThis`
section cause an error when configuration is first resolved. If the section is
absent, LogThis uses all defaults. Configuration is read once per service
provider; file reloads do not update an active provider.

An application can also pass a specific `IConfigurationSection` or a prepared
`LogThisConfiguration` instance to `AddLogThisConfiguration`.

### Named configuration profiles

The default configuration is always available. Add profiles under `LogThis:Profiles`
to give selected methods a different complete configuration:

```json
{
  "LogThis": {
    "LogClassName": true,
    "OnEntryConfig": { "AccessPointMessage": "Starting" },
    "Profiles": {
      "Detailed": {
        "LogMethodArguments": true,
        "LogMethodReturnValue": true,
        "LogMethodDuration": true,
        "JsonFieldsToMask": ["Password", "AuthToken"]
      },
      "ExternalCall": {
        "LogMethodDuration": true,
        "OnEntryConfig": { "LogAccessPoint": false },
        "OnExceptionConfig": { "AccessPointMessage": "External call failed" }
      }
    }
  }
}
```

`AddLogThisConfiguration()` loads both the default settings and named profiles.
Each profile is a complete configuration built from library defaults; it does
not inherit the application's default configuration or another profile. In this
example, `Detailed` still uses `LogClassName = false` and the default entry
message `Entered`.

Profiles can also be registered in code:

```csharp
builder.Services.AddLogThisConfiguration();
builder.Services.AddLogThisProfile("Detailed", options =>
{
    options.LogMethodArguments = true;
    options.LogMethodDuration = true;
});
builder.Services.AddLogThisProfile("ExternalCall", new LogThisConfiguration
{
    LogMethodDuration = true
});
```

A callback is applied after a matching JSON profile; without matching JSON, it
starts from library defaults. Passing a `LogThisConfiguration` instance supplies
the complete profile instead. Names are trimmed and matched case-insensitively.
Empty names and duplicate code definitions are rejected.

Select a profile on the class or method:

```csharp
[LogThis(Profile = "Detailed")]
public class OrderService
{
    public void Submit() { } // Detailed

    [LogThis]
    public void CheckStatus() { } // Default

    [LogThis(Profile = "ExternalCall")]
    public void Send() { } // ExternalCall

    [DontLogThis]
    public void Ignore() { } // No generated events
}
```

A method-level `[LogThis]` overrides the class-level selection. An explicit
method attribute without `Profile` selects the default configuration.
`[DontLogThis]` always takes precedence, and profile selections are never
merged.

The selected profile controls events, levels, duration, labels, captured values,
masking, custom components, and diagnostics. `[MaskThisInLogs]` without an
explicit replacement uses that profile's `JsonMaskValue`.

Profiles are validated when the scope factory is first resolved. An unknown
profile throws `InvalidOperationException` when its method is called inside an
active LogThis scope; LogThis does not silently fall back to the default.

### Options

| Option | Default | Effect |
| --- | --- | --- |
| `DebugLogThis` | `false` | Enables best-effort console diagnostics for internal LogThis failures. |
| `JsonFieldsToMask` | Empty | Lists JSON property names to redact. |
| `JsonMaskValue` | `"*****"` | Sets the replacement for redacted values. |
| `LogClassName` | `false` | Adds the declaring class as `{LogThisClass}`. |
| `LogComponentLabels` | `true` | Adds descriptive labels such as `Class:` and `Method:` to rendered components. |
| `LogCorrelationId` | `true` | Lazily obtains the chain's ID for eligible events and adds `{LogThisCorrelationId}`. |
| `LogMethodName` | `true` | Adds the method as `{LogThisMethod}`. |
| `LogMethodArguments` | `false` | Adds `{LogThisArguments}` to entry and exception events. |
| `LogMethodReturnValue` | `false` | Adds `{LogThisReturnValue}` to exit events and an empty value to exception events. |
| `LogMethodDuration` | `false` | Adds numeric `{LogThisDurationMs}` to existing exit and exception events. |
| `LogSpanId` | `false` | Includes an existing W3C activity span ID as `{LogThisSpanId}`. |
| `LogTraceId` | `false` | Includes an existing W3C activity trace ID as `{LogThisTraceId}`. |
| `MessageDelimeter` | Space, pipe, space | Separates placeholders in the message template. |
| `MessageComponents` | Empty | Adds custom structured properties to every event. |

`OnEntryConfig`, `OnExceptionConfig`, and `OnExitConfig` each provide:

- `LogAccessPoint` to enable or disable that event
- `LogLevel` to select its severity
- `AccessPointMessage` to change its message

### Component labels and access-point messages

`LogComponentLabels` defaults to `true`. Set `options.LogComponentLabels = false`
in the registration callback, or `"LogComponentLabels": false` in the `LogThis`
configuration section, to omit descriptive component labels. It controls labels
such as `Class:`, `Method:`, `Arguments:`, `Return value:`, `Duration ms:`,
`Correlation ID:`, `Trace ID:`, and `Span ID:`.

`LogComponentLabels` does not control the access-point message at the beginning
of an event. Entry, exit, and exception messages remain independently
configurable through each event's `AccessPointMessage`; `Entered`, `Exited`,
and `Exception` are only their defaults.

For an exit event with class and method enabled, correlation disabled, and the
default exit message:

```text
Enabled:  Exited | Class: OrderService | Method: SubmitAsync
Disabled: Exited | OrderService | SubmitAsync
```

Disabling labels preserves component order, delimiters, structured property
names and values, and event selection. Custom components do not receive an
automatic label. Whole-event formatting, including JSON output, remains the
logging provider's responsibility.

### Custom component validation and lifetime

Custom component names must not conflict with these library-owned structured
properties, regardless of case or whether the related feature is enabled:

- `LogThisOnEntry`, `LogThisOnExit`, `LogThisOnException`
- `LogThisClass`, `LogThisMethod`, `LogThisArguments`, `LogThisReturnValue`
- `LogThisDurationMs`, `LogThisCorrelationId`
- `LogThisTraceId`, `LogThisSpanId`

Other names, including names such as `LogThisCustom`, `Method`, and
`CorrelationId`, are allowed. Bare names and single brace-wrapped placeholders
are normalized. Reserved, duplicate, or malformed names throw an
`ArgumentException` during addition or configuration resolution.

Configure custom components before the first LogThis scope is created. The scope
factory takes a shallow snapshot of the dictionary, so later additions, removals,
or replacements do not affect that service provider's events.

### Correlation IDs

Correlation is enabled by default. LogThis generates a 16-character,
case-sensitive ID when the first enabled event in an operation needs it and adds
the value to that event as `LogThisCorrelationId`.

Set `options.LogCorrelationId = false` in the configuration callback, or
`"LogCorrelationId": false` in the default or named profile configuration, to
omit correlation from those events. Disabled or filtered events do not trigger
ID generation.

Nested scopes, profile selections, and async continuations share the same
correlation state. Once generated, enabled events in that operation reuse the
same ID; independent outermost scopes receive independent IDs.

Only LogThis-generated events are enriched. Explicit application logs are
unaffected. LogThis does not read incoming headers or derive its correlation ID
from `Activity.TraceId`. ASP.NET Core middleware opens one scope per request;
workers should open one scope per job or message when separate correlation
chains are required.

### Existing trace and span IDs

Enable either or both settings to include existing `System.Diagnostics.Activity`
context in LogThis events:

```csharp
builder.Services.AddLogThisConfiguration(options =>
{
    options.LogTraceId = true;
    options.LogSpanId = true;
});
```

The same settings work in JSON and named profiles:

```json
{
  "LogThis": {
    "LogTraceId": true,
    "LogSpanId": true
  }
}
```

Both default to `false` and are independent: a provider or application may already
supply one identifier. Trace IDs group a distributed operation; span IDs identify
individual operations within it. Ordinary method calls do not automatically
create spans. These settings only include existing context; they do not create
tracing, start or stop activities, or change sampling or exporters.

For each eligible event, LogThis reads `Activity.Current` once and uses that
activity for both fields. The selected profile determines which fields appear.
`LogThisTraceId` contains the complete 32-character hexadecimal trace ID, and
`LogThisSpanId` contains the complete 16-character hexadecimal span ID. With
component labels enabled, their labels are `Trace ID:` and `Span ID:`; disabling
labels leaves the same structured names and values. Both names are reserved for
LogThis, even when disabled.

Missing, non-W3C, or invalid identifiers are omitted without replacement IDs or
absence diagnostics. Activity context follows its normal async flow. If application
code creates a nested activity, events use its current span and return to the
parent's span after that activity ends. LogThis correlation IDs remain independent
and retain their existing lazy generation and operation-chain lifetime.

Method exclusions leave activity context intact:

```csharp
[LogThis]
public class OrderService
{
    [DontLogThis]
    public void Submit()
    {
        Validate();
    }

    public void Validate() { }
}
```

`Submit` emits no generated events. `Validate` still emits its configured events
with the existing activity identifiers when enabled. Neither method creates a
span through LogThis. Explicit application logs are not enriched by this feature.

Custom `ILogThisConfiguration` implementations must add `LogTraceId` and
`LogSpanId`. Existing applications using `LogThisConfiguration` keep their current
output unless these settings are enabled. Context must already be supplied by the
application or its instrumentation; no tracing service is required by LogThis.

### Optional method duration

Set `LogMethodDuration = true` in code, or `"LogMethodDuration": true` in the
`LogThis` configuration section, to add numeric `LogThisDurationMs` to enabled
exit and exception events. It is disabled by default. Entry events have no
duration, and enabling timing does not enable a disabled event or change its
severity.

Duration is elapsed milliseconds, including asynchronous waits and child calls,
measured after entry logging and before completion-event logging. The rendered
component is similar to `Duration ms: 12.34`, while the structured value remains
a number. For deferred sequences, duration covers the method call rather than
later consumer processing.

## Behavior and limitations

- LogThis checks whether the configured level is enabled before formatting a
  message or serializing arguments and return values. The woven method still
  creates its context and captures its arguments when called.
- `DebugLogThis` reports internal logging failures through a separate console
  logger. Those diagnostics do not use the application's configured provider,
  and failures in the diagnostic itself are suppressed.
- The fallback Metalama template buffers iterator methods. Avoid applying
  `[LogThis]` to long-running or unbounded iterators until iterator-specific
  templates are available.
- An exception event can include an empty return-value component, but a failed
  method has no result to log.

## Building from source

Restore and build both packages from the repository root:

```powershell
dotnet restore LogThis.NET.csproj
dotnet build LogThis.NET.csproj
dotnet build LogThis.NET.AspNetCore/LogThis.NET.AspNetCore.csproj
```

The builds emit XML API-documentation files alongside their assemblies.

## License

LogThis.NET is licensed under the terms of the
[MIT License](https://github.com/david-m-leonhardt/LogThis.NET/blob/main/LICENSE).
