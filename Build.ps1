$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\FavoriteHelper'
$output = Join-Path $root 'artifacts\bin'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$uiaClient = "$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll"
$uiaTypes = "$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$sources = Get-ChildItem -LiteralPath $source -Filter '*.cs' | ForEach-Object FullName
$icon = Join-Path $source 'Assets\FavoriteHelper.ico'
& $csc /nologo /target:winexe /platform:x64 /win32icon:$icon /out:"$output\FavoriteHelper.exe" /reference:$uiaClient /reference:$uiaTypes $sources
if ($LASTEXITCODE) { throw 'FavoriteHelper compilation failed' }
Write-Host "Built $output\FavoriteHelper.exe"
