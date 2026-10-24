# Gera a build WebGL em batchmode.
# Uso: powershell -File Tools/build-webgl.ps1 -Scene NetProof -Out Build/WebGL-proof
param(
    [string] $Scene = "Main",
    [string] $Out = "Build/WebGL",
    [string] $Unity = $(if ($env:UNITY_EXE) { $env:UNITY_EXE } else { "Z:\Unity\Editors\6000.3.27f1\Editor\Unity.exe" })
)
$root = Split-Path -Parent $PSScriptRoot
$log = Join-Path $root "Logs\build-webgl.log"
New-Item -ItemType Directory -Force (Join-Path $root "Logs") | Out-Null
$args = @("-batchmode", "-nographics", "-quit", "-projectPath", "`"$root`"", "-executeMethod", "ES.GameEditor.BuildScripts.BuildWebGL",
          "-esScene", $Scene, "-esOut", $Out, "-logFile", "`"$log`"")
$p = Start-Process -FilePath $Unity -ArgumentList $args -PassThru -Wait -NoNewWindow
Select-String -Path $log -Pattern "\[ES\]|error CS|Error building|BuildFailedException" | ForEach-Object { $_.Line.Trim() } | Select-Object -First 30
exit $p.ExitCode
