# Publica a build WebGL no Cloudflare Pages (projeto engineering-survivors).
# Pré-requisito (uma vez, pelo autor): npx wrangler@4.147.0 login
# Uso: powershell -File Tools/deploy.ps1 [-Build Build/WebGL] [-Project engineering-survivors]
param(
    [string] $Build = "Build/WebGL",
    [string] $Project = "engineering-survivors",
    [string] $Wrangler = "wrangler@4.147.0"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root $Build
if (-not (Test-Path (Join-Path $dir "index.html"))) { throw "Build não encontrada em $dir (rode Tools/build-webgl.ps1)" }

# Limite do Cloudflare Pages: 25 MiB por arquivo.
$big = Get-ChildItem $dir -Recurse -File | Where-Object { $_.Length -gt 25MB }
if ($big) { throw "Arquivo acima de 25 MiB: $($big.FullName -join ', ') — use o plano B (GitHub Pages + Decompression Fallback)" }

Copy-Item (Join-Path $root "Hosting\_headers") (Join-Path $dir "_headers") -Force
$total = (Get-ChildItem $dir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Publicando {0:N1} MB de {1} em {2}..." -f $total, $Build, $Project)

# Cria o projeto na primeira vez (ignora o erro se já existir).
npx --yes $Wrangler pages project create $Project --production-branch main 2>$null
npx --yes $Wrangler pages deploy $dir --project-name $Project --branch main --commit-dirty=true
exit $LASTEXITCODE
