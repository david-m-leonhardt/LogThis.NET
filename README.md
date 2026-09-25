# LogThis.NET

LogThis.NET is an in-development .NET library for configurable method-boundary
logging with `[LogThis]`. It uses `Microsoft.Extensions.Logging` and Metalama's
compile-time method interception; applications choose their own logging provider.

> [!IMPORTANT]
> This project is under active development. The API may change, and some
> features are incomplete. ASP.NET Core requests are scoped
> after you add the LogThis middleware. Console applications and background
> services establish an explicit logging scope.

## Current requirements

- .NET 10 SDK
- `Metalama.Framework` (referenced transitively through the library)

The repository builds two libraries: `LogThis.NET` for console applications and
background services, and `LogThis.NET.AspNetCore` for ASP.NET Core applications.
The ASP.NET Core package also installs the core package. Console and background
applications using only `LogThis.NET` do not need the ASP.NET Core runtime.

## Installation

Install the prerelease package appropriate for your application:

```powershell
dotnet add package LogThis.NET --version 0.1.0-beta.1
# ASP.NET Core applications install this package instead:
dotnet add package LogThis.NET.AspNetCore --version 0.1.0-beta.1
```

`LogThis.NET.AspNetCore` brings in `LogThis.NET` automatically. The packages
target .NET 10, and applications using `[LogThis]` need the .NET 10 SDK when
they are built so Metalama can weave the logging code.

## Usage

Register LogThis.NET and add its request middleware during application startup:

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

`UseLogThis` adds middleware that establishes a separate logging scope for each
request. Place it before endpoints that call methods marked with `[LogThis]`.

For console applications and background services, register an `IConfiguration`
and call `services.AddLogThisConfiguration()`. LogThis reads the `LogThis` section
by name when its configuration is first resolved. A plain console application
can load `appsettings.json` like this (with the
`Microsoft.Extensions.Configuration.Json` package and the file copied to output):

```csharp
using LogThis.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

ServiceCollection services = new();
services.AddSingleton(configuration);
services.AddLogThisConfiguration();
```

Then resolve `ILogThisScopeFactory` from the service provider. Keep its scope
active while calling marked methods:

```csharp
using LogThis.Interfaces;
using Microsoft.Extensions.DependencyInjection;

ILogThisScopeFactory scopeFactory =
    serviceProvider.GetRequiredService<ILogThisScopeFactory>();

using (scopeFactory.BeginScope("MyApplication.Worker"))
{
    await RunApplicationAsync();
}
```

The scope flows across `await`. A background service can inject the factory and
open a scope in `ExecuteAsync`. The same operation is available as
`serviceProvider.UseLogThis()` after importing `LogThis.Middleware`. Marked
methods called without an active scope produce no LogThis logs.

Apply `[LogThis]` to a class or method to log entry, exit, and unhandled
exceptions. A class-level attribute applies to its public concrete methods:

```csharp
using LogThis.Attributes;

[LogThis]
public class OrdersService
{
    public void SubmitOrder()
    {
        // Method implementation
    }
}
```

`[LogThis]` logs exit after an awaited method completes and logs exceptions
thrown during that await. The fallback Metalama template buffers iterator
methods; do not apply it to long or unbounded iterators without adding
iterator-specific templates.

By default, each log includes the access-point message and method name:

- entry at `Information` with the message `Entered`
- exit at `Information` with the message `Exited`
- exceptions at `Error` with the message `Exception` and exception details
- components separated by ` | `

Class names, arguments, and return values are omitted by default. No custom
components are registered by default.

### Custom configuration

Use a callback to override defaults in code. When a `LogThis` section is also
available, its values are bound first and the callback takes precedence:

```csharp
using LogThis.Middleware;
using Microsoft.Extensions.Logging;

builder.Services.AddLogThisConfiguration(
    options =>
    {
        options.DebugLogThis = true;
        options.JsonFieldsToMask = ["Password", "AuthToken"];
        options.JsonMaskValue = "[REDACTED]";
        options.LogClassName = true;
        options.LogMethodArguments = true;
        options.LogMethodReturnValue = true;
        options.OnEntryConfig.LogLevel = LogLevel.Debug;
        options.OnEntryConfig.AccessPointMessage = "Starting";
        options.OnExceptionConfig.AccessPointMessage = "Failed";
        options.OnExitConfig.LogLevel = LogLevel.Debug;
        options.OnExitConfig.AccessPointMessage = "Finished";
        options.AddMessageComponents(new Dictionary<string, object>
        {
            ["Application"] = "Ordering"
        });
    });
```

Alternatively, bind a `LogThis` section from the application's configuration:

```json
{
  "LogThis": {
    "JsonFieldsToMask": ["Password", "AuthToken"],
    "JsonMaskValue": "[REDACTED]",
    "LogMethodArguments": true,
    "LogMethodReturnValue": true,
    "MessageComponents": {
      "Application": "Ordering"
    },
    "OnEntryConfig": {
      "LogLevel": "Debug",
      "AccessPointMessage": "Starting"
    },
    "OnExceptionConfig": {
      "LogLevel": "Error"
    }
  }
}
```

