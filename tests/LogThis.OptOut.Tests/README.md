# Opt-out integration checks

Run from the repository root:

```powershell
dotnet run --project tests/LogThis.OptOut.Tests -c Release
```

This executable builds a real Metalama consumer and checks captured log events.
It fails with a nonzero exit code if method exclusion, explicit logging, called
method instrumentation, overload isolation, return values, exception identity,
async behavior, or precedence over a direct `[LogThis]` attribute regresses.

Data exclusion checks cover mixed and fully excluded parameter lists, generic,
ref/out and async methods, nested objects, collections, dictionary values,
argument/return/exception capture, renamed properties, throwing excluded getters,
masking, object and exception identity, explicit logs, and runtime capture/event
configuration. No additional test framework is required.

Named argument checks verify declaration order, name/value alignment with
exclusions at every position, null values, nested exclusions and masking,
entry/exception output, disabled capture, and legacy context compatibility.
