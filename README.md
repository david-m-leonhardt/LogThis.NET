# LogThis.NET

LogThis.NET adds configurable method-entry, method-exit, and exception logging
through the `[LogThis]` attribute. It uses `Microsoft.Extensions.Logging`, so
applications keep control of their logging provider, and Metalama to weave the
logging code at build time.

> [!IMPORTANT]
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
dotnet add package LogThis.NET --version 1.0.0
```

For an ASP.NET Core application:

```powershell
dotnet add package LogThis.NET.AspNetCore --version 1.0.0
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
