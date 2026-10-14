using Metalama.Framework.Code;
using Metalama.Framework.Diagnostics;
using Metalama.Framework.Fabrics;

namespace LogThis.Attributes;

/// <summary>Reports annotation conflicts in consuming compilations, independently of method weaving.</summary>
internal sealed class LogThisAttributeDiagnostics : TransitiveProjectFabric
{
    #region Private Fields

    private static readonly DiagnosticDefinition ExcludedLogging = new("LOGTHIS003", Severity.Warning,
        "DontLogThis excludes this method, so LogThis has no effect. Remove LogThis or remove the exclusion.");
    private static readonly DiagnosticDefinition ExcludedReturnMask = new("LOGTHIS004", Severity.Warning,
        "DontLogThis excludes this method, so MaskReturnInLogs has no effect. Remove the mask or remove the exclusion.");
    private static readonly DiagnosticDefinition ExcludedDataMask = new("LOGTHIS005", Severity.Warning,
        "DontLogThis omits this declaration, so MaskThisInLogs has no effect. Remove the mask or remove the exclusion.");
    private static readonly DiagnosticDefinition RedundantProfile = new("LOGTHIS006", Severity.Warning,
        "This method selects the same LogThis profile as its containing class, which already covers it. Remove the method's LogThis attribute or select a different profile.");

    #endregion

    #region Public Methods

    /// <summary>Queries annotations directly so excluded and uninstrumented declarations are also checked.</summary>
    /// <param name="amender">The consuming compilation's compile-time query entry point.</param>
    public override void AmendProject(IProjectAmender amender)
    {
        var excluded = amender.SelectDeclarationsWithAttribute(typeof(DontLogThisAttribute));
        excluded.Where(d => d is IMethod && Has(d, typeof(LogThisAttribute)))
            .ReportDiagnostic(_ => ExcludedLogging);
        excluded.Where(d => d is IMethod && Has(d, typeof(MaskReturnInLogsAttribute)))
            .ReportDiagnostic(_ => ExcludedReturnMask);
        excluded.Where(d => Has(d, typeof(MaskThisInLogsAttribute)))
            .ReportDiagnostic(_ => ExcludedDataMask);

        amender.SelectDeclarationsWithAttribute(typeof(LogThisAttribute))
            .Where(d => d is IMethod method && IsRedundant(method))
            .ReportDiagnostic(_ => RedundantProfile);
    }

    #endregion

    #region Private Methods

    private static bool Has(IDeclaration declaration, Type attributeType)
        => declaration.Attributes.OfAttributeType(attributeType).Any();

    /// <summary>Matches class coverage and profile-name resolution, without consulting runtime configuration.</summary>
    private static bool IsRedundant(IMethod method)
    {
        if (method.Accessibility != Accessibility.Public || method.IsAbstract ||
            Has(method, typeof(DontLogThisAttribute))) return false;
        var classAttribute = method.DeclaringType.Attributes.OfAttributeType(typeof(LogThisAttribute)).SingleOrDefault();
        if (classAttribute == null) return false;
        var methodAttribute = method.Attributes.OfAttributeType(typeof(LogThisAttribute)).Single();
        string? classProfile = GetProfile(classAttribute);
        string? methodProfile = GetProfile(methodAttribute);
        // Whitespace-only names are invalid at runtime, not aliases for the default selection.
        if (classProfile != null && string.IsNullOrWhiteSpace(classProfile) ||
            methodProfile != null && string.IsNullOrWhiteSpace(methodProfile)) return false;
        return string.Equals(classProfile?.Trim(), methodProfile?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProfile(IAttribute attribute)
        => attribute.NamedArguments.TryGetValue(nameof(LogThisAttribute.Profile), out var profile)
            ? (string?)profile.Value : null;

    #endregion
}
