# LogThis.NET

LogThis.NET adds configurable method-entry, method-exit, and exception logging
through the `[LogThis]` attribute. It uses `Microsoft.Extensions.Logging`, so
applications keep control of their logging provider, and Metalama to weave the
logging code at build time.

> [!IMPORTANT]
> This branch targets the upcoming 1.1.0-beta.1 prerelease, including `[DontLogThis]`.
> LogThis.NET 1.0.0 is the first stable release.

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

## Applying `[LogThis]`

Apply the attribute to one method or to a class. A class-level attribute wraps
each public, non-abstract method:

```csharp
using LogThis.Attributes;

[LogThis]
public class OrdersService
{
    public async Task<Order> SubmitOrderAsync(OrderRequest request)
    {
        // Method implementation
    }
}
```

Awaitable methods log their exit after completion and log exceptions thrown
during the await.

The default events are:

| Event | Level | Message |
| --- | --- | --- |
| Entry | `Information` | `Entered` |
| Exit | `Information` | `Exited` |
| Exception | `Error` | `Exception` |

The method name is included by default, and message components are separated by
` | `. Class names, arguments, return values, and custom properties are omitted
until enabled.

### Excluding a method with `[DontLogThis]`

Use `[DontLogThis]` to prevent LogThis from intercepting a particular method:

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

LogThis generates no entry, exit, or exception events for `SubmitOrder`.
Explicit `ILogger` calls are unaffected by either attribute and retain their
normal provider and log-level filtering. Called methods such as `ValidateOrder`
follow their own instrumentation rules; no ambient logging state is disabled.
Return values and exception propagation are unchanged, including for async methods.
An instrumented caller can still log its own failure when an excluded method throws.

The exclusion takes precedence even when `[LogThis]` is also applied directly to
the method. It is harmless on a method that would not otherwise be instrumented.
It applies only to the annotated method: annotate other overloads, overrides,
or interface implementations separately. Class and constructor exclusions are
not supported.

### Excluding parameters and properties

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

With `LogMethodArguments` enabled, `apiKey` is omitted entirely, with no name,
value, or masking placeholder. Included arguments are logged as a JSON object
of parameter names and values in declaration order. If all parameters are
excluded, arguments are logged as `{}` and configured events still occur.

`PaymentToken` is omitted whenever LogThis captures an `OrderRequest`, including
arguments, return values, nested objects, and objects in collections. Excluded
properties are skipped before serialization, so logging does not invoke their
getters. Included data retains the existing JSON serialization and masking rules;
exclusion means omission, not replacement with `JsonMaskValue`. Application
objects, method results, and exceptions are not modified.

Exclusions are independent: an excluded parameter can still be captured as a
return value or by a called method, with property exclusions applied in each
capture. An excluded method does not disable property exclusions in other methods.

Configuration determines which entry, exit, and exception events occur and
whether arguments or return values are captured. Exclusions only remove eligible
methods or data; they never enable capture. Parameter and property exclusions
have no effect when the corresponding data capture is disabled. They remain
valid without build warnings because configuration is resolved at runtime and
capture can be enabled later.

These exclusions govern data captured by LogThis. They do **not** sanitize explicit
application logs or exception-message text. An application can still explicitly
log an excluded value, and an exception message can still contain that value.

### Named argument output and compatibility

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

This prerelease changes newly woven argument output from a positional array to
a named JSON object. Update log parsers that depend on the old format and rebuild
consumer applications to capture names. The structured `{Arguments}` field still
contains the formatted `Arguments: ...` string; it is not a new set of logger fields.
Renaming a source parameter changes its logged JSON key after rebuilding.

The public `LogThisMethodContext(string, string, object?[])` constructor and
`Arguments` property remain available. Existing compiled instrumentation and
manual contexts using that constructor keep positional output because they have
no parameter names. New instrumentation uses `FromNamedArguments` and the
`NamedArguments` property; `Arguments` also exposes the corresponding values for
compatibility. Use the new factory for manual contexts that need named output.

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

### Options

| Option | Default | Effect |
| --- | --- | --- |
| `LogClassName` | `false` | Adds the declaring class as `{Class}`. |
| `LogMethodName` | `true` | Adds the method as `{Method}`. |
| `LogMethodArguments` | `false` | Adds `{Arguments}` to entry and exception events. |
| `LogMethodReturnValue` | `false` | Adds `{ReturnValue}` to exit events and an empty value to exception events. |
| `MessageDelimeter` | Space, pipe, space | Separates placeholders in the message template. |
| `MessageComponents` | Empty | Adds custom structured properties to every event. |
| `JsonFieldsToMask` | Empty | Lists JSON property names to redact. |
| `JsonMaskValue` | `"*****"` | Sets the replacement for redacted values. |
| `DebugLogThis` | `false` | Enables best-effort console diagnostics for internal LogThis failures. |

`OnEntryConfig`, `OnExceptionConfig`, and `OnExitConfig` each provide:

- `LogAccessPoint` to enable or disable that event
- `LogLevel` to select its severity
- `AccessPointMessage` to change its message

## Sensitive-data masking

When argument or return-value logging is enabled, LogThis serializes those
values. Every property whose name matches `JsonFieldsToMask`, ignoring case, is
replaced with `JsonMaskValue` at any nesting depth:

```csharp
builder.Services.AddLogThisConfiguration(options =>
{
    options.JsonFieldsToMask = ["Password", "AuthToken"];
    options.JsonMaskValue = "*****";
    options.LogMethodArguments = true;
    options.LogMethodReturnValue = true;
});
```

The rule also applies to objects in nested arrays, and a match replaces the
property's entire value. Dotted paths, wildcards, and partial-value masking are
not supported. Original objects are not modified.

Do not rely on masking alone to protect sensitive information: fields not on
the list are logged unchanged. Enable argument and return-value logging only
when it is appropriate for the application's data and volume.

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
