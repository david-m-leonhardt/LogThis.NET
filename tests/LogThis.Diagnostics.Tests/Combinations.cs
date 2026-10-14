using LogThis.Attributes;

/// <summary>Method conflicts produce one warning for each ineffective annotation.</summary>
public sealed class MethodConflicts
{
    #region Public Methods

    [LogThis, DontLogThis] public void Excluded() { } // LOGTHIS003
    [LogThis, MaskReturnInLogs, DontLogThis] public string Both() => "secret"; // LOGTHIS003 LOGTHIS004

    #endregion
}

/// <summary>Class coverage makes same-profile public method annotations redundant.</summary>
[LogThis]
public class DefaultProfiles
{
    #region Public Methods

    [LogThis] public void ImplicitDefault() { } // LOGTHIS006
    [LogThis(Profile = null)] public static void ExplicitDefault() { } // LOGTHIS006
    [LogThis(Profile = "Other")] public void Different() { }
    [DontLogThis] public void ValidExclusion() { }
    [LogThis, DontLogThis] public void Excluded() { } // LOGTHIS003 (no LOGTHIS006)
    [MaskReturnInLogs] public string Masked() => "secret";

    #endregion

    #region Nonpublic Methods

    [LogThis] private void Private() { }
    [LogThis] protected void Protected() { }
    [LogThis] internal void Internal() { }

    #endregion
}

/// <summary>Profile comparison uses trimmed case-insensitive names, not configuration contents.</summary>
[LogThis(Profile = " Audit ")]
public sealed class NamedProfiles
{
    #region Public Methods

    [LogThis(Profile = "audit")] public void Same() { } // LOGTHIS006
    [LogThis(Profile = " AUDIT ")] public void SameTrimmed() { } // LOGTHIS006
    [LogThis] public void Default() { }
    [LogThis(Profile = null)] public void ExplicitDefault() { }
    [LogThis(Profile = "Other")] public void Different() { }
    [LogThis(Profile = " ")] public void InvalidNameIsNotRedundant() { }
    [DontLogThis, MaskReturnInLogs] public string Excluded() => "secret"; // LOGTHIS004

    #endregion
}
