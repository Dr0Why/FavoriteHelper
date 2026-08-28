$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:DOTNET_CLI_HOME = Join-Path $root 'artifacts\.dotnet-home'
$env:APPDATA = Join-Path $root 'artifacts\.appdata'
$env:NUGET_PACKAGES = Join-Path $root 'artifacts\.nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$project = Join-Path $root 'src\FavoriteHelper\FavoriteHelper.csproj'
$output = Join-Path $root 'artifacts\Portable\FavoriteHelper'
$portableRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts\Portable'))
$resolvedOutput = [IO.Path]::GetFullPath($output)
if (-not $resolvedOutput.StartsWith($portableRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing to clean output outside artifacts\Portable' }
if (Test-Path -LiteralPath $resolvedOutput) { Remove-Item -LiteralPath $resolvedOutput -Recurse -Force }
$localDotnet = Join-Path $root 'artifacts\.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
& $dotnet publish $project -c Release -r win-x64 --self-contained true -o $output --configfile (Join-Path $root 'NuGet.Config')
if ($LASTEXITCODE) { throw 'Self-contained publish failed' }
Copy-Item -LiteralPath (Join-Path $root 'config.json') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $root 'RELEASE_NOTES-v6.1.0.md') -Destination (Join-Path $output 'RELEASE_NOTES.md') -Force
Write-Host "Published self-contained win-x64 Portable output: $output"
