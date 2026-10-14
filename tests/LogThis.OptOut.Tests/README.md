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

Duration checks verify default/disabled behavior, numeric completion fields,
awaited Task and ValueTask results, original exceptions, event and level
suppression, method exclusions, provider failures, and configuration binding.
An application-controlled task gate verifies awaited completion without sleeps
or fixed timing thresholds.

Correlation checks cover default format, lazy generation, disabled-to-enabled
transitions, nested inheritance, awaits, concurrent first use and operation isolation,
context restoration, event enrichment, explicit-log isolation, generation failures,
configuration binding, filtered events, failed capture, and unchanged library-owned property names.

Component checks cover default and disabled labels, raw structured names and
values, reserved-name validation across registration paths, case/brace/format
normalization, disabled-feature reservations, allowed custom names, and the
post-validation component snapshot policy.

Masking checks cover MaskThisInLogs replacements, exclusion/configuration
precedence, skipped throwing getters, whole object/collection replacement, null
and empty values, nested models, dictionary/JSON fallback, configuration isolation,
disabled capture, original object identity, and exception propagation.

Profile checks cover independent defaults, JSON/code layering and replacement,
duplicate and invalid definitions, class/method selection, explicit default
selection, single instrumentation, missing references, per-profile masks and
custom fields, async/concurrent isolation, and scope-owned correlation identity.

Activity checks use controlled activities without tracing services. They cover
all trace/span setting combinations, labels and complete structured values,
default/named configuration, missing and hierarchical context, awaits, nested
activities, concurrent contexts, method exclusions, filters, and provider failures.
The reservation matrix includes both activity property names.

Parameter masking checks cover configured/profile and literal masks, names/order,
null and empty values, whole objects and collections without inspection,
exclusion precedence, entry/exception capture, disabled events, ref/out values,
and masks not following values into returns or child calls.

Property inheritance checks cover unchanged inherited declarations, annotated
and unannotated overrides, new hiding properties, concrete overrides of abstract
properties, selected-profile replacements, skipped throwing getters, and
exclusion/configured-name masking on overrides.

Return masking checks cover configured/profile and literal replacements, empty
and null results, untouched objects and collections, awaited completion,
ValueTask results, exclusions, unannotated overrides, disabled capture/events,
level filtering, and unchanged exception propagation. Build-only diagnostic
fixtures can be enabled with `-p:ReturnMaskDiagnosticMode=DIAGNOSTICS` (three
`LOGTHIS002` warnings) or `-p:ReturnMaskDiagnosticMode=NULL` (two `LOGTHIS001`
errors) when building this project. Run the normal suite afterward without these
symbols. Direct constructor null validation is included in the runtime checks.
