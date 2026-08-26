param(
    [Parameter(Mandatory = $true)]
    [string] $PackageSource,

    [Parameter(Mandatory = $true)]
    [string] $PackageVersion,

    [Parameter(Mandatory = $true)]
    [string] $TargetFramework,

    [string] $SdkVersion,

    [string] $ConsumerDirectory,

    [string] $ArtifactsDirectory
)

$ErrorActionPreference = "Stop"

$resolvedPackageSource = (Resolve-Path -LiteralPath $PackageSource).Path
if (-not $ConsumerDirectory) {
    $ConsumerDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("complexity-analyzers-consumer-" + [System.Guid]::NewGuid().ToString("N"))
}

if (-not $ArtifactsDirectory) {
    $ArtifactsDirectory = Join-Path $ConsumerDirectory "artifacts"
}

New-Item -ItemType Directory -Force -Path $ConsumerDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $ArtifactsDirectory | Out-Null
$ConsumerDirectory = (Resolve-Path -LiteralPath $ConsumerDirectory).Path
$ArtifactsDirectory = (Resolve-Path -LiteralPath $ArtifactsDirectory).Path

Push-Location $ConsumerDirectory
try {
    if ($SdkVersion) {
        @{
            sdk = @{
                version = $SdkVersion
                rollForward = "latestFeature"
                allowPrerelease = $false
            }
        } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "global.json"
    }

    dotnet new console --framework $TargetFramework --no-restore
    @(
        "<Project />"
    ) | Set-Content -LiteralPath "Directory.Build.props"
    @(
        "<Project />"
    ) | Set-Content -LiteralPath "Directory.Build.targets"
    @(
        "<Project>"
        "  <PropertyGroup>"
        "    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"
        "  </PropertyGroup>"
        "</Project>"
    ) | Set-Content -LiteralPath "Directory.Packages.props"

    dotnet new nugetconfig

    $nugetConfig = Join-Path $ConsumerDirectory "nuget.config"
    if (-not (Test-Path -LiteralPath $nugetConfig)) {
        $nugetConfig = Join-Path $ConsumerDirectory "NuGet.config"
    }

    if (-not (Test-Path -LiteralPath $nugetConfig)) {
        throw "NuGet config template did not create NuGet.config or nuget.config."
    }

    dotnet nuget add source $resolvedPackageSource --name local-complexity-analyzers --configfile $nugetConfig
    dotnet add package ComplexityAnalysis.Analyzers --version $PackageVersion --source $resolvedPackageSource --no-restore

    @(
        "root = true"
        ""
        "[*.cs]"
        "complexity_analyzers.maximum_complexity = n"
        "dotnet_diagnostic.BIG1006.severity = warning"
        "dotnet_diagnostic.BIG9000.severity = warning"
    ) | Set-Content -LiteralPath ".editorconfig"

    @(
        "using System;"
        ""
        "public static class Program"
        "{"
        "    public static void Main()"
        "    {"
        "        Console.WriteLine(new Sample().Quadratic(new[] { 1, 2, 3 }));"
        "    }"
        "}"
        ""
        "public sealed class Sample"
        "{"
        "    public int Quadratic(int[] values)"
        "    {"
        "        var total = 0;"
        "        foreach (var outer in values)"
        "        {"
        "            foreach (var inner in values)"
        "            {"
        "                total += outer + inner;"
        "            }"
        "        }"
        ""
        "        return total;"
        "    }"
        "}"
    ) | Set-Content -LiteralPath "Program.cs"

    dotnet restore --configfile $nugetConfig

    $logPath = Join-Path $ArtifactsDirectory "consumer-build.log"
    $output = & dotnet build --configuration Release --no-restore 2>&1
    $exitCode = $LASTEXITCODE
    $output | Tee-Object -FilePath $logPath

    if ($exitCode -ne 0) {
        exit $exitCode
    }

    $joined = $output -join "`n"
    if ($joined -match "CS8032|CS9057|AD0001|AD0002") {
        throw "Analyzer load failure or analyzer exception was reported."
    }

    if ($joined -notmatch "BIG9000") {
        throw "Expected BIG9000 probe diagnostic was not reported by the consumer build."
    }

    if ($joined -notmatch "BIG1006") {
        throw "Expected BIG1006 diagnostic was not reported by the consumer build."
    }

    $assetsPath = Join-Path $ConsumerDirectory "obj/project.assets.json"
    $assets = Get-Content -Raw -LiteralPath $assetsPath | ConvertFrom-Json
    $packageKey = "ComplexityAnalysis.Analyzers/$PackageVersion"
    $packageTarget = $null

    foreach ($target in $assets.targets.PSObject.Properties) {
        $candidate = $target.Value.PSObject.Properties[$packageKey]
        if ($null -ne $candidate) {
            $packageTarget = $candidate.Value
            break
        }
    }

    if ($null -eq $packageTarget) {
        throw "Could not find $packageKey in project.assets.json targets."
    }

    foreach ($assetKind in @("compile", "runtime", "runtimeTargets")) {
        $assetProperty = $packageTarget.PSObject.Properties[$assetKind]
        if ($null -ne $assetProperty -and @($assetProperty.Value.PSObject.Properties).Count -ne 0) {
            throw "Analyzer package must not contribute $assetKind assets to the consumer."
        }
    }

    $library = $assets.libraries.PSObject.Properties[$packageKey]
    if ($null -eq $library) {
        throw "Could not find $packageKey in project.assets.json libraries."
    }

    $files = @($library.Value.files | ForEach-Object { $_.Replace('\', '/') })
    if ($files -notcontains "analyzers/dotnet/cs/ComplexityAnalysis.Analyzers.dll") {
        throw "Analyzer package did not expose ComplexityAnalysis.Analyzers.dll under analyzers/dotnet/cs/."
    }

    $dependencies = $library.Value.PSObject.Properties["dependencies"]
    if ($null -ne $dependencies) {
        $roslynDependencies = @($dependencies.Value.PSObject.Properties | Where-Object { $_.Name -like "Microsoft.CodeAnalysis*" })
        if ($roslynDependencies.Count -ne 0) {
            throw "Roslyn dependencies leaked through package metadata: $($roslynDependencies.Name -join ', ')"
        }
    }
}
finally {
    Pop-Location
}
