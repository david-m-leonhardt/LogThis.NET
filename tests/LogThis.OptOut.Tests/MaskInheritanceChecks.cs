using LogThis.Attributes;
using LogThis.Entities;
using LogThis.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Verifies the declaration selected by Newtonsoft governs property masking.</summary>
public static class MaskInheritanceChecks
{
    #region Public Methods

    /// <summary>Checks inherited declarations, overrides, hiding, abstract members, and configured masks.</summary>
    public static void Run()
    {
        var options = new LogThisConfiguration { JsonMaskValue = "default-mask" };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogThisConfiguration(options);
        services.AddLogThisProfile("Inheritance", o => o.JsonMaskValue = "profile-mask");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.UseLogThis();
        foreach (string? profile in new string?[] { null, "Inheritance" })
        {
            using var selection = LogThisRuntimeLogger.SelectProfile(profile);
            string mask = profile == null ? "default-mask" : "profile-mask";
            JObject inherited = Capture(new UnchangedMaskModel());
            Check((string?)inherited["DefaultMasked"] == mask && (string?)inherited["ExplicitMasked"] == "[BASE]" && (string?)inherited["Hidden"] == mask, "Unchanged inherited declaration lost its annotation.");

            BaseMaskModel unannotated = new UnannotatedMaskModel();
            JObject visible = Capture(unannotated);
            Check((string?)visible["DefaultMasked"] == "override" && (string?)visible["ExplicitMasked"] == "override-explicit" && (string?)visible["Hidden"] == "hidden", "Base annotation masked an unannotated override or hiding property.");
            Check(((UnannotatedMaskModel)unannotated).Reads == 3, "Unannotated declarations were not captured normally.");

            JObject annotated = Capture(new AnnotatedMaskModel());
            Check((string?)annotated["DefaultMasked"] == "[DERIVED]" && (string?)annotated["ExplicitMasked"] == mask && (string?)annotated["Hidden"] == "", "Annotated override/hiding declaration did not use its own replacement.");
            var concrete = new ConcreteMaskModel();
            Check((string?)Capture(concrete)["AbstractSecret"] == "concrete" && concrete.Reads == 1, "Abstract annotation masked concrete override.");
            Check((string?)Capture(new AnnotatedConcreteMaskModel())["AbstractSecret"] == mask, "Annotated concrete override lost selected mask.");
            Check(Capture(new ExcludedMaskModel())["DefaultMasked"] == null, "Exclusion on override did not take precedence.");
        }

        // Unannotated overrides remain eligible for independent configured-name masking.
        options.JsonFieldsToMask = ["defaultmasked", "explicitmasked", "hidden", "abstractsecret"];
        var configured = new UnannotatedMaskModel();
        JObject masked = Capture(configured);
        Check(masked.Properties().All(p => (string?)p.Value == "default-mask") && configured.Reads == 0, "Configured masking did not skip override/hiding getters.");
        var configuredAbstract = new ConcreteMaskModel();
        Check((string?)Capture(configuredAbstract)["AbstractSecret"] == "default-mask" && configuredAbstract.Reads == 0, "Configured abstract-override mask invoked the getter.");
    }

    #endregion

    #region Private Methods

    /// <summary>Uses the actual capture serializer and selected runtime configuration.</summary>
    private static JObject Capture(object value) => JObject.Parse(LogThisValueMasker.MaskObject(value));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    #endregion
}

/// <summary>Throwing getters establish that unchanged inherited annotations skip access.</summary>
public class BaseMaskModel
{
    #region Public Properties

    [MaskThisInLogs] public virtual string DefaultMasked => throw new Exception("Base getter read.");
    [MaskThisInLogs("[BASE]")] public virtual string ExplicitMasked => throw new Exception("Base explicit getter read.");
    [MaskThisInLogs] public string Hidden => throw new Exception("Base hidden getter read.");

    #endregion
}

/// <summary>Inherits property declarations unchanged.</summary>
public sealed class UnchangedMaskModel : BaseMaskModel { }

/// <summary>Overrides and hides properties without redeclaring masking intent.</summary>
public sealed class UnannotatedMaskModel : BaseMaskModel
{
    #region Public Properties

    [JsonIgnore] public int Reads { get; private set; }
    public override string DefaultMasked { get { Reads++; return "override"; } }
    public override string ExplicitMasked { get { Reads++; return "override-explicit"; } }
    public new string Hidden { get { Reads++; return "hidden"; } }

    #endregion
}

/// <summary>Each declaration chooses its own replacement and must not invoke its getter.</summary>
public sealed class AnnotatedMaskModel : BaseMaskModel
{
    #region Public Properties

    [MaskThisInLogs("[DERIVED]")] public override string DefaultMasked => throw new Exception("Annotated override getter read.");
    [MaskThisInLogs] public override string ExplicitMasked => throw new Exception("Default override getter read.");
    [MaskThisInLogs("")] public new string Hidden => throw new Exception("Annotated hiding getter read.");

    #endregion
}

/// <summary>An abstract declaration's annotation does not annotate its implementations.</summary>
public abstract class AbstractMaskModel
{
    #region Public Properties

    [MaskThisInLogs("[ABSTRACT]")] public abstract string AbstractSecret { get; }

    #endregion
}

/// <summary>An unannotated implementation remains eligible for capture.</summary>
public sealed class ConcreteMaskModel : AbstractMaskModel
{
    #region Public Properties

    [JsonIgnore] public int Reads { get; private set; }
    public override string AbstractSecret { get { Reads++; return "concrete"; } }

    #endregion
}

/// <summary>A concrete override can opt into the selected configuration's mask.</summary>
public sealed class AnnotatedConcreteMaskModel : AbstractMaskModel
{
    #region Public Properties

    [MaskThisInLogs] public override string AbstractSecret => throw new Exception("Concrete masked getter read.");

    #endregion
}

/// <summary>An override can independently exclude itself from capture.</summary>
public sealed class ExcludedMaskModel : BaseMaskModel
{
    #region Public Properties

    [DontLogThis, MaskThisInLogs] public override string DefaultMasked => throw new Exception("Excluded override getter read.");

    #endregion
}
