[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'src/Jev.Tool/Jev.Tool.csproj'
$raw = & dotnet msbuild $project -restore -target:GetVersion -getProperty:GitVersion_SemVer,PackageVersion,NuGetPackageRoot,GitVersionTargetFramework
if ($LASTEXITCODE -ne 0) { throw 'Could not calculate the package version.' }
$properties = ($raw | ConvertFrom-Json).Properties
if ($properties.PackageVersion -ne $properties.GitVersion_SemVer) {
    throw 'MSBuild PackageVersion does not match GitVersion SemVer.'
}

$package = Get-Item -LiteralPath $PackagePath
$expectedName = "Jev.Tool.$($properties.GitVersion_SemVer).nupkg"
if ($package.Name -ne $expectedName) { throw "Expected package filename $expectedName." }
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $entry = $archive.GetEntry('Jev.Tool.nuspec')
    if ($null -eq $entry) { throw 'Package manifest is missing.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($manifest.package.metadata.version -ne $properties.GitVersion_SemVer) {
        throw 'Package manifest version does not match GitVersion SemVer.'
    }
    if ($manifest.SelectNodes('//*[local-name()="dependency" and @id="GitVersion.MsBuild"]').Count -ne 0) {
        throw 'GitVersion leaked into package dependencies.'
    }
} finally {
    $archive.Dispose()
}
Write-Output "PASS: package filename, manifest, and MSBuild agree on $($properties.GitVersion_SemVer)."

[xml]$packages = Get-Content -LiteralPath (Join-Path $repo 'Directory.Packages.props') -Raw
$gitVersion = ($packages.Project.ItemGroup.PackageVersion | Where-Object Include -EQ 'GitVersion.MsBuild').Version
$gitVersionDll = Join-Path $properties.NuGetPackageRoot "gitversion.msbuild/$gitVersion/tools/$($properties.GitVersionTargetFramework)/gitversion.dll"
$fixture = Join-Path $repo ('artifacts/version-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null

function Invoke-FixtureGit {
    & git -C $fixture -c user.name=GitVersionTest -c user.email=gitversion-test@example.invalid @args | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Fixture git command failed: $args" }
}

function Get-FixtureVersion {
    $result = & dotnet $gitVersionDll $fixture -output json -nofetch -nocache
    if ($LASTEXITCODE -ne 0) { throw 'GitVersion failed for the isolated history fixture.' }
    return ($result | ConvertFrom-Json).SemVer
}

Invoke-FixtureGit init --initial-branch main
Copy-Item -LiteralPath (Join-Path $repo 'GitVersion.yml') -Destination $fixture
Invoke-FixtureGit add GitVersion.yml
Invoke-FixtureGit commit -m 'Initial fixture'
$first = Get-FixtureVersion
if ($first -notmatch '^\d+\.\d+\.\d+-ci\.\d+$') { throw "Expected a CI prerelease, got $first." }
if ((Get-FixtureVersion) -ne $first) { throw 'Repeated calculation changed the version of the same commit.' }
Write-Output "PASS: same commit produces the same CI SemVer ($first)."

Invoke-FixtureGit commit --allow-empty -m 'Second fixture commit'
$second = Get-FixtureVersion
$firstNumber = [int]($first.Split('.')[-1])
$expectedSecond = $first.Substring(0, $first.LastIndexOf('.') + 1) + ($firstNumber + 1)
if ($second -ne $expectedSecond) { throw "Expected $expectedSecond after another commit, got $second." }
Write-Output "PASS: the next commit advances the package version ($second)."

Invoke-FixtureGit tag v1.2.3
$tagged = Get-FixtureVersion
if ($tagged -ne '1.2.3') { throw "Expected the release tag version 1.2.3, got $tagged." }
Write-Output 'PASS: a release tag produces its stable SemVer (1.2.3).'

Invoke-FixtureGit commit --allow-empty -m 'Post-release fixture commit'
$afterTag = Get-FixtureVersion
if ($afterTag -ne '1.2.4-ci.1') { throw "Expected 1.2.4-ci.1 after the release tag, got $afterTag." }
Write-Output 'PASS: a post-release commit advances the patch and CI counter (1.2.4-ci.1).'
Write-Output "History fixture: $fixture"
