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

The repository currently builds the library from source; package-distribution
instructions will be added when a package is published.

## Usage

Reference `LogThis.NET.csproj` from your application while developing locally:

```xml
<ItemGroup>
  <ProjectReference Include="..\LogThis\LogThis.NET.csproj" />
</ItemGroup>
```

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

For console applications and background services, register LogThis with
`AddLogThisConfiguration()`, then resolve `ILogThisScopeFactory` from the service
provider. Keep its scope active while calling marked methods:

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

Pass configuration values when registering the service:

```csharp
using LogThis.Middleware;
using Microsoft.Extensions.Logging;

builder.Services.AddLogThisConfiguration(
    DebugLogThis: true,
    JsonFieldsToMask: ["Password", "AuthToken"],
    JsonMaskValue: "[REDACTED]",
    LogClassName: true,
    LogMethodArguments: true,
    LogMethodName: true,
    LogMethodReturnValue: true,
    LogOnEntry: true,
    LogOnException: true,
    LogOnExit: true,
    MessageComponents: new Dictionary<string, object>
    {
        ["Application"] = "Ordering"
    },
    MessageDelimeter: " | ",
    OnEntryLogLevel: LogLevel.Debug,
    OnEntryMessage: "Starting",
    OnExceptionLogLevel: LogLevel.Error,
    OnExceptionMessage: "Failed",
    OnExitLogLevel: LogLevel.Debug,
    OnExitMessage: "Finished");
```

The component switches control the structured properties added to a message:

- `LogClassName` adds `{Class}`.
- `LogMethodName` adds `{Method}`.
- `LogMethodArguments` adds `{Arguments}` to entry and exception logs.
- `LogMethodReturnValue` adds `{ReturnValue}` to exit logs. It also adds an
  empty return-value component to exception logs; a failed method has no result.
- `MessageComponents` adds application-specific properties to every log.
- `LogOnEntry`, `LogOnException`, and `LogOnExit` independently enable each
  method access point.

`DebugLogThis` attempts to write a `Debug` log if message construction or a
provider call fails. LogThis catches the original logging failure; the
diagnostic provider call itself is not guarded against another failure.

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
they are logged. Matching JSON object fields in `JsonFieldsToMask` are replaced
with `JsonMaskValue`:

```csharp
builder.Services.AddLogThisConfiguration(
    JsonFieldsToMask: ["Password", "AuthToken"],
    JsonMaskValue: "*****",
    LogMethodArguments: true,
    LogMethodReturnValue: true,
    MessageComponents: new Dictionary<string, object>());
```

Arguments are serialized one value at a time. A top-level list returned by a
method is split into items before masking. Other non-object JSON roots are
returned without field masking, and the behavior of the masking dependency on
nested collections has not been verified. Do not rely on this list alone to
protect sensitive data: unlisted fields are not masked automatically. Enable
argument and return-value logging only when appropriate for the data and volume
handled by the application.

## Development

From the repository root, restore and build the library with:

```powershell
dotnet restore LogThis.NET.csproj
dotnet build LogThis.NET.csproj
dotnet run --project tests/LogThis.Smoke/LogThis.Smoke.csproj -c Release
```

The build emits `LogThis.NET.xml` alongside the library DLL for API-documentation
tools. The smoke project checks method-level and class-level interception,
sync and async outcomes, exceptions, and nested runtime scopes.

The `LogThis.ApiTester` and `LogThis.consoleTester` projects are development
harnesses in sibling directories rather than supported packages.

## License

This project is licensed under the terms in [LICENSE](LICENSE).
