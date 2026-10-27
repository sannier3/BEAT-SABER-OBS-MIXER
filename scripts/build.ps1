param(
    [Parameter(Mandatory=$true)][string]$BeatSaberDir,
    [switch]$Install,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$BeatSaberDir = (Resolve-Path -LiteralPath $BeatSaberDir).Path
$required = @(
    'Beat Saber_Data/Managed/IPA.Loader.dll',
    'Beat Saber_Data/Managed/UnityEngine.dll',
    'Beat Saber_Data/Managed/UnityEngine.CoreModule.dll',
    'Beat Saber_Data/Managed/UnityEngine.ImageConversionModule.dll',
    'Beat Saber_Data/Managed/UnityEngine.InputLegacyModule.dll',
    'Beat Saber_Data/Managed/UnityEngine.UIModule.dll',
    'Beat Saber_Data/Managed/UnityEngine.TextRenderingModule.dll',
    'Beat Saber_Data/Managed/UnityEngine.UI.dll',
    'Beat Saber_Data/Managed/Unity.TextMeshPro.dll',
    'Plugins/ChatPlexSDK_BS.dll',
    'Libs/Hive.Versioning.dll',
    'Beat Saber_Data/Managed/Main.dll',
    'Beat Saber_Data/Managed/BeatSaber.ViewSystem.dll',
    'Beat Saber_Data/Managed/BGLib.Polyglot.dll',
    'Beat Saber_Data/Managed/GameplayCore.dll',
    'Beat Saber_Data/Managed/HMUI.dll',
    'Beat Saber_Data/Managed/HMLib.dll',
    'Plugins/BSML.dll',
    'Plugins/BS_Utils.dll'
)
foreach ($file in $required) {
    if (!(Test-Path -LiteralPath (Join-Path $BeatSaberDir $file))) { throw "Missing game dependency: $file" }
}
$chatPlexPath = Join-Path $BeatSaberDir 'Plugins/ChatPlexSDK_BS.dll'
$chatPlexVersion = [System.Reflection.AssemblyName]::GetAssemblyName($chatPlexPath).Version
if ($chatPlexVersion -ne [version]'6.4.2.0') {
    throw "This Beat Saber 1.40.8 build requires ChatPlexSDK_BS 6.4.2.0. Found $chatPlexVersion."
}
Write-Host "Target: Beat Saber 1.40.8 / ChatPlexSDK_BS $chatPlexVersion / .NET Framework 4.8"
if (!$SkipTests) {
    & dotnet test (Join-Path $projectRoot 'tests/OBSMixer.Tests/OBSMixer.Tests.csproj') -c Release "-p:BeatSaberDir=$BeatSaberDir"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
& dotnet build (Join-Path $projectRoot 'src/OBSMixer.Plugin/OBSMixer.Plugin.csproj') -c Release "-p:BeatSaberDir=$BeatSaberDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$output = Join-Path $projectRoot 'src/OBSMixer.Plugin/bin/Release/net48'
$package = Join-Path $projectRoot 'dist/package'
if (Test-Path $package) { Remove-Item -LiteralPath $package -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$package/Plugins", "$package/Libs" | Out-Null
Copy-Item -LiteralPath "$output/OBSMixer.dll" -Destination "$package/Plugins/OBSMixer.dll"
Copy-Item -LiteralPath "$output/OBSMixer.Core.dll" -Destination "$package/Libs/OBSMixer.Core.dll"
Copy-Item -LiteralPath $chatPlexPath -Destination "$package/Plugins/ChatPlexSDK_BS.dll"
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'third-party/ChatPlexSDK_BS') -Filter 'LICENSE.*.txt' | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination "$package/Plugins/$($_.Name)"
}
foreach ($notice in @('THIRD-PARTY-NOTICES.md', 'THIRD-PARTY-NOTICES.fr.md')) {
    $noticeText = Get-Content -LiteralPath (Join-Path $projectRoot $notice) -Raw -Encoding UTF8
    $noticeText = $noticeText.Replace('(README.md)', '(README-OBSMixer.md)').Replace('(README.fr.md)', '(README-OBSMixer.fr.md)')
    Set-Content -LiteralPath (Join-Path $package $notice) -Value $noticeText -Encoding UTF8 -NoNewline
}
$readmeFiles = [ordered]@{
    'README.md' = 'README-OBSMixer.md'
    'README.fr.md' = 'README-OBSMixer.fr.md'
}
foreach ($entry in $readmeFiles.GetEnumerator()) {
    $readme = Get-Content -LiteralPath (Join-Path $projectRoot $entry.Key) -Raw -Encoding UTF8
    $readme = $readme.Replace('(README.md)', '(README-OBSMixer.md)').Replace('(README.fr.md)', '(README-OBSMixer.fr.md)')
    $readme = $readme.Replace('(docs/images/', '(https://raw.githubusercontent.com/sannier3/BEAT-SABER-OBS-MIXER/main/docs/images/')
    foreach ($document in @('DEVELOPMENT.md', 'DEVELOPMENT.fr.md', 'COMPATIBILITY.md', 'COMPATIBILITY.fr.md', 'VALIDATION.md', 'VALIDATION.fr.md')) {
        $readme = $readme.Replace("($document)", "(https://github.com/sannier3/BEAT-SABER-OBS-MIXER/blob/main/$document)")
    }
    Set-Content -LiteralPath (Join-Path $package $entry.Value) -Value $readme -Encoding UTF8 -NoNewline
}
Compress-Archive -Path "$package/*" -DestinationPath (Join-Path $projectRoot 'dist/OBSMixer-0.1.0.zip') -Force
if ($Install) {
    Copy-Item -LiteralPath "$package/Plugins/OBSMixer.dll" -Destination "$BeatSaberDir/Plugins/OBSMixer.dll"
    New-Item -ItemType Directory -Path "$BeatSaberDir/Libs" -Force | Out-Null
    Copy-Item -LiteralPath "$package/Libs/OBSMixer.Core.dll" -Destination "$BeatSaberDir/Libs/OBSMixer.Core.dll"
}
Write-Host "Package: $projectRoot/dist/OBSMixer-0.1.0.zip"
