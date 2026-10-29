# Verify the generated warning is suppressed only on eligible instrumented methods.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'LogThis.Diagnostics.Tests.csproj'
foreach ($invalid in @('false', 'true')) {
    & dotnet build $project -c Release -t:Rebuild -p:DiagnosticFixture=Cancellation "-p:InvalidCancellation=$invalid" -p:WarningsAsErrors=CS8424 --nologo
    $buildExit = $LASTEXITCODE
    if ($invalid -eq 'false' -and $buildExit -ne 0) { throw 'Valid iterator consumer failed with CS8424 treated as an error.' }
    if ($invalid -eq 'true' -and $buildExit -eq 0) { throw 'Invalid cancellation annotations were silently suppressed.' }
    $report = Get-Content (Join-Path $PSScriptRoot 'obj/diagnostics.sarif') -Raw | ConvertFrom-Json
    foreach ($diagnostic in $report.runs.results) {
        if ($diagnostic.level -eq 'error' -and $diagnostic.ruleId -ne 'CS8424' -and
            $diagnostic.suppressionStates -notcontains 'suppressedInSource') {
            throw "Unexpected build error: $($diagnostic.ruleId)"
        }
    }
    $actual = @(
        foreach ($diagnostic in $report.runs.results) {
            if ($diagnostic.ruleId -ne 'CS8424') { continue }
            if ($diagnostic.suppressionStates -contains 'suppressedInSource') { continue }
            if ($diagnostic.level -ne 'error') { throw 'CS8424 was not promoted to an error.' }
            $diagnostic.locations[0].resultFile.region.startLine
        }
    )
    $expected = @()
    if ($invalid -eq 'true') {
        $lineNumber = 0
        foreach ($line in Get-Content (Join-Path $PSScriptRoot 'Cancellation.cs')) {
            $lineNumber++
            if ($line -match '// CS8424') { $expected += $lineNumber }
        }
    }
    # Multiple invalid annotations on one method can produce multiple reports at that line.
    if ((($actual | Sort-Object -Unique) -join ',') -ne (($expected | Sort-Object -Unique) -join ',')) {
        throw "Unexpected CS8424 locations. Expected: $expected; actual: $actual"
    }
    Write-Host "Cancellation consumer verified (invalid=$invalid): $($actual.Count) expected CS8424 errors."
}