```csharp
builder.Services.AddLogThisConfiguration(); // ASP.NET Core
// or: services.AddLogThisConfiguration(); // IConfiguration registered in DI
```

This works with ASP.NET Core's `WebApplicationBuilder` and with a generic host
for a console application or background service. A plain `ServiceCollection`
caller can register an `IConfiguration` as shown above. The
section uses the property names of `LogThisConfiguration`; event settings live
under `OnEntryConfig`, `OnExceptionConfig`, and `OnExitConfig`. Omitted values keep their
defaults, and bound `MessageComponents` keys are normalized into logging
placeholders. Unknown keys inside a present `LogThis` section cause an error when
the LogThis configuration is first resolved. If `IConfiguration` is not
registered or the whole `LogThis` section is absent, LogThis uses its defaults;
therefore a misspelled section name cannot be distinguished from an intentionally
absent section. Configuration is read once when first resolved; file reloads do
not update an active LogThis service provider.

The component switches control the structured properties added to a message:

- `LogClassName` adds `{Class}`.
- `LogMethodName` adds `{Method}`.
- `LogMethodArguments` adds `{Arguments}` to entry and exception logs.
- `LogMethodReturnValue` adds `{ReturnValue}` to exit logs. It also adds an
  empty return-value component to exception logs; a failed method has no result.
- `MessageComponents` adds application-specific properties to every log.
- `OnEntryConfig.LogAccessPoint`, `OnExceptionConfig.LogAccessPoint`, and
  `OnExitConfig.LogAccessPoint` independently enable each method access point.

LogThis checks the configured provider's level for each access point before
formatting the message or serializing its arguments or return value. The aspect
still creates its method context and captures the argument array when the
method is called, even if that log level is filtered out.

`DebugLogThis` writes a `Debug` diagnostic to the console if message
construction or the configured provider fails. This diagnostic uses a separate
console logger: it does not go to the application's configured log4net, Serilog,
NLog, or other logging provider. The console diagnostic is best-effort; its own
failure is suppressed so it cannot replace the application's result or exception.

You can also construct `LogThisConfiguration`, modify its access-point
configurations, and register it directly:

```csharp
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.Logging;

LogThisConfiguration logThisConfiguration = new()
{
    LogClassName = true,
    LogMethodArguments = true,
    LogMethodReturnValue = true
};

logThisConfiguration.OnEntryConfig.LogLevel = LogLevel.Debug;
logThisConfiguration.OnExceptionConfig.LogLevel = LogLevel.Critical;
logThisConfiguration.OnExitConfig.LogAccessPoint = false;

logThisConfiguration.AddMessageComponents(new Dictionary<string, object>
{
    ["Application"] = "Ordering"
});

builder.Services.AddLogThisConfiguration(logThisConfiguration);
```

Pass a category name to `UseLogThis` if the default `LogThis` logging category
is not suitable:

```csharp
app.UseLogThis("MyApplication.Methods");
```

### Masking sensitive fields

When argument or return-value logging is enabled, values are serialized before
they are logged. Every property whose name matches `JsonFieldsToMask`
(case-insensitively) is replaced with `JsonMaskValue`, regardless of its depth:

```csharp
builder.Services.AddLogThisConfiguration(
    options =>
    {
        options.JsonFieldsToMask = ["Password", "AuthToken"];
        options.JsonMaskValue = "*****";
        options.LogMethodArguments = true;
        options.LogMethodReturnValue = true;
    });
```

Arguments are serialized one value at a time. Objects inside arrays, including
nested arrays, are masked using the same rule. A matched property's entire value
is replaced, whether that value is a scalar, object, or array. Only property
names are supported; dotted paths, wildcards, and partial-value masking are not
available. The original objects are not changed. Do not rely on this list alone
to protect sensitive data: unlisted fields are not masked automatically. Enable
argument and return-value logging only when appropriate for the data and volume
handled by the application.

## Development

To work from source instead of using NuGet packages, reference the core project
from a console or background application:

```xml
<ItemGroup>
  <ProjectReference Include="..\LogThis\LogThis.NET.csproj" />
</ItemGroup>
```

For an ASP.NET Core application, reference the integration project instead; it
also brings in the core library:

```xml
<ItemGroup>
  <ProjectReference Include="..\LogThis\LogThis.NET.AspNetCore\LogThis.NET.AspNetCore.csproj" />
</ItemGroup>
```

From the repository root, restore and build both libraries with:

```powershell
dotnet restore LogThis.NET.csproj
dotnet build LogThis.NET.csproj
dotnet build LogThis.NET.AspNetCore/LogThis.NET.AspNetCore.csproj
```

The build emits `LogThis.NET.xml` alongside the library DLL for API-documentation
tools. Release setup and the manual publishing workflow are documented in
[PUBLISHING.md](https://github.com/david-m-leonhardt/LogThis.NET/blob/main/PUBLISHING.md).

The `LogThis.ApiTester` and `LogThis.consoleTester` projects are development
harnesses in sibling directories rather than supported packages.

## License

This project is licensed under the terms in
[LICENSE](https://github.com/david-m-leonhardt/LogThis.NET/blob/main/LICENSE).
