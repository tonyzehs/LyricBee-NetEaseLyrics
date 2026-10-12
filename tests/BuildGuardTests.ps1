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
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolvedFixture.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedFixture) -notlike 'lyricbee-build-guard-*') {
        throw 'Refusing cleanup outside the test fixture.'
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
