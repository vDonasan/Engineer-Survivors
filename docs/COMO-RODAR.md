# Como rodar, testar, gerar e publicar

Tudo abaixo roda no Windows, a partir da pasta do repositório (`Z:\Dev\EngineeringSurvivors`).

## Requisitos

| Item | Onde |
|---|---|
| Unity **6000.3.27f1** (6.3 LTS) com Web Build Support | `Z:\Unity\Editors\6000.3.27f1` (Unity Hub) |
| .NET SDK 10.0.402 | `Z:\Dev\tools\dotnet` (fora do PATH) |
| Node.js 24 | PATH |
| Unity Cloud: projeto `cd18869f-06b2-4e03-bc30-32ecff7d27a2`, organização `vitordonasan`, com Relay e Lobby ativos | dashboard da Unity |

## Abrir na Unity

No Unity Hub: **Add → Add project from disk → `Z:\Dev\EngineeringSurvivors`** (a raiz do repositório é o projeto Unity).
Abra a cena `Assets/_Project/Scenes/Main.unity` e aperte Play.

Atenção: a pasta `Engineering Survivors\` (com espaço) que o Hub cria ao "abrir" o projeto da nuvem é um projeto vazio
de modelo 3D, não este jogo.

Menus do editor (barra **Engineering Survivors**):
- *Conteúdo → Importar/Exportar JSON*: sincroniza `Content/Data/sp1-content.json` com os ScriptableObjects.
- *Atualizar catálogo de arte e áudio*: refaz `Game/GameAssets.asset` a partir de `Art/manifest.json` e `Audio/`.
- *Interface → Atualizar recursos da interface*: refaz `UI/UiAssets.asset` e `UI/PanelSettings.asset`.
- *Cenas → Recriar cena Main* (e a cena de prova de rede).
- *Projeto → Aplicar configurações*: WebGL (Brotli, template `ES`), pixel perfect (sem AA/MSAA/HDR).

## Testes

```bash
bash Tools/test-sim.sh
```
Simulação em C# puro fora da Unity (NUnit): 183 testes.

```bash
powershell -File Tools/unity-tests.ps1 -Platform EditMode
```

```bash
powershell -File Tools/unity-tests.ps1 -Platform PlayMode
```
O projeto não pode estar aberto em outro editor durante os testes em batchmode.

```bash
npm --prefix ArtSource test
```

```bash
npm --prefix AudioSource test
```

## Arte e áudio

- Pixel art: `npm --prefix ArtSource run build` grava os PNGs pixel a pixel em `Assets/_Project/Art` (documentação em `Art/ANIMACOES.md`).
- Áudio: `npm --prefix AudioSource run build` sintetiza os WAV em `Assets/_Project/Audio`.
- Depois, na Unity: *Atualizar catálogo de arte e áudio*.

## Build WebGL

```bash
powershell -File Tools/build-webgl.ps1 -Scene Main -Out Build/WebGL
```
Leva ~15 min. O script recria a cena Main, o catálogo e os recursos da interface antes de construir.

## Jogar localmente

```bash
node Tools/serve-local.mjs Build/WebGL 8080
```
Abra http://localhost:8080 (o servidor envia os cabeçalhos `Content-Encoding: br` que a build precisa).
O modo cooperativo funciona localmente também: usa o Relay da Unity pela internet.

Parâmetros de URL (automação e testes): `?mode=solo`, `?mode=host&autostart=2`, `?mode=join&code=ABC123`,
`&spec=eletrica|mecanica`, `&name=Ana`, `&log=1` (estado no console a cada 2 s), `&bench=400` (solo: mantém 400 inimigos).

## Provas automatizadas no navegador

Uma vez: `npm --prefix Tools/netproof install` e `npx --prefix Tools/netproof playwright install chromium`
(com `PLAYWRIGHT_BROWSERS_PATH` apontando para onde guardar os navegadores, ex.: `Z:\Dev\tools\ms-playwright`).

```bash
node Tools/netproof/proof.mjs Build/WebGL 8081 jogo
```
Host e cliente em dois Chromium com identidades separadas, pelo Relay. Relatório em `Build/netproof/jogo/report.json`.

```bash
node Tools/netproof/perf.mjs Build/WebGL 8082 --headed
```
FPS com 300 e 400 inimigos, memória, tamanho e capturas em `Build/perf/` (`--headed` usa a GPU real).

## Publicar (Cloudflare Pages)

1. Login (uma vez, feito pelo autor): `npx wrangler@4.147.0 login`.
2. Na primeira vez o projeto foi criado no Pages clássico com `npx wrangler@4.147.0 pages project create engineering-survivors --production-branch main --force` (sem `--force` o wrangler novo tenta migrar para Workers e falha). Depois, `powershell -File Tools/deploy.ps1` copia `Hosting/_headers` para a build e publica no projeto `engineering-survivors`.
3. Abra a URL publicada e jogue antes de divulgar.

## Serviços da Unity

O cliente WebGL contém só o ID do projeto Unity Cloud (público por natureza). Relay e Lobby ficam ativos no dashboard
(Multiplayer → Relay / Lobby). Autenticação anônima: Authentication → Anonymous sign-in habilitado.
Se trocar de projeto na nuvem: Edit → Project Settings → Services, ligar ao novo projeto e refazer a build.
