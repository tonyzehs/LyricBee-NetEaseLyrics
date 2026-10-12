param([string]$DotNetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('lyricbee-build-guard-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Properties') -Force | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\build.ps1') -Destination $fixture
    '[assembly: AssemblyInformationalVersion("1.9.5")]' |
        Set-Content -LiteralPath (Join-Path $fixture 'Properties\AssemblyInfo.cs')
    $rejected = $false
    try {
        & (Join-Path $fixture 'build.ps1') -DevelopmentDiagnostics -DotNetPath 'must-not-be-invoked'
    } catch {
        if ($_.Exception.Message -notlike '*stable packaging is refused*') { throw }
        $rejected = $true
    }
    if (!$rejected -or (Test-Path -LiteralPath (Join-Path $fixture 'artifacts'))) {
        throw 'Stable diagnostics guard did not reject before build or packaging.'
    }
    Write-Output 'PASS: stable diagnostics packaging is refused before SDK invocation.'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\NeteaseLyrics.csproj') -Destination $fixture
    $project = Join-Path $fixture 'NeteaseLyrics.csproj'
    $assemblyInfo = Join-Path $fixture 'Properties\AssemblyInfo.cs'
    function Set-DeclaredVersion([string]$Version) {
        $text = if ($Version) { '[assembly: AssemblyInformationalVersion("' + $Version + '")]' } else { '// no informational version' }
        Set-Content -LiteralPath $assemblyInfo -Value $text -Encoding utf8
    }
    function Check-Project([string]$Label, [string]$Version, [bool]$Diagnostics, [bool]$Rejected,
                           [string[]]$Extra = @(), [switch]$FullBuild) {
        Set-DeclaredVersion $Version
        $arguments = if ($FullBuild) { @('build', $project, '--no-restore', '--nologo') }
            else { @('msbuild', $project, '-t:ValidateDevelopmentDiagnosticsVersion', '-nologo') }
        $arguments += '-p:DevelopmentDiagnostics=' + $Diagnostics.ToString().ToLowerInvariant()
        $arguments += $Extra
        $output = & $DotNetPath @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $guardFailure = ($output -join [Environment]::NewLine) -match 'error LYRICBEE001'
        if (($Rejected -and ($exitCode -eq 0 -or !$guardFailure)) -or (!$Rejected -and $exitCode -ne 0)) {
            throw "$Label failed (exit $exitCode): $($output -join [Environment]::NewLine)"
        }
        Write-Output "PASS: $Label"
    }
    # These run the actual project target. Full negative builds must stop at our guard before missing assets/compiler inputs.
    Check-Project 'AnyCPU Release stable dev build rejected before compilation' '1.9.5' $true $true -Extra @('-c', 'Release') -FullBuild
    Check-Project 'x86 Debug stable dev build rejected' '1.9.5' $true $true -Extra @('-p:Platform=x86', '-c', 'Debug') -FullBuild
    Check-Project 'SDK version properties cannot disguise a stable DLL' '1.9.5' $true $true -Extra @('-p:Version=1.9.5-test.99', '-p:InformationalVersion=1.9.5-test.99') -FullBuild
    Check-Project 'stable version permits the non-diagnostic profile' '1.9.5' $false $false
    foreach ($version in @('1.9.5-test.3', '1.9.5-pre', '1.9.5-pre.1', '1.9.5-pre01')) {
        Check-Project "recognized development version $version permitted" $version $true $false
    }
    foreach ($version in @('1.9.5-preview', '1.9.5-test.', '1.9.5-test.3-extra', '1.9.5-release-pre', '')) {
        Check-Project "unrecognized or missing version '$version' rejected" $version $true $true
    }
    if (Get-ChildItem -LiteralPath $fixture -Recurse -Filter '*.dll') {
        throw 'A rejected build produced a DLL.'
    }
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolvedFixture.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedFixture) -notlike 'lyricbee-build-guard-*') {
        throw 'Refusing cleanup outside the test fixture.'
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
# Expected build rejections leave a native exit code of 1. Successful checks must report success to CI callers.
$global:LASTEXITCODE = 0
