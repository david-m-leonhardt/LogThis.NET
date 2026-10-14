# Attribute diagnostic build checks

From the repository root:

```powershell
./tests/LogThis.Diagnostics.Tests/Verify.ps1
```

The script rebuilds two real Metalama consumers and compares the compiler's SARIF
results with inline expected IDs and source lines. It checks warning severity,
missing/duplicate reports, and unexpected warnings on valid declarations.

The standalone build contains no LogThis usage. It checks property, nested
property, method-parameter, and constructor-parameter conflicts, plus excluded
return masking. The complete build adds method conflicts and default/named
profile comparisons, including whitespace/case normalization, explicit default
selection, different names, nonpublic methods, and excluded methods. No runtime
configuration is needed or consulted by these diagnostics.

Existing integration checks remain in `../LogThis.OptOut.Tests`; their deliberate
conflicting attributes now produce the corresponding warnings while retaining
all existing runtime assertions. Existing LOGTHIS001/LOGTHIS002 checks remain in
that project's diagnostic fixtures.
