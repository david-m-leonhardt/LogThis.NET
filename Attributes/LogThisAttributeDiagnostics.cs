using LogThis.Constants;
using Metalama.Framework.Code;
using Metalama.Framework.Diagnostics;
using Metalama.Framework.Fabrics;

namespace LogThis.Attributes;

/// <summary>Reports annotation conflicts in consuming compilations, independently of method weaving.</summary>
internal sealed class LogThisAttributeDiagnostics : TransitiveProjectFabric
{
    #region Public Methods

    /// <summary>Queries annotations directly so excluded and uninstrumented declarations are also checked.</summary>
    /// <param name="amender">The consuming compilation's compile-time query entry point.</param>
    public override void AmendProject(IProjectAmender amender)
    {
        IQuery<IDeclaration> excluded = amender.SelectDeclarationsWithAttribute(typeof(DontLogThisAttribute));

        excluded.Where(d => d is IMethod && Has(d, typeof(LogThisAttribute)))
            .ReportDiagnostic(_ => LogThisDiagnostics.ExcludedLogging);

        excluded.Where(d => d is IMethod && Has(d, typeof(MaskReturnInLogsAttribute)))
            .ReportDiagnostic(_ => LogThisDiagnostics.ExcludedReturnMask);

        excluded.Where(d => Has(d, typeof(MaskThisInLogsAttribute)))
            .ReportDiagnostic(_ => LogThisDiagnostics.ExcludedDataMask);

        amender.SelectDeclarationsWithAttribute(typeof(LogThisAttribute))
            .Where(d => d is IMethod method && IsRedundant(method))
            .ReportDiagnostic(_ => LogThisDiagnostics.RedundantProfile);
    }

    #endregion

    #region Private Methods

    /// <summary>Checks annotations on the selected declaration without traversing an override chain.</summary>
    private static bool Has(IDeclaration declaration, Type attributeType)
        => declaration.Attributes.OfAttributeType(attributeType).Any();

    /// <summary>Matches class coverage and profile-name resolution, without consulting runtime configuration.</summary>
    private static bool IsRedundant(IMethod method)
    {
        if (method.Accessibility != Accessibility.Public ||
            method.IsAbstract ||
            Has(method, typeof(DontLogThisAttribute)))
        {
            return false;
        }

        IAttribute? classAttribute = method.DeclaringType.Attributes.OfAttributeType(typeof(LogThisAttribute)).SingleOrDefault();
        if (classAttribute == null)
        {
            return false;
        }

        IAttribute methodAttribute = method.Attributes.OfAttributeType(typeof(LogThisAttribute)).Single();

        string? classProfile = GetProfile(classAttribute);
        string? methodProfile = GetProfile(methodAttribute);

        // Whitespace-only names are invalid at runtime, not aliases for the default selection.
        if (classProfile != null && string.IsNullOrWhiteSpace(classProfile) ||
            methodProfile != null && string.IsNullOrWhiteSpace(methodProfile))
        {
            return false;
        }

        return string.Equals(classProfile?.Trim(), methodProfile?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads profile-selection metadata; an omitted or null value selects the default profile.</summary>
    private static string? GetProfile(IAttribute attribute)
        => attribute.NamedArguments.TryGetValue(nameof(LogThisAttribute.Profile), out TypedConstant profile)
            ? (string?)profile.Value : null;

    #endregion
}
