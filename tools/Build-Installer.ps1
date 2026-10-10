param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$publish = Join-Path $root 'artifacts\publish'
$setupPublish = Join-Path $root 'artifacts\installer\setup-publish'
$installer = Join-Path $root 'artifacts\installer'
foreach ($target in @($publish, $setupPublish)) {
    $resolved = [System.IO.Path]::GetFullPath($target)
    if (-not $resolved.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Build output is outside the project root.'
    }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
New-Item -ItemType Directory -Path $installer -Force | Out-Null
& dotnet publish (Join-Path $root 'src\PromptManager\PromptManager.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -p:NuGetAudit=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
foreach ($file in @('PromptManager.exe', 'USER_MANUAL.html', 'USER_MANUAL.md', 'Assets\ProtoCall_logo.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $file))) { throw "Missing application file: $file" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload = Join-Path $installer 'payload.zip'
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($publish, $payload, [System.IO.Compression.CompressionLevel]::Optimal, $false)
& dotnet publish (Join-Path $root 'installer\PromptManagerSetup\PromptManagerSetup.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:NuGetAudit=false -o $setupPublish
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
$result = Join-Path $installer 'PromptManagerSetup.exe'
Copy-Item -LiteralPath (Join-Path $setupPublish 'PromptManagerSetup.exe') -Destination $result -Force
Get-Item -LiteralPath $result | Select-Object FullName, Length, @{Name='Version';Expression={$_.VersionInfo.FileVersion}}
Get-FileHash -LiteralPath $result -Algorithm SHA256 | Format-List Algorithm, Hash
