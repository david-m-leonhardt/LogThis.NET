namespace LogThis.Attributes;

/// <summary>Excludes a method from LogThis instrumentation or a parameter or property from captured data.</summary>
/// <remarks>
/// Explicit logging calls and instrumentation of called methods are unaffected.
/// A method exclusion takes precedence over method-level and class-level LogThis attributes.
/// Apply method exclusions separately to each overload or override that should be excluded.
/// Parameter values are omitted from argument capture. Properties are skipped before
/// serialization without invoking their getters. Included data still follows configured masking rules.
/// Data exclusions do not enable capture and do not sanitize explicit logs or exception messages.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property,
    AllowMultiple = false, Inherited = false)]
public sealed class DontLogThisAttribute : Attribute;
