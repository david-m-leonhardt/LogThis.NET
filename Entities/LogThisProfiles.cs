using LogThis.Interfaces;

namespace LogThis.Entities;

/// <summary>Holds complete method configurations and their validated custom-component snapshots.</summary>
internal sealed class LogThisProfiles
{
    #region Internal Properties

    /// <summary>The operation's default configuration, independent of the current method selection.</summary>
    internal Selection Default { get; }

    #endregion

    #region Private Properties

    // Profile names are literal and compared ordinally.
    private readonly Dictionary<string, Selection> profiles = new(StringComparer.Ordinal);

    #endregion

    #region Constructor

    /// <summary>Validates and snapshots each configuration once for the scope factory.</summary>
    internal LogThisProfiles(ILogThisConfiguration configuration, IReadOnlyDictionary<string, ILogThisConfiguration>? named = null)
    {
        Default = Prepare(configuration);

        if (named == null)
        {
            return;
        }

        foreach (KeyValuePair<string, ILogThisConfiguration> entry in named)
        {
            profiles.Add(ValidateName(entry.Key), Prepare(entry.Value));
        }
    }

    #endregion

    #region Internal Methods

    /// <summary>Selects a whole configuration; null explicitly selects the default.</summary>
    internal Selection Select(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return Default;
        }

        string selectedName = ValidateName(name);

        return profiles.TryGetValue(selectedName, out Selection? selection)
            ? selection
            : throw new InvalidOperationException($"LogThis profile '{name}' is not registered. Register it with AddLogThisProfile or LogThis:Profiles, or correct the attribute's Profile name.");
    }

    /// <summary>Validates literal names consistently for JSON, code registration, and attribute selection.</summary>
    internal static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return name;
    }

    /// <summary>Rejects invalid capture lengths regardless of the truncation toggle.</summary>
    /// <param name="configuration">Default or named configuration being resolved.</param>
    internal static void ValidateCaptureLength(ILogThisConfiguration configuration)
    {
        if (configuration.MaxCapturedValueLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration.MaxCapturedValueLength),
                configuration.MaxCapturedValueLength, "LogThis MaxCapturedValueLength must be greater than zero. Set TruncateCapturedValues to false for unlimited output.");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>Pairs configuration with a validated snapshot of custom components for the scope lifetime.</summary>
    private static Selection Prepare(ILogThisConfiguration configuration)
    {
        ValidateCaptureLength(configuration);
        return new(configuration, LogParameters.PrepareComponents(configuration.MessageComponents));
    }

    #endregion

    #region Internal Type

    /// <summary>A complete configuration and its frozen custom field names/values.</summary>
    internal sealed record Selection(ILogThisConfiguration Configuration, Dictionary<string, object> MessageComponents);

    #endregion
}
