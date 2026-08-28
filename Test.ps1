$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'Build.ps1')
$source = Join-Path $root 'src\FavoriteHelper'
$output = Join-Path $root 'artifacts\bin'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$core = @('Models.cs', 'FileIdentityReader.cs', 'SessionManager.cs', 'Log.cs') | ForEach-Object { Join-Path $source $_ }
& $csc /nologo /target:exe /platform:x64 /out:"$output\CoreTests.exe" $core (Join-Path $root 'tests\CoreTests.cs')
if ($LASTEXITCODE) { throw 'Core tests compilation failed' }
& "$output\CoreTests.exe"
if ($LASTEXITCODE) { throw 'Core tests failed' }
$round2 = @('Models.cs', 'FileIdentityReader.cs', 'FavoriteOperations.cs', 'ShellLinkInterop.cs') | ForEach-Object { Join-Path $source $_ }
& $csc /nologo /target:exe /platform:x64 /out:"$output\Round2ATests.exe" $round2 (Join-Path $root 'tests\Round2ATests.cs')
if ($LASTEXITCODE) { throw 'Round 2A tests compilation failed' }
& "$output\Round2ATests.exe"
if ($LASTEXITCODE) { throw 'Round 2A tests failed' }
$product = @('Models.cs', 'FileIdentityReader.cs', 'FavoriteOperations.cs', 'ShellLinkInterop.cs', 'AppConfig.cs', 'NotificationPolicy.cs') | ForEach-Object { Join-Path $source $_ }
& $csc /nologo /target:exe /platform:x64 /out:"$output\ProductizationTests.exe" $product (Join-Path $root 'tests\ProductizationTests.cs')
if ($LASTEXITCODE) { throw 'Productization tests compilation failed' }
& "$output\ProductizationTests.exe"
if ($LASTEXITCODE) { throw 'Productization tests failed' }
& $csc /nologo /target:exe /platform:x64 /out:"$output\LoggingTests.exe" (Join-Path $source 'Log.cs') (Join-Path $root 'tests\LoggingTests.cs')
if ($LASTEXITCODE) { throw 'Logging tests compilation failed' }
& "$output\LoggingTests.exe"
if ($LASTEXITCODE) { throw 'Logging tests failed' }
