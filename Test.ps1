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
$compatibility = @('NativeMethods.cs', 'WindowClassifier.cs') | ForEach-Object { Join-Path $source $_ }
& $csc /nologo /target:exe /platform:x64 /out:"$output\CompatibilityTests.exe" $compatibility (Join-Path $root 'tests\CompatibilityTests.cs')
if ($LASTEXITCODE) { throw 'Compatibility tests compilation failed' }
& "$output\CompatibilityTests.exe"
if ($LASTEXITCODE) { throw 'Compatibility tests failed' }
$migration = @('Models.cs', 'FileIdentityReader.cs', 'FavoriteOperations.cs', 'ShellLinkInterop.cs', 'ShortcutMigration.cs') | ForEach-Object { Join-Path $source $_ }
& $csc /nologo /target:exe /platform:x64 /out:"$output\MigrationTests.exe" $migration (Join-Path $root 'tests\MigrationTests.cs')
if ($LASTEXITCODE) { throw 'Migration tests compilation failed' }
& "$output\MigrationTests.exe"
if ($LASTEXITCODE) { throw 'Migration tests failed' }
$export = @('Models.cs', 'FileIdentityReader.cs', 'FavoriteOperations.cs', 'ShellLinkInterop.cs', 'ExportService.cs') | ForEach-Object { Join-Path $source $_ }
& $csc /nologo /target:exe /platform:x64 /out:"$output\ExportTests.exe" $export (Join-Path $root 'tests\ExportTests.cs')
if ($LASTEXITCODE) { throw 'Export tests compilation failed' }
& "$output\ExportTests.exe"
if ($LASTEXITCODE) { throw 'Export tests failed' }
$command = @('Models.cs', 'FileIdentityReader.cs', 'FavoriteOperations.cs', 'ShellLinkInterop.cs', 'ShortcutMigration.cs', 'ExportService.cs', 'CommandLine.cs') | ForEach-Object { Join-Path $source $_ }
& $csc /nologo /target:exe /platform:x64 /out:"$output\CommandLineTests.exe" $command (Join-Path $root 'tests\CommandLineTests.cs')
if ($LASTEXITCODE) { throw 'Command-line tests compilation failed' }
& "$output\CommandLineTests.exe"
if ($LASTEXITCODE) { throw 'Command-line tests failed' }
