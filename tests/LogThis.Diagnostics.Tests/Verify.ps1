# Build real consumers and compare every LogThis diagnostic with inline expectations.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'LogThis.Diagnostics.Tests.csproj'
foreach ($fixture in @('Standalone', 'All')) {
    & dotnet build $project -c Release -t:Rebuild "-p:DiagnosticFixture=$fixture" --nologo
    if ($LASTEXITCODE -ne 0) { throw "The $fixture diagnostic build failed." }

    $files = @('Standalone.cs')
    if ($fixture -eq 'All') { $files += 'Combinations.cs' }
    $expected = @(
        foreach ($file in $files) {
            $lineNumber = 0
            foreach ($line in Get-Content (Join-Path $PSScriptRoot $file)) {
                $lineNumber++
                if ($line -match '// (LOGTHIS\d{3}(?: LOGTHIS\d{3})*)') {
                    foreach ($id in $Matches[1].Split(' ')) { '{0}:{1}:{2}' -f $file, $lineNumber, $id }
                }
            }
        }
    )
    $report = Get-Content (Join-Path $PSScriptRoot 'obj/diagnostics.sarif') -Raw | ConvertFrom-Json
    $actual = @(
        foreach ($diagnostic in $report.runs.results) {
            if ($diagnostic.ruleId -notmatch '^LOGTHIS') { continue }
            if ($diagnostic.level -ne 'warning') { throw "Expected a warning: $($diagnostic.ruleId)" }
            $location = $diagnostic.locations[0].resultFile
            $file = [IO.Path]::GetFileName(([Uri]$location.uri).LocalPath)
            '{0}:{1}:{2}' -f $file, $location.region.startLine, $diagnostic.ruleId
        }
    )
    # Retain duplicate entries: an extra report on the same declaration must fail.
    if ((($actual | Sort-Object) -join "`n") -cne (($expected | Sort-Object) -join "`n")) {
        throw "Unexpected diagnostics for ${fixture}. Expected: $($expected -join ', '). Actual: $($actual -join ', ')."
    }
    Write-Host "$fixture verified: $($actual.Count) warnings, no missing, duplicate, or unexpected diagnostics."
}
