# LogThis.NET

LogThis.NET is an in-development .NET library for adding configurable
method-boundary logging with attributes. It uses the standard
`Microsoft.Extensions.Logging` abstractions and
`MethodBoundaryAspect.Fody` for compile-time method interception.

> [!IMPORTANT]
> This project is under active development. The API may change, documentation
> may lag behind the implementation, and some features are incomplete. The
> ASP.NET Core requests are scoped automatically. Console applications and
> background services establish an explicit logging scope.

## Current requirements

- .NET 10 SDK
- `MethodBoundaryAspect.Fody` configured in the consuming project

The repository currently builds the library from source; package-distribution
instructions will be added when a package is published.

## Usage

Reference `LogThis.NET.csproj` from your application while developing locally:

```xml
<ItemGroup>
  <ProjectReference Include="..\LogThis\LogThis.NET.csproj" />
  <PackageReference Include="MethodBoundaryAspect.Fody" Version="2.0.150" />
</ItemGroup>
```

Enable the Fody weaver in the consuming project by adding `FodyWeavers.xml`:

```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <MethodBoundaryAspect />
</Weavers>
```

Register LogThis.NET and initialize it during application startup:

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
exceptions. A class-level attribute applies to its methods:

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

By default, each log includes the access-point message and method name:

- entry at `Information` with the message `Entered`
- exit at `Information` with the message `Exited`
- exceptions at `Error` with the message `Exception` and exception details
- components separated by ` | `

Class names, arguments, return values, and custom components are disabled by
default.

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
- `LogMethodReturnValue` adds `{ReturnValue}` to exit and exception logs.
- `MessageComponents` adds application-specific properties to every log.
- `LogOnEntry`, `LogOnException`, and `LogOnExit` independently enable each
  method access point.

`DebugLogThis` writes a `Debug` log if LogThis.NET itself encounters an error
while constructing a log message. The library catches that internal error so it
does not replace the application method's behavior.

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
they are logged. Names in `JsonFieldsToMask` are replaced with `JsonMaskValue`:

```csharp
builder.Services.AddLogThisConfiguration(
    JsonFieldsToMask: ["Password", "AuthToken"],
    JsonMaskValue: "*****",
    LogMethodArguments: true,
    LogMethodReturnValue: true,
    MessageComponents: new Dictionary<string, object>());
```

Only enable argument and return-value logging when appropriate for the data and
volume handled by the application. Keep the masking list current; unlisted
sensitive fields are not masked automatically.

## Development

From the repository root, restore and build the library with:

```powershell
dotnet restore LogThis.NET.csproj
dotnet build LogThis.NET.csproj
```

The `LogThis.ApiTester` and `LogThis.consoleTester` projects are development
harnesses in sibling directories rather than supported packages.

## License

This project is licensed under the terms in [LICENSE](LICENSE).
