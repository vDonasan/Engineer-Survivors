# Roda os testes da Unity em batchmode e resume o XML de resultados.
# Uso: powershell -File Tools/unity-tests.ps1 -Platform EditMode|PlayMode
# O projeto não pode estar aberto em outro editor.
param(
    [ValidateSet("EditMode", "PlayMode")] [string] $Platform = "EditMode",
    [string] $Unity = $(if ($env:UNITY_EXE) { $env:UNITY_EXE } else { "Z:\Unity\Editors\6000.3.27f1\Editor\Unity.exe" }),
    [string] $Filter = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $root "Logs"
New-Item -ItemType Directory -Force $logs | Out-Null
$results = Join-Path $logs "tests-$Platform.xml"
$log = Join-Path $logs "tests-$Platform.log"
Remove-Item $results -ErrorAction SilentlyContinue

$args = @("-batchmode", "-nographics", "-projectPath", "`"$root`"", "-runTests", "-testPlatform", $Platform,
          "-testResults", "`"$results`"", "-logFile", "`"$log`"")
if ($Filter) { $args += @("-testFilter", $Filter) }
$p = Start-Process -FilePath $Unity -ArgumentList $args -PassThru -Wait -NoNewWindow

if (-not (Test-Path $results)) {
    Write-Host "Sem resultados (código $($p.ExitCode)). Últimas linhas do log:"
    Get-Content $log -Tail 40
    exit 2
}
[xml] $x = Get-Content $results
$run = $x.'test-run'
Write-Host ("{0}: total {1}, passou {2}, falhou {3}, ignorado {4}" -f $Platform, $run.total, $run.passed, $run.failed, $run.skipped)
$x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
    Write-Host ("FALHOU {0}: {1}" -f $_.fullname, $_.failure.message.InnerText.Trim())
}
exit $(if ([int]$run.failed -gt 0) { 1 } else { 0 })
