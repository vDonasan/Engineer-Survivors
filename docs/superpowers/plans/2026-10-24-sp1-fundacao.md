# SP1 — Fundação jogável + prova online: Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar o primeiro jogo jogável de Engineering Survivors (2 engenharias, 3 inimigos, arena Laboratório) em WebGL publicado, com solo e coop online de até 4 jogadores via Unity Relay.

**Architecture:** Simulação autoritativa em C# puro (`ES.Sim`, sem `UnityEngine`) executada só no host a 30 Hz; compilada também fora da Unity para testes `dotnet test` e bots. NGO 2.x transporta entradas, snapshots compactos e eventos; solo usa `SinglePlayerTransport`. Unity cuida de visuais com pool, UI Toolkit, áudio e build WebGL.

**Tech Stack:** Unity 6000.3.27f1 (6.3 LTS), C# 9, URP 2D, Input System, UI Toolkit, Netcode for GameObjects 2.x, Multiplayer Services 2.x (Sessions/Relay `wss`), NUnit, .NET SDK 10.0.402, Node 24 (arte/áudio), Cloudflare Pages.

**Spec:** `docs/superpowers/specs/2026-10-24-sp1-fundacao-design.md`

## Global Constraints

- Editor: `Z:\Unity\Editors\6000.3.27f1\Editor\Unity.exe`. Projeto Unity na **raiz** do repositório (`Z:\Dev\EngineeringSurvivors`).
- .NET: `Z:\Dev\tools\dotnet\dotnet.exe` (fora do PATH; sempre caminho completo).
- Código em `Assets/_Project/Sim/**`: `netstandard2.1`, `LangVersion 9`, sem `UnityEngine`, sem `record`/`init`, sem LINQ e sem alocação dentro de `World.Step`.
- Tick 30 Hz; snapshot 15 Hz em blocos ≤ 1100 bytes; interpolação 100–200 ms adaptativa.
- Tetos (dados, não constantes): 400 inimigos, 150 gemas, 200 projéteis hostis, 48 eventos cosméticos por snapshot, 64 números de dano na tela.
- Partida: vitória aos 600 s; máximo 4 jogadores; sessão privada, trancada ao iniciar.
- Balanceamento combinado **somente** em `BalanceResolver`.
- Unity Cloud: projeto `cd18869f-06b2-4e03-bc30-32ecff7d27a2`, organização `vitordonasan`. Nenhum outro identificador ou chave no cliente.
- Textos ao jogador em português (Brasil) via tabela de strings por chave. Nenhum botão sem função.
- IDs de conteúdo em `snake_case` português (ex.: `descarga_direcionada`); identificadores de código em inglês, exceto valores do enum `Tag` (vocabulário do domínio).
- Commits: `tipo(escopo): descrição` em português, terminando com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Comandos de teste:
  - Sim: `Z:\Dev\tools\dotnet\dotnet.exe test Tools/ES.Tools.sln`
  - Unity: `& $Unity -batchmode -projectPath Z:\Dev\EngineeringSurvivors -runTests -testPlatform EditMode|PlayMode -testResults Logs/<nome>.xml -logFile Logs/<nome>.log` (sem `-quit`; o projeto não pode estar aberto em outro editor).

## Review Focus

1. **Código de sala digitado com espaços/minúsculas** (" f6gplw ") deve funcionar; caracteres inválidos rejeitados antes de chamar o serviço. → teste em Task 18.
2. **Conjunto de ofertas esgotado** (tudo no máximo, espaços cheios): a subida de nível ainda oferece ≥ 1 opção (atributo ou Kit), nunca 0 opções nem exceção. → teste em Task 11.
3. **Várias subidas de nível de uma vez** (gema grande): ofertas enfileiradas e resolvidas em ordem, sem perder nível. → teste em Task 11.
4. **Jogador desconecta com oferta pendente ou sendo o último vivo:** sem travar; oferta descartada; derrota avaliada só entre jogadores ativos. → teste em Task 13.
5. **Quadro longo no host** (aba volta do segundo plano após 2 min): a simulação roda no máximo 5 ticks por quadro e descarta o resto, sem "espiral da morte". → teste em Task 13.

---

## Estrutura de arquivos

```
Assets/_Project/Sim/                    asmdef ES.Sim (noEngineReferences)
  Core/        Vec2.cs Rng.cs Quant.cs TagSet.cs Tags.cs
  Stats/       StatId.cs Modifier.cs StatBlock.cs StatQuery.cs
  Data/        ContentDef.cs (todas as *Def autorais) GameDatabase.cs ContentCanonical.cs
  Difficulty/  Difficulty.cs MatchBalance.cs BalanceResolver.cs
  World/       World.cs PlayerStore.cs EnemyStore.cs ProjectileStore.cs GemStore.cs SpatialGrid.cs ArenaCollision.cs
  Match/       MatchConfig.cs PlayerInput.cs EventBuffer.cs MatchRules.cs TickClock.cs
  Systems/     MovementSystem.cs EnemyAISystem.cs SpawnDirector.cs CombatSystem.cs
  Abilities/   AbilitySystem.cs IAbilityBehavior.cs Behaviors/(Chain|Orbit|Projectile|Aura|Passive).cs SpecialSystem.cs
  Progression/ XpCurve.cs OfferGenerator.cs ProgressionSystem.cs RequirementEvaluator.cs AffinitySystem.cs SynergySystem.cs
  Codec/       ByteWriter.cs ByteReader.cs InputCodec.cs SnapshotCodec.cs RuleEventCodec.cs HelloCodec.cs InputValidator.cs
  Netcode/     InterpBuffer.cs ClientPrediction.cs
  Bots/        BotBrain.cs
Assets/_Project/Content/                asmdef ES.Content
  Data/sp1-content.json                 conteúdo canônico (fonte para SOs e para dotnet)
  Assets/      ContentDatabaseAsset.cs AbilityVisual.cs ...  Editor/ContentJsonSync.cs
Assets/_Project/Net/                    asmdef ES.Net
Assets/_Project/Game/                   asmdef ES.Game
Assets/_Project/UI/                     asmdef ES.UI (+ UXML/USS)
Assets/_Project/Tests/EditMode|PlayMode/
Assets/Plugins/WebGL/Visibility.jslib
Assets/WebGLTemplates/ES/
Tools/ES.Tools.sln  Tools/Sim/ES.Sim.csproj  Tools/SimTests/  Tools/BalanceRunner/  Tools/build-webgl.ps1  Tools/unity-tests.ps1
ArtSource/  AudioSource/  Hosting/  docs/
```

**Decisão de conteúdo:** a fonte de verdade é `sp1-content.json` (classes `*Def` serializáveis com campos públicos, compatíveis com `JsonUtility` e `System.Text.Json` com `IncludeFields`). Na Unity, `ContentJsonSync` importa o JSON para ScriptableObjects editáveis e exporta de volta; um teste EditMode garante que o JSON exportado é idêntico ao versionado. Os projetos dotnet leem o JSON. A Sim não faz parsing de JSON.

---

## Fase A — Simulação (dotnet, sem Unity)

### Task 1: Ferramentas e núcleo matemático

**Files:**
- Create: `Tools/ES.Tools.sln`, `Tools/Sim/ES.Sim.csproj`, `Tools/SimTests/ES.SimTests.csproj`, `global.json`
- Create: `Assets/_Project/Sim/ES.Sim.asmdef`, `Assets/_Project/Sim/Core/{Vec2,Rng,Quant}.cs`
- Test: `Tools/SimTests/Core/CoreTests.cs`

**Interfaces:**
- Produces: `struct Vec2 { float X, Y; +,-,*,/; float Length(); float LengthSq(); Vec2 Normalized(); static float Dot(Vec2,Vec2); static float DistanceSq(Vec2,Vec2); static Vec2 ClampLength(Vec2, float max) }`
- Produces: `struct Rng { Rng(ulong seed); uint NextUInt(); float NextFloat(); /*[0,1)*/ int Range(int minIncl, int maxExcl); float Range(float min, float max); }` (PCG32)
- Produces: `static class Quant { ushort Pos(float v, float min, float max); float UnPos(ushort q, float min, float max); sbyte Axis(float v); float UnAxis(sbyte q); }`

- [ ] **Step 1:** `ES.Sim.csproj`: `TargetFramework netstandard2.1`, `LangVersion 9`, `<Compile Include="../../Assets/_Project/Sim/**/*.cs" />`, `EnableDefaultCompileItems false`. `ES.SimTests.csproj`: `net10.0`, NUnit 4 + NUnit3TestAdapter + Microsoft.NET.Test.Sdk (versões estáveis com ≥ 2 semanas), referência a ES.Sim. `global.json`: SDK `10.0.402`, `rollForward latestFeature`. `ES.Sim.asmdef`: `{"name":"ES.Sim","noEngineReferences":true,"autoReferenced":true}`.
- [ ] **Step 2: Testes que falham**

```csharp
[Test] public void Rng_SameSeed_SameSequence() { var a = new Rng(42); var b = new Rng(42); for (int i=0;i<1000;i++) Assert.AreEqual(a.NextUInt(), b.NextUInt()); }
[Test] public void Rng_Range_StaysInBounds() { var r = new Rng(1); for (int i=0;i<10000;i++){ int v=r.Range(3,7); Assert.That(v, Is.InRange(3,6)); } }
[Test] public void Quant_Pos_RoundTripErrorWithin1Over1024() { for (float v=0; v<=64; v+=0.0137f) Assert.That(Math.Abs(Quant.UnPos(Quant.Pos(v,0,64),0,64)-v), Is.LessThanOrEqualTo(1f/1024)); }
[Test] public void Quant_Axis_ClampsAndRoundTrips() { Assert.AreEqual(127, Quant.Axis(2f)); Assert.That(Math.Abs(Quant.UnAxis(Quant.Axis(0.5f))-0.5f), Is.LessThan(0.01f)); }
[Test] public void Vec2_ClampLength_LimitsToMax() { Assert.That(Vec2.ClampLength(new Vec2(3,4),1).Length(), Is.EqualTo(1).Within(1e-5)); }
```

- [ ] **Step 3:** Rodar `dotnet test Tools/ES.Tools.sln` → FAIL (tipos inexistentes).
- [ ] **Step 4:** Implementar `Vec2`, `Rng` (PCG32: estado 64 bits, incremento ímpar fixo), `Quant` (16 bits linear; eixo em `sbyte` −127..127).
- [ ] **Step 5:** Rodar testes → PASS.
- [ ] **Step 6:** Commit `feat(sim): ferramentas dotnet e núcleo matemático`.

### Task 2: Tags e atributos

**Files:**
- Create: `Assets/_Project/Sim/Core/{Tags,TagSet}.cs`, `Assets/_Project/Sim/Stats/{StatId,Modifier,StatBlock,StatQuery}.cs`
- Test: `Tools/SimTests/Stats/StatTests.cs`

**Interfaces:**
- Produces: `enum Tag { Energia, Eletronica, Automacao, Software, Estruturas, Mecanica, Biomecanica, Quimica, Sensores, Robotica, Defesa, Suporte, Termico, Reacao, Drones, Maquina, Blindado, Organico }`; `static bool Tags.TryParse(string, out Tag)` (aceita o nome sem acento, sem diferenciar maiúsculas).
- Produces: `struct TagSet { ulong Bits; static TagSet Of(params Tag[]); bool Has(Tag); bool Overlaps(TagSet); int Count; }`
- Produces: `enum StatId { MaxHp, MoveSpeed, Regen, PickupRadius, Armor, DamageTaken, Damage, Cooldown, Range, Area, Duration, ProjectileSpeed, Knockback, Slow, HitInterval, Count, Pierce, Chains, ChainRange, ChainFalloff }`
- Produces: `enum ModOp { Add, Mult }`; `enum ScopeKind { Player, AllAbilities, Tag, Ability }`; `struct ModScope { ScopeKind Kind; TagSet Tags; int AbilityIndex; }`; `struct Modifier { StatId Stat; ModOp Op; float Value; int SourceId; ModScope Scope; }`
- Produces: `class StatBlock { void SetBase(StatId, float); float GetBase(StatId); void Add(in Modifier); int RemoveSource(int sourceId); float Get(StatId); }` — final = (base + ΣAdd) × Π(1 + Mult).
- Produces: `static float StatQuery.Ability(StatBlock player, StatBlock ability, int abilityIndex, TagSet abilityTags, StatId stat)` — base da habilidade + modificadores da própria habilidade + modificadores do jogador cujo escopo casa (`AllAbilities`, `Tag` com sobreposição, `Ability` igual); mesma fórmula.

- [ ] **Step 1: Testes que falham**

```csharp
[Test] public void StatBlock_Formula_AddThenMultiplyProduct() { var s=new StatBlock(); s.SetBase(StatId.Damage,10); s.Add(Add(StatId.Damage,2)); s.Add(Mul(StatId.Damage,0.5f)); s.Add(Mul(StatId.Damage,0.1f)); Assert.AreEqual(12*1.5f*1.1f, s.Get(StatId.Damage), 1e-4); }
[Test] public void StatBlock_RemoveSource_RemovesOnlyThatSource() { /* fontes 1 e 2; RemoveSource(1) retorna 1; Get volta a refletir só a fonte 2 */ }
[Test] public void StatQuery_TagScope_AppliesOnlyToMatchingAbility() { /* Mult -0.06 em Cooldown com escopo Tag(Energia): ability Energia => 0.94x; ability Mecanica => 1x */ }
[Test] public void TagSet_OverlapsAndCount() { var t=TagSet.Of(Tag.Energia,Tag.Termico); Assert.IsTrue(t.Overlaps(TagSet.Of(Tag.Termico))); Assert.AreEqual(2,t.Count); }
[Test] public void Tags_TryParse_IgnoresCase() { Assert.IsTrue(Tags.TryParse("energia", out var t)); Assert.AreEqual(Tag.Energia,t); Assert.IsFalse(Tags.TryParse("plasma", out _)); }
```

- [ ] **Step 2:** Rodar → FAIL. **Step 3:** Implementar (armazenamento de modificadores em lista pré-alocada; `Get` sem alocação). **Step 4:** Rodar → PASS. **Step 5:** Commit `feat(sim): tags e sistema de atributos`.

### Task 3: Definições de conteúdo, banco de dados e hash

**Files:**
- Create: `Assets/_Project/Sim/Data/{ContentDef,GameDatabase,ContentCanonical}.cs`
- Create: `Assets/_Project/Content/Data/sp1-content.json`
- Create: `Tools/SimTests/Support/ContentLoader.cs` (System.Text.Json, `IncludeFields = true`)
- Test: `Tools/SimTests/Data/DatabaseTests.cs`

**Interfaces:**
- Produces (todas `[Serializable]`, campos públicos em camelCase, arrays em vez de listas):
  - `ContentDef { RulesDef rules; BalanceDef balance; SpawnDef spawn; ArenaDef[] arenas; SpecializationDef[] specializations; SpecialDef[] specials; AbilityDef[] abilities; EnemyDef[] enemies; SynergyDef[] synergies; EvolutionDef[] evolutions; }`
  - `RulesDef { float matchSeconds=600, tickRate=30; float playerMaxHp=100, playerSpeed=4.5f, playerPickup=1.6f, hitInvuln=0.5f; float xpA=5, xpB=5, xpC=0.6f; int maxWeapons=6, maxComponents=6; float coopChoiceSeconds=12, choiceInvuln=2; float affinityInitial=0.04f, affinityIntermediate=0.04f; int affinityIntermediateLevel=4; int capEnemies=400, capGems=150, capHostileProjectiles=200, capCosmeticEvents=48; float survivalHpThreshold=0.5f; float eliteHp=3, eliteSize=1.3f, eliteDamage=1.25f, eliteXp=4, eliteHealChance=0.25f; float healAmountFraction=0.3f; }`
  - `AbilityDef { string id, name, description, kind /*weapon|component|attribute|survival*/, rarity /*comum|incomum|raro*/, behavior /*chain|orbit|projectile|aura|passive|attribute|heal*/; string[] tags; int maxLevel; StatValueDef[] baseStats; LevelDef[] levels; ModifierDef[] grants; /* passivos/atributos: modificadores por nível */ }`
  - `StatValueDef { string stat; float value; }`; `LevelDef { StatValueDef[] add; StatValueDef[] mult; string text; }`; `ModifierDef { string stat, op /*add|mult*/, scope /*player|all|tag:<Tag>|ability:<id>*/; float value; }`
  - `SpecializationDef { string id, name, startingAbility, special, portrait; string[] affinityTags; ModifierDef[] advancedAffinity; string advancedAffinityText; }`
  - `SpecialDef { string id, name, kind /*blackout|turbine*/, description; float cooldown, radius, expandSeconds, damage, stunSeconds, duration, moveBonus, damageBonus, damageTakenMult, ramDamage, ramKnockback; string stunTag; }`
  - `EnemyDef { string id, name, role, behavior /*chase|kite_shoot*/; string[] tags; float hp, speed, contactDamage, contactInterval, radius, xp, cost, knockbackResist, keepMin, keepMax, fireInterval, telegraph, projectileSpeed, projectileDamage; }`
  - `SynergyDef { string id, name, description, effect /*charge_every_n*/, targetAbility; RequirementDef[] requires; float n, damageMult, extraChains; }`
  - `EvolutionDef { string id, name, description, fromAbility, resultAbility; RequirementDef[] requires; bool discoverable; }`
  - `RequirementDef { string kind /*ability_level|tag_count|specialization|stat_at_least|branch*/, target; float value; }`
  - `BalanceDef { DifficultyProfileDef[] difficulties /*easy,normal,hard*/; TeamProfileDef[] teams /*1..4*/; CellOverrideDef[] overrides; }` — campos conforme Task 4.
  - `SpawnDef { float baseBudget=1.2f, rampPerMinute=0.55f, surgeIntervalSeconds=60; int surgeCount=24; float spawnPadding=2; BracketDef[] brackets; }`; `BracketDef { float fromMinute; EnemyWeightDef[] weights; }`; `EnemyWeightDef { string enemyId; float weight; }`
  - `ArenaDef { string id, name; float width, height; RectDef[] obstacles; }`; `RectDef { float x, y, w, h; }`
- Produces: `static BuildResult GameDatabase.Build(ContentDef def)`; `BuildResult { GameDatabase Db; string[] Errors; }`; `GameDatabase { ContentDef Def; int AbilityIndex(string id) /* -1 se não existe */; ResolvedAbility Ability(int i); int AbilityCount; ... Enemy/Specialization/Special/Synergy/Evolution equivalentes; ulong ContentHash; }` — `ResolvedAbility` contém `TagSet Tags`, `StatBlock BaseStats` pronto e níveis resolvidos para `StatId`.
- Produces: `static ulong ContentCanonical.Hash(ContentDef def)` — FNV-1a 64 sobre escrita binária em ordem fixa de campos (strings UTF-8 com comprimento; floats pelos bits `BitConverter.SingleToInt32Bits`); nunca via `float.ToString`.

- [ ] **Step 1:** Escrever `sp1-content.json` com **todos** os valores da spec §6 e §7 (especializações, especiais, 4 habilidades + 2 iniciais + 2 resultados de evolução + 5 atributos + Kit, 3 inimigos, sinergia, 2 evoluções, arena 64×64 com ~8 obstáculos, spawn §6.5, balance §7.1/§7.2).
- [ ] **Step 2: Testes que falham**

```csharp
[Test] public void Build_Sp1Content_HasNoErrors() { var r=GameDatabase.Build(ContentLoader.LoadSp1()); CollectionAssert.IsEmpty(r.Errors); }
[Test] public void Build_DuplicateAbilityId_ReportsError() { /* duplica descarga_direcionada → Errors contém "duplicado: ability descarga_direcionada" */ }
[Test] public void Build_EvolutionPointsToMissingAbility_ReportsError() { /* resultAbility "inexistente" → erro citando o id */ }
[Test] public void Build_UnknownTag_ReportsError() { /* tags ["plasma"] → erro */ }
[Test] public void Hash_IsStable_AndChangesWithValues() { var d=ContentLoader.LoadSp1(); var h1=ContentCanonical.Hash(d); Assert.AreEqual(h1, ContentCanonical.Hash(ContentLoader.LoadSp1())); d.enemies[0].hp+=1; Assert.AreNotEqual(h1, ContentCanonical.Hash(d)); }
[Test] public void Sp1_SpecValues_Spot() { var db=GameDatabase.Build(ContentLoader.LoadSp1()).Db; var e=db.Def.enemies.Single(x=>x.id=="prensa_ambulante"); Assert.AreEqual(70,e.hp); Assert.AreEqual(600, db.Def.rules.matchSeconds); }
```

- [ ] **Step 3:** Rodar → FAIL. **Step 4:** Implementar `Build` (valida ids únicos, referências, tags, stats e escopos conhecidos; resolve strings para índices/enum). **Step 5:** Rodar → PASS. **Step 6:** Commit `feat(sim): definições de conteúdo, banco e hash canônico`.

### Task 4: Balanceamento central (12 perfis)

**Files:**
- Create: `Assets/_Project/Sim/Difficulty/{Difficulty,MatchBalance,BalanceResolver}.cs`; adicionar campos a `BalanceDef` em `ContentDef.cs`
- Test: `Tools/SimTests/Difficulty/BalanceTests.cs`

**Interfaces:**
- Produces: `enum Difficulty { Easy, Normal, Hard }`
- `DifficultyProfileDef { string id; float enemyDamage, enemyHp, spawnBudget, spawnRamp, compositionShiftMinutes, xp, healDropChance, regen, eliteChance, telegraph; }` (`compositionShiftMinutes`, `healDropChance`, `eliteChance` são **(+)**)
- `TeamProfileDef { int size; float enemyCount, enemyHp, eliteChance /*(+)*/, xpPerGem; }`
- `CellOverrideDef { string difficulty; int teamSize; string param; float value; }`
- Produces: `struct MatchBalance { Difficulty Difficulty; int TeamSize; float EnemyDamage, EnemyHp, SpawnBudget, SpawnRamp, CompositionShiftMinutes, Xp, HealDropChance, Regen, EliteChance, Telegraph, EnemyCount; }` e `static MatchBalance BalanceResolver.Resolve(BalanceDef def, Difficulty d, int teamSize)` (lança `ArgumentOutOfRangeException` fora de 1..4). `Xp` = xp da dificuldade × xpPerGem da equipe.

- [ ] **Step 1: Testes que falham**

```csharp
[Test] public void Resolve_All12Cells_Exist() { foreach (Difficulty d in Enum.GetValues(typeof(Difficulty))) for (int n=1;n<=4;n++) Assert.DoesNotThrow(()=>BalanceResolver.Resolve(Def,d,n)); }
[Test] public void EnemyDamage_HardGtNormalGtEasy() { for (int n=1;n<=4;n++){ var e=R(Easy,n).EnemyDamage; var m=R(Normal,n).EnemyDamage; var h=R(Hard,n).EnemyDamage; Assert.That(h>m && m>e); } }
[Test] public void EnemyHpPerPlayer_IsSublinear() { for (int n=2;n<=4;n++) Assert.Less(R(Normal,n).EnemyHp/n, R(Normal,n-1).EnemyHp/(n-1)); }
[Test] public void Spec_Normal4_Values() { var b=R(Normal,4); Assert.AreEqual(2.2f,b.EnemyCount,1e-5); Assert.AreEqual(1.75f,b.EnemyHp,1e-5); Assert.AreEqual(0.025f+0.03f,b.EliteChance,1e-5); Assert.AreEqual(0.55f,b.Xp,1e-5); }
[Test] public void CellOverride_MultAndAdd() { /* override Hard/3 enemyHp=1.1 → hp x1.1; override eliteChance=0.01 → +0.01 */ }
[Test] public void TeamSizeOutOfRange_Throws() { Assert.Throws<ArgumentOutOfRangeException>(()=>R(Normal,5)); }
```

- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): resolvedor central de balanceamento`.

### Task 5: Mundo, grade espacial e colisão com a arena

**Files:**
- Create: `Assets/_Project/Sim/World/{World,PlayerStore,EnemyStore,ProjectileStore,GemStore,SpatialGrid,ArenaCollision}.cs`, `Assets/_Project/Sim/Match/{MatchConfig,PlayerInput,EventBuffer}.cs`
- Test: `Tools/SimTests/World/GridTests.cs`, `ArenaTests.cs`

**Interfaces:**
- Produces: `class MatchConfig { ulong Seed; Difficulty Difficulty; int TeamSize; string ArenaId; PlayerSlot[] Roster; }`; `struct PlayerSlot { string Name; string SpecializationId; }`
- Produces: `struct PlayerInput { ushort Seq; sbyte MoveX, MoveY; byte SpecialCounter; short ChoiceOfferId; sbyte ChoiceIndex; /* -1 = nenhuma */ }`
- Produces: stores SoA com `Count` e arrays públicos. `EnemyStore { ushort[] Id; Vec2[] Pos; Vec2[] Vel; byte[] Type; byte[] Flags /*bit0 elite, bit1 atordoado, bit2 piscar*/; float[] Hp, MaxHp, StunUntil, NextContact; int Count; int Spawn(...); void RemoveAt(int i) /*swap-remove*/; }`. `PlayerStore` por slot (0..3): `Vec2 Pos, Vel; float Hp; PlayerStatus Status /*Active, Downed, Inactive*/; StatBlock Stats; ...`. Análogos para projéteis hostis e gemas.
- Produces: `class SpatialGrid { SpatialGrid(float width, float height, float cell=2); void Clear(); void Insert(int index, Vec2 pos); int Query(Vec2 center, float radius, int[] results) /*retorna quantidade; sem alocação*/; }`
- Produces: `static Vec2 ArenaCollision.Resolve(Vec2 pos, float radius, ArenaDef arena)` — empurra o círculo para fora de obstáculos e para dentro das bordas.
- Produces: `class EventBuffer { List<RuleEvent> Rule; List<CosmeticEvent> Cosmetic; void Clear(); }`; `struct RuleEvent { RuleEventKind Kind; int A, B, C; float X, Y; }`; `enum RuleEventKind { EnemyDied, GemSpawned, GemCollected, LevelUpOffer, ChoiceApplied, SynergyChanged, EvolutionApplied, SpecialActivated, PlayerDowned, PlayerInactive, MatchEnded }`; `struct CosmeticEvent { CosmeticKind Kind; int A, B; float X, Y, X2, Y2, Value; }`; `enum CosmeticKind { Damage, ChainArc, ProjectileFired, Hit, EnemyTelegraph, WaveStarted }` — `Cosmetic` limitado a `rules.capCosmeticEvents` (excedente descartado).
- Produces: `class World { World(GameDatabase db, MatchConfig cfg); GameDatabase Db; MatchBalance Balance; ArenaDef Arena; int Tick; float Elapsed; MatchPhase Phase /*Running, Victory, Defeat*/; PlayerStore Players; EnemyStore Enemies; ProjectileStore Hostile; GemStore Gems; EventBuffer Events; Rng Rng; void Step(PlayerInput[] inputs); }` — `Step` chama os sistemas das Tasks 6–13 em ordem da spec §5.1 (inicialmente vazio além de `Tick++`).

- [ ] **Step 1: Testes que falham**

```csharp
[Test] public void Grid_Query_MatchesBruteForce() { /* 500 pontos aleatórios (Rng 7); 200 consultas; conjunto de índices dentro do raio == força bruta */ }
[Test] public void Grid_Query_NoAllocation() { /* GC.GetAllocatedBytesForCurrentThread antes/depois de 1000 Query == 0 */ }
[Test] public void Arena_PushesOutOfObstacle() { /* círculo r=0.5 dentro de RectDef(10,10,4,2) → posição final sem sobreposição */ }
[Test] public void Arena_KeepsInsideBounds() { var p=ArenaCollision.Resolve(new Vec2(-5,70),0.5f,Arena64); Assert.That(p.X>=0.5f && p.Y<=63.5f); }
[Test] public void World_New_SpawnsRosterPlayersActive() { /* TeamSize 3 → 3 jogadores Active com vida 100 perto do centro */ }
[Test] public void EventBuffer_CosmeticCappedAt48() { /* adicionar 100 → Count == 48 */ }
```

- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): mundo, grade espacial e colisão de arena`.

### Task 6: Movimento do jogador e validação de entrada

**Files:**
- Create: `Assets/_Project/Sim/Systems/MovementSystem.cs`, `Assets/_Project/Sim/Codec/InputValidator.cs`
- Test: `Tools/SimTests/Systems/MovementTests.cs`, `Tools/SimTests/Codec/InputValidatorTests.cs`

**Interfaces:**
- Produces: `static Vec2 MovementSystem.Integrate(Vec2 pos, sbyte moveX, sbyte moveY, float speed, float dt, ArenaDef arena, float radius)` — **o mesmo código** usado pela previsão do cliente (Task 15).
- Produces: `class InputValidator { InputValidator(int maxPerSecond=60, int kickAfterMalformed=50); bool Accept(int slot, in PlayerInput input, double nowSeconds, out PlayerInput sanitized); void CountMalformed(int slot); bool ShouldKick(int slot); }` — limita vetor a comprimento 1, rejeita `Seq` antigo (comparação circular de `ushort`), mantém o maior `SpecialCounter` visto (contador que regride é ignorado), descarta acima de 60/s.

- [ ] **Step 1: Testes que falham** — `Integrate_FullInput_MovesAtSpeed` (1 s de ticks a 4,5 u/s ≈ 4,5 u ± 0,01), `Integrate_DiagonalNotFaster`, `Integrate_StopsAtWall`, `Validator_ClampsOversizedVector` (127,127 → comprimento ≤ 1), `Validator_RejectsOldSeq_WithWraparound` (65535 → 0 é novo; 10 após 20 é velho), `Validator_IgnoresRegressingSpecialCounter`, `Validator_RateLimit60PerSecond` (100 entradas no mesmo segundo → 60 aceitas), `Validator_KickAfter50Malformed`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar; `World.Step` aplica entradas validadas e chama `Integrate` para jogadores `Active` com velocidade `Stats.Get(MoveSpeed)`. **Step 4:** PASS. **Step 5:** Commit `feat(sim): movimento e validação de entrada`.

### Task 7: Inimigos e diretor de surgimento

**Files:**
- Create: `Assets/_Project/Sim/Systems/{EnemyAISystem,SpawnDirector}.cs`
- Test: `Tools/SimTests/Systems/SpawnTests.cs`, `EnemyAITests.cs`

**Interfaces:**
- Produces: `static float SpawnDirector.Budget(SpawnDef s, in MatchBalance b, float elapsedSeconds)` = `(baseBudget + rampPerMinute · b.SpawnRamp · t_min) · b.SpawnBudget · b.EnemyCount`.
- Produces: `static BracketDef SpawnDirector.BracketAt(SpawnDef s, float elapsedMinutes, float shiftMinutes)` — usa `t + shift` (shift positivo = antecipa).
- Produces: `static void SpawnDirector.Step(World w, float dt)` — acumula orçamento, escolhe tipo por pesos (Rng do mundo), nasce em anel fora da visão (raio = meia diagonal da câmera 12 u + `spawnPadding`) ao redor de um jogador ativo aleatório, dentro da arena e fora de obstáculos; respeita `capEnemies`; elite com chance `b.EliteChance` (multiplicadores de `RulesDef`); surto a cada `surgeIntervalSeconds` com `surgeCount × b.EnemyCount` enxames (limitado pelo teto). Vida = `EnemyDef.hp × b.EnemyHp (× eliteHp)`.
- Produces: `static void EnemyAISystem.Step(World w, float dt)` — `chase`: persegue o jogador ativo mais próximo; `kite_shoot`: mantém `keepMin..keepMax`, dispara após `telegraph × b.Telegraph` (evento cosmético `EnemyTelegraph`) a cada `fireInterval`; separação leve via grade; inimigos a > 30 u de todos os jogadores atualizam IA a cada 3 ticks; atordoados não agem.

- [ ] **Step 1: Testes que falham** — `Budget_Formula_NormalSolo_At5Min` (= 1,2 + 0,55·5 = 3,95), `Budget_Hard4Players` (usa ramp 1,2, budget 1,25, count 2,2), `Bracket_ShiftHardAnticipates` (Hard em 3,0 min usa o bracket de 4 min), `Spawn_NeverExceedsCap` (simular 600 s Hard/4 com jogador imortal → `Count ≤ 400` sempre), `Spawn_OutsideCameraRadius` (todo inimigo nasce a > 12 u do jogador-alvo), `Spawn_EliteHasTripleHp`, `Composition_FirstTwoMinutesOnlySwarm`, `Kite_KeepsDistanceWindow` (após 5 s a torreta fica entre 5,5 e 7 u), `Stunned_DoesNotMove`, `FarEnemies_UpdateEvery3Ticks`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): inimigos e diretor de surgimento`.

### Task 8: Combate, gemas e cura

**Files:**
- Create: `Assets/_Project/Sim/Systems/CombatSystem.cs`
- Test: `Tools/SimTests/Systems/CombatTests.cs`

**Interfaces:**
- Produces: `static bool CombatSystem.DamageEnemy(World w, int enemyIndex, float amount, Vec2 knockDir, float knockback, int sourcePlayer, int abilityIndex)` — retorna `true` se matou; emite `Damage` cosmético; na morte: `EnemyDied`, gema (`GemSpawned`, valor = xp × elite), chance de cura (`b.HealDropChance`, ou `eliteHealChance` se elite).
- Produces: `static void CombatSystem.DamagePlayer(World w, int slot, float amount)` — aplica `EnemyDamage`, `Armor`, `DamageTaken`, invulnerabilidade `hitInvuln`; vida 0 → `Downed` + `PlayerDowned`.
- Produces: `static void CombatSystem.Step(World w, float dt)` — contato inimigo→jogador (`contactInterval`), projéteis hostis, coleta de gemas no raio `PickupRadius` (XP compartilhado: Task 11), fusão de gemas acima de `capGems` (funde a mais antiga na mais próxima), regeneração `Regen × b.Regen`.

- [ ] **Step 1: Testes que falham** — `Contact_RespectsInterval`, `Player_InvulnAfterHit`, `EnemyDamageScaledByBalance` (Easy 0,65 × 5 = 3,25), `Kill_SpawnsGemWithXp`, `EliteKill_GemX4`, `Gems_MergeAboveCap` (200 mortes → `Gems.Count ≤ 150` e soma de XP preservada), `Player_DownedAtZeroHp`, `HostileProjectile_HitsAndDespawns`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): combate, gemas e cura`.

### Task 9: Habilidades (comportamentos)

**Files:**
- Create: `Assets/_Project/Sim/Abilities/{AbilitySystem,IAbilityBehavior}.cs`, `Behaviors/{ChainBehavior,OrbitBehavior,ProjectileBehavior,AuraBehavior,PassiveBehavior}.cs`
- Test: `Tools/SimTests/Abilities/BehaviorTests.cs`

**Interfaces:**
- Produces: `class AbilityInstance { int AbilityIndex; int Level; float CooldownLeft; int FireCount; StatBlock Stats; object State; }`; jogadores têm `List<AbilityInstance> Abilities` (máx. 6 armas + 6 componentes).
- Produces: `interface IAbilityBehavior { void OnAcquire(World w, int slot, AbilityInstance a); void Tick(World w, int slot, AbilityInstance a, float dt); }`; `static IAbilityBehavior AbilitySystem.For(string behavior)`; `static void AbilitySystem.Step(World w, float dt)`; `static void AbilitySystem.Grant(World w, int slot, int abilityIndex)` e `LevelUp(World w, int slot, AbilityInstance a)` (aplica `LevelDef`; atributos/componentes mantêm **um** `Modifier` por entrada de `grants` com `SourceId` da instância e valor = `value × nível` — ex.: Bateria nível 2 = Mult −0,12, Turbina nível 3 = Mult +0,18).
- Comportamentos (valores via `StatQuery.Ability`):
  - `chain`: alvo inicial = inimigo mais próximo em `Range`; até `Chains` saltos para o mais próximo não atingido em `ChainRange`; dano × `(1 − ChainFalloff)^salto`; `Count` alvos iniciais (evolução Pulso: 3). Cosmético `ChainArc` por salto.
  - `orbit`: `Count` braços em `Range` (raio), período `Duration`; acerta cada inimigo no máximo a cada `HitInterval`; empurrão `Knockback`. Flag de dados `speedDamageScale` (Rotor): dano × (1 + escala × velocidade/velocidadeBase); braços dobram com `TurbineActive`.
  - `projectile`: dispara no inimigo mais próximo em `Range`; velocidade `ProjectileSpeed`; perfura `Pierce`; vida = alcance/velocidade. Cosmético `ProjectileFired`.
  - `aura`: a cada `HitInterval` aplica `Damage` e `Slow` a inimigos em `Area`.
  - `passive`: só modificadores (`grants`).

- [ ] **Step 1: Testes que falham** — `Chain_HitsNearestThenJumpsWithFalloff` (3 inimigos alinhados; danos 14, 11,9, 10,1), `Chain_RespectsChainRange`, `Chain_CooldownFromStats` (Bateria nível 2 → 1,0 × 0,88 s), `Orbit_HitIntervalPerTarget` (1 inimigo parado no raio: 2 s → 4 acertos), `Orbit_ArmsIncreaseAtLevels3And5`, `Projectile_PierceCount` (5 inimigos em linha, pierce 3 → 3 atingidos), `Aura_TicksAndSlows`, `Passive_TurbinaAddsMoveSpeed` (nível 3 → ×1,18), `Passive_LevelUp_ReplacesModifierNotStacks` (subir a Bateria de 1 para 2 deixa exatamente 1 modificador da instância).
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): comportamentos de habilidades`.

### Task 10: Habilidades especiais

**Files:**
- Create: `Assets/_Project/Sim/Abilities/SpecialSystem.cs`
- Test: `Tools/SimTests/Abilities/SpecialTests.cs`

**Interfaces:**
- Produces: `class SpecialState { int SpecialIndex; float CooldownLeft; float ActiveLeft; byte LastCounter; float WaveRadius; }` por jogador; `bool TurbineActive(int slot)`.
- Produces: `static void SpecialSystem.Request(World w, int slot, byte counter)` (ativa quando `counter` > último visto e recarga zerada; emite `SpecialActivated` e `WaveStarted`), `static void SpecialSystem.Step(World w, float dt)`.
  - `blackout` (Apagão): onda expande até `radius` em `expandSeconds`; cada inimigo atingido uma vez por ativação: `damage × Damage`, atordoa `stunSeconds` se tiver `stunTag` (`Maquina`); remove projéteis hostis dentro da onda.
  - `turbine` (Turbina de Emergência): por `duration`: modificadores temporários `MoveSpeed +0,45`, `Damage +0,30` em escopo `Tag(Mecanica)`, `DamageTaken × 0,7`; contato do jogador com inimigo causa `ramDamage` + `ramKnockback` (no máximo 1 vez por inimigo a cada 0,5 s). Modificadores removidos ao fim (via `SourceId`).

- [ ] **Step 1: Testes que falham** — `Blackout_DamagesAndStunsMachinesInRadius` (inimigo a 5 u recebe 35 e fica atordoado 2 s; a 7 u não), `Blackout_ClearsHostileProjectiles`, `Special_RespectsCooldown` (pedido aos 10 s com recarga 22 s ignorado), `Special_CounterRegressionIgnored`, `Special_LostPacket_NextCounterStillActivates` (contador 1→3 ativa uma vez), `Turbine_BuffsAndExpires` (velocidade ×1,45 durante 6 s, volta ao normal depois), `Turbine_RamDamagesOnContact`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): especiais Apagão e Turbina de Emergência`.

### Task 11: Progressão (XP, ofertas, escolhas)

**Files:**
- Create: `Assets/_Project/Sim/Progression/{XpCurve,OfferGenerator,ProgressionSystem}.cs`
- Test: `Tools/SimTests/Progression/ProgressionTests.cs`

**Interfaces:**
- Produces: `static int XpCurve.ToNext(RulesDef r, int level)` = `round(xpA + xpB·(L−1) + xpC·(L−1)²)`.
- Produces: `enum OptionKind { NewAbility, Upgrade, Evolution, Attribute, Survival }`; `struct OfferOption { OptionKind Kind; int Ref; /* índice de habilidade ou evolução */ }`; `class LevelUpOffer { short OfferId; OfferOption[] Options; float Deadline; }`; `PlayerProgress { int Level; float Xp; Queue<LevelUpOffer> Pending; }`.
- Produces: `static LevelUpOffer OfferGenerator.Generate(World w, int slot, short offerId)` — até 3 opções sem repetição, por pesos; respeita `maxLevel`, `maxWeapons`, `maxComponents`; evolução só se `RequirementEvaluator` (Task 12) aprova; Kit quando vida < `survivalHpThreshold` ou quando faltam opções; **nunca 0 opções**.
- Produces: `static void ProgressionSystem.AddSharedXp(World w, float amount)` (todos os `Active` recebem `amount × b.Xp`; cada nível novo gera oferta na fila e evento `LevelUpOffer`); `static bool ProgressionSystem.Choose(World w, int slot, short offerId, int index)` (valida oferta da frente da fila e índice; aplica; `ChoiceApplied`); `static void ProgressionSystem.Step(World w, float dt, bool coop)` (coop: vence prazo de `coopChoiceSeconds` → opção 0; aplica `choiceInvuln` ao abrir oferta). `bool World.PausedForChoice` = solo com oferta pendente (o `MatchRunner` não chama `Step` enquanto verdadeiro).

- [ ] **Step 1: Testes que falham** — `XpCurve_Values` (L1=5, L5=35, L10=99), `SharedXp_AllActivePlayersGain` (jogador `Downed` não ganha), `Offer_NoDuplicates_RespectsMax`, `Offer_ExhaustedPool_StillReturnsAtLeastOne` *(Review Focus 2)*, `MultiLevel_QueuesOffersInOrder` *(Review Focus 3: XP para 3 níveis → 3 ofertas; escolher a 1ª expõe a 2ª)*, `Choose_WrongOfferId_Rejected`, `Choose_IndexOutOfRange_Rejected`, `Coop_Timeout_AppliesFirstOption`, `Solo_PausedForChoice_True`, `Offer_SurvivalWhenLowHp`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): progressão, ofertas e escolhas`.

### Task 12: Afinidade, sinergias e evoluções

**Files:**
- Create: `Assets/_Project/Sim/Progression/{RequirementEvaluator,AffinitySystem,SynergySystem}.cs`
- Test: `Tools/SimTests/Progression/BuildSystemsTests.cs`

**Interfaces:**
- Produces: `static bool RequirementEvaluator.Met(World w, int slot, RequirementDef[] reqs)` — `ability_level` (habilidade `target` com nível ≥ `value`), `tag_count` (≥ `value` habilidades com tag `target`), `specialization`, `stat_at_least`, `branch` (id de ramo escolhido; no SP1 sempre falso — reservado ao SP2).
- Produces: `static void AffinitySystem.OnAbilityChanged(World w, int slot, AbilityInstance a)` — se tags da habilidade ∩ `affinityTags`: inicial `+affinityInitial` Mult em `Damage` (escopo `Ability`), intermediário no nível ≥ `affinityIntermediateLevel`, avançado no nível máximo aplica `advancedAffinity`. Fontes distintas e idempotentes.
- Produces: `static void SynergySystem.Evaluate(World w, int slot)` (liga/desliga; evento `SynergyChanged`); `charge_every_n`: a cada `n`-ésimo disparo de `targetAbility`, dano × (1 + `damageMult`) e +`extraChains`.
- Produces: `static void ProgressionSystem.ApplyEvolution(World w, int slot, int evolutionIndex)` — substitui a instância `fromAbility` por `resultAbility` (nível 1 do resultado, mantém slot), emite `EvolutionApplied`; descoberta registrada em `World.Discovered` (HashSet de índices de evolução).

- [ ] **Step 1: Testes que falham** — `Affinity_ThreeStages_Eletrica_Descarga` (0,04 ao adquirir; +0,04 no nível 4; +1 salto no nível 5), `Affinity_NotAppliedToNonMatchingTags`, `Synergy_CargaAcumulada_ActivatesAtBateria2_AndEvery4thIsAmplified`, `Synergy_DeactivatesWhenRequirementLost`, `Evolution_NotOfferedBeforeRequirements`, `Evolution_PulsoAmplificado_ReplacesDescarga` (3 alvos iniciais), `Evolution_RotorDeImpacto_IsDiscoverable_AndRecordedOnApply`, `HybridBuild_EletricaWithBracoGiratorio_Works` (sem afinidade, mas funcional).
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): afinidade, sinergias e evoluções`.

### Task 13: Regras de partida e relógio de ticks

**Files:**
- Create: `Assets/_Project/Sim/Match/{MatchRules,TickClock}.cs`
- Test: `Tools/SimTests/Match/MatchTests.cs`

**Interfaces:**
- Produces: `static void MatchRules.Step(World w)` — vitória quando `Elapsed ≥ matchSeconds` com ≥ 1 jogador `Active`; derrota quando nenhum `Active` (jogadores `Inactive` não contam); emite `MatchEnded(result)` uma vez.
- Produces: `static void MatchRules.SetInactive(World w, int slot)` — desconexão: status `Inactive`, ofertas pendentes descartadas, `PlayerInactive`; **balanceamento inalterado**.
- Produces: `class TickClock { TickClock(double tickRate=30, int maxTicksPerFrame=5); int Advance(double frameSeconds); double Alpha; }` — retorna ticks a executar; acúmulo além de `maxTicksPerFrame` é descartado.
- `World.Step` final: entradas → movimento → spawn → IA → habilidades/especiais → combate → progressão → sinergias → regras (spec §5.1).

- [ ] **Step 1: Testes que falham** — `Victory_At600s`, `Defeat_WhenAllDowned`, `Disconnect_LastActiveWithPendingOffer_NoSoftLock` *(Review Focus 4: 2 jogadores; o único `Active` tem oferta e desconecta → `Defeat`, fila vazia, sem exceção)*, `Disconnect_DoesNotChangeBalance`, `TickClock_LongFrame_CapsAt5Ticks` *(Review Focus 5: `Advance(120.0)` → 5; chamada seguinte com 1/30 → 1)*, `TickClock_NormalFrames_30PerSecond`, `World_Step_NoAllocations_AfterWarmup` (Normal/1, 200 inimigos, 300 ticks; alocação ≤ 0 bytes após aquecimento), `World_SameSeedSameInputs_SameState` (2 mundos, 900 ticks, posições iguais).
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): regras de partida e relógio de ticks`.

### Task 14: Codecs de rede

**Files:**
- Create: `Assets/_Project/Sim/Codec/{ByteWriter,ByteReader,InputCodec,SnapshotCodec,RuleEventCodec,HelloCodec}.cs`
- Test: `Tools/SimTests/Codec/CodecTests.cs`

**Interfaces:**
- Produces: `class ByteWriter { ByteWriter(int capacity); void U8/U16/I16/U32/U64/F32/Str(...); int Length; byte[] Buffer; void Reset(); }`; `struct ByteReader { ByteReader(byte[] data, int length); bool TryU8(out byte) ...; bool TryStr(int maxLen, out string); }` — leitura nunca lança; retorna `false` em dados curtos/malformados.
- Produces: `static void InputCodec.Write(ByteWriter, in PlayerInput)`, `static bool InputCodec.TryRead(ref ByteReader, out PlayerInput)` (8 bytes).
- Produces: `static int SnapshotCodec.Write(World w, List<byte[]> chunksOut, int maxChunkBytes = 1100)` — bloco 0: cabeçalho (`tick`, `chunkIndex`, `chunkCount`) + jogadores (posição 2×U16, velocidade 2×I8, vida U8 em %, flags, último `Seq` processado, recarga do especial U8 em décimos, nível U8, XP U16) + projéteis hostis; blocos seguintes: inimigos (7 B cada; elites +1 B de vida %) e cosméticos (≤ 48). Cada bloco autocontido.
- Produces: `class SnapshotView { int Tick; PlayerSnap[] Players; Dictionary<ushort, EnemySnap> Enemies; List<HostileSnap> Hostile; List<CosmeticEvent> Cosmetic; }`; `static bool SnapshotCodec.TryRead(byte[] chunk, int length, float arenaW, float arenaH, SnapshotView into)`.
- Produces: `RuleEventCodec.Write(List<RuleEvent>, LevelUpOffer lookup...)`/`TryRead` — `LevelUpOffer` leva as opções (tipo + ref) junto.
- Produces: `struct Hello { string BuildVersion; ulong ContentHash; string Name; string SpecializationId; }`; `HelloCodec` com nome saneado: remove caracteres de controle, corta em 16 caracteres, vazio → "Engenheiro(a)".

- [ ] **Step 1: Testes que falham** — `Input_RoundTrip`, `Input_TruncatedData_ReturnsFalse`, `Snapshot_RoundTrip_PositionsWithin1Over1024`, `Snapshot_300Enemies_ChunksUnder1100Bytes_AndAllPresent`, `Snapshot_EnemyRecordIs7Bytes` (tamanho com 100 vs 101 inimigos difere em 7), `Snapshot_ChunkAlone_DecodesItsEnemies` (descartar bloco 1 não corrompe bloco 2), `Snapshot_GarbageBytes_ReturnsFalse_NoThrow` (10 000 sequências aleatórias), `RuleEvents_RoundTrip_IncludingOfferOptions`, `Hello_SanitizesName` (`"\u0007Ana\nBeatriz Souza da Silva"` → `"AnaBeatriz Souza"`).
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): codecs de entrada, snapshot e eventos`.

### Task 15: Netcode puro do cliente (interpolação e previsão)

**Files:**
- Create: `Assets/_Project/Sim/Netcode/{InterpBuffer,ClientPrediction}.cs`
- Test: `Tools/SimTests/Netcode/ClientNetTests.cs`

**Interfaces:**
- Produces: `class InterpBuffer { InterpBuffer(double minDelay=0.1, double maxDelay=0.2); void Push(int tick, double arrivalTime); double Delay; bool TrySample(double now, out int fromTick, out int toTick, out float t); }` — atraso sobe com o jitter observado (máx. 0,2 s) e desce lentamente.
- Produces: `class ClientPrediction { ClientPrediction(RulesDef rules, ArenaDef arena); void Record(in PlayerInput input, float speed); Vec2 Predicted; void Reconcile(Vec2 serverPos, ushort lastProcessedSeq, float speed); Vec2 RenderOffset; }` — reaplica entradas não confirmadas com `MovementSystem.Integrate`; erro < 0,05 u ignorado; acima disso `RenderOffset` decai em 0,1 s.

- [ ] **Step 1: Testes que falham** — `Interp_SteadyArrivals_Delay100ms`, `Interp_Jitter_IncreasesDelayUpTo200ms`, `Prediction_NoLatency_MatchesServer`, `Prediction_ReplaysUnackedInputs` (servidor confirma seq 10 de 15 → previsão = servidor + 5 entradas), `Prediction_SmallError_Ignored`, `Prediction_LargeError_SmoothedOver100ms`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(sim): interpolação e previsão do cliente`.

### Task 16: Bots e executor de balanceamento

**Files:**
- Create: `Assets/_Project/Sim/Bots/BotBrain.cs`, `Tools/BalanceRunner/{ES.BalanceRunner.csproj,Program.cs}`
- Test: `Tools/SimTests/Bots/BotSmokeTests.cs`

**Interfaces:**
- Produces: `static PlayerInput BotBrain.Decide(World w, int slot, ushort seq)` — vetor de fuga ponderado dos inimigos a ≤ 6 u + atração para a gema mais próxima + repulsão das bordas; usa o especial quando ≥ 8 inimigos a ≤ 4 u; escolhe a opção pendente por heurística (evolução > nova arma com afinidade > melhoria > atributo).
- Produces: `BalanceRunner` CLI: `--minutes <n> --seeds <n> --out <csv>`; executa 12 combinações × 2 especializações × seeds; CSV com `difficulty,team,spec,seed,survivedSeconds,result,levelAvg,kills,damageTaken,peakEnemies,maxStepMs`.

- [ ] **Step 1: Teste que falha** — `Smoke_12Combos_2Specs_3Seeds_2Minutes_NoExceptions_CapsRespected` (72 partidas de 120 s em ticks acelerados; nenhuma exceção; `peakEnemies ≤ 400`; `Gems ≤ 150`).
- [ ] **Step 2:** FAIL. **Step 3:** Implementar `BotBrain` e o runner. **Step 4:** PASS; executar `dotnet run --project Tools/BalanceRunner -- --minutes 10 --seeds 3 --out Tools/BalanceRunner/reports/sp1-baseline.csv` e anexar um resumo ao `docs/TESTES.md` (valores marcados "não afinados"). **Step 5:** Commit `feat(sim): bots e executor de balanceamento`.

---

## Fase B — Projeto Unity e prova de rede

### Task 17: Projeto Unity na raiz e pacotes

**Files:**
- Create (Unity): `Assets/`, `Packages/manifest.json`, `ProjectSettings/` na raiz; asmdefs `ES.Content`, `ES.Net`, `ES.Game`, `ES.UI`, `ES.Tests.EditMode`, `ES.Tests.PlayMode`; `Tools/unity-tests.ps1`
- Modify: `.gitignore` (já cobre Unity)
- Remove: `Engineering Survivors/` (pasta do Hub) → Lixeira

**Interfaces:**
- Produces: projeto que compila em batchmode; `Tools/unity-tests.ps1 -Platform EditMode|PlayMode` retorna código ≠ 0 em falha e imprime o resumo do XML.

- [ ] **Step 1:** Confirmar que nenhum editor está aberto em `Engineering Survivors/`; se estiver, pedir ao autor para fechá-lo. Criar o projeto: `& $Unity -batchmode -createProject Z:\Dev\EngineeringSurvivors\_newproj -quit -logFile Logs/create.log`, mover `Assets/ Packages/ ProjectSettings/` para a raiz, apagar `_newproj`.
- [ ] **Step 2:** Fixar pacotes: para `com.unity.netcode.gameobjects`, `com.unity.services.multiplayer`, `com.unity.inputsystem`, `com.unity.render-pipelines.universal`, `com.unity.2d.sprite`, `com.unity.2d.tilemap`, `com.unity.test-framework`, escolher a maior versão estável cujo campo `unity` em `https://packages.unity.com/<pacote>` seja ≤ `6000.3` (registrar as versões escolhidas em `docs/COMO-RODAR.md`). Sem `com.unity.collab-proxy`.
- [ ] **Step 3:** `ProjectSettings.asset`: `cloudProjectId: cd18869f-06b2-4e03-bc30-32ecff7d27a2`, `organizationId: vitordonasan`, `projectName: Engineering Survivors 2026-10-24_17-43-24`, `productName: Engineering Survivors`, `companyName: Engineering Survivors`; WebGL: compressão Brotli, *Managed Stripping Level* High, *Run In Background* ligado, *Data Caching* ligado. URP com Renderer 2D.
- [ ] **Step 4:** Criar asmdefs (referências conforme spec §4.1) e um teste EditMode trivial `Sanity_SimAssemblyLoads` (cria `new Rng(1)`).
- [ ] **Step 5:** `Tools/unity-tests.ps1 -Platform EditMode` → PASS. Rodar `dotnet test` → PASS (os mesmos fontes compilam nos dois lados).
- [ ] **Step 6:** Mover `Engineering Survivors/` para a Lixeira (`Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(..., OnlyErrorDialogs, SendToRecycleBin)`); atualizar a lista de projetos do Hub para a raiz.
- [ ] **Step 7:** Commit `chore(unity): projeto 2D na raiz, pacotes e assemblies`.

### Task 18: Conteúdo na Unity (ScriptableObjects)

**Files:**
- Create: `Assets/_Project/Content/{ContentDatabaseAsset,AbilityAsset,SpecializationAsset,EnemyAsset,SpecialAsset,SynergyAsset,EvolutionAsset,RulesAsset,BalanceAsset,SpawnAsset,ArenaAsset}.cs`, `Assets/_Project/Content/Editor/ContentJsonSync.cs`
- Test: `Assets/_Project/Tests/EditMode/ContentTests.cs`

**Interfaces:**
- Consumes: `ContentDef`, `GameDatabase.Build`, `ContentCanonical.Hash` (Task 3).
- Produces: `class ContentDatabaseAsset : ScriptableObject { ContentDef ToContentDef(); GameDatabase BuildDatabase(); }` (cada `*Asset` tem `public <X>Def def;` + referências visuais por id: `Sprite icon`, etc.); menu `Engineering Survivors/Conteúdo/Importar JSON` e `.../Exportar JSON`.

- [ ] **Step 1: Testes que falham** — `Content_BuildsWithoutErrors`, `Content_ExportEqualsVersionedJson` (exportação canônica == `sp1-content.json` normalizado), `Content_HashEqualsDotnetHash` (hash dos SOs == hash do JSON lido via `JsonUtility`), `Content_AllAbilitiesHaveIcons` (após Task 21; marcado `[Ignore("até a arte")]` até lá).
- [ ] **Step 2:** FAIL. **Step 3:** Implementar e importar o JSON. **Step 4:** PASS. **Step 5:** Commit `feat(content): conteúdo como ScriptableObjects com sincronização JSON`.

### Task 19: Camada de rede (sessões, transportes, mensagens)

**Files:**
- Create: `Assets/_Project/Net/{SessionService,JoinCode,TransportSelector,NetMessages,MatchHost,MatchClient,HostVisibility}.cs`, `Assets/Plugins/WebGL/Visibility.jslib`
- Test: `Assets/_Project/Tests/EditMode/JoinCodeTests.cs`, `Assets/_Project/Tests/PlayMode/NetLoopTests.cs`

**Interfaces:**
- Consumes: `World`, `TickClock`, codecs (Tasks 13–15), `InputValidator` (Task 6).
- Produces: `static bool JoinCode.TryNormalize(string raw, out string code)` — remove espaços, maiúsculas, aceita só `[A-Z0-9]`, 6–8 caracteres.
- Produces: `class SessionService { Task SignInAsync(); Task<string> CreateAsync(int maxPlayers=4) /*retorna código*/; Task JoinAsync(string code); Task LockAsync(); Task LeaveAsync(); event Action<string> Error; }` — `CreateAsync` usa `SessionOptions { MaxPlayers = 4, IsPrivate = true }.WithRelayNetwork()` com `wss` em WebGL; mapeia exceções do serviço para chaves de string (`erro_codigo_invalido`, `erro_sala_cheia`, `erro_partida_iniciada`, `erro_conexao`).
- Produces: `static void TransportSelector.UseSinglePlayer(NetworkManager)` / `UseRelayWebSockets(NetworkManager)`.
- Produces: `NetMessages` (nomes `es.input`, `es.choose`, `es.snapshot`, `es.events`, `es.hello`, `es.welcome`, `es.reject`, `es.start`, `es.hostbg`) via `CustomMessagingManager`; entrega: input/snapshot `UnreliableSequenced`, demais `ReliableSequenced`.
- Produces: `class MatchHost : MonoBehaviour { void Begin(MatchConfig cfg, GameDatabase db); World World; }` — `TickClock` + `World.Step` + `SnapshotCodec` a cada 2 ticks + eventos de regra; valida `Hello` (versão `Application.version` + `ContentHash`; sala não iniciada; < 4).
- Produces: `class MatchClient : MonoBehaviour { SnapshotView Latest; InterpBuffer Interp; ClientPrediction Prediction; event Action<RuleEvent> OnRuleEvent; void SendInput(in PlayerInput); void SendChoice(short offerId, int index); }` — no host, o cliente local lê o `World` diretamente (sem rede) pelo mesmo contrato.
- Produces: `HostVisibility` — `.jslib` chama `SendMessage` em `visibilitychange`; host envia `es.hostbg`.

- [ ] **Step 1: Testes que falham** — EditMode: `JoinCode_NormalizesSpacesAndCase` *(Review Focus 1: " f6gplw " → "F6GPLW")*, `JoinCode_RejectsInvalidChars` ("AB-12!" → false), `JoinCode_RejectsWrongLength`. PlayMode (dois `NetworkManager` no mesmo processo, transporte UTP local): `Hello_VersionMismatch_Rejected`, `Client_InputMovesPlayerOnHost`, `Client_ReceivesEnemiesInSnapshot`, `Client_ChoiceAppliedAndBroadcast`, `Special_ActivatesViaCounter`, `MatchEnd_ReachesClient`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** `Tools/unity-tests.ps1` EditMode e PlayMode → PASS. **Step 5:** Commit `feat(net): sessões, transportes e mensagens`.

### Task 20: Prova de rede WebGL (risco R1) — PONTO DE DECISÃO

**Files:**
- Create: `Assets/_Project/Game/Bootstrap/NetProofScene` (cena mínima: botões Criar/Entrar, campo de código, quadrados coloridos para jogadores e inimigos), `Tools/build-webgl.ps1`, `Tools/serve-local.ps1`
- Modify: `docs/TESTES.md`

- [ ] **Step 1:** `Tools/build-webgl.ps1 -Scene NetProof -Out Build/WebGL-proof` (batchmode, `BuildPipeline.BuildPlayer` via método `ES.Game.Editor.BuildScripts.BuildWebGL`).
- [ ] **Step 2:** Servir localmente (`Tools/serve-local.ps1`, servidor Node estático com cabeçalhos Brotli corretos) e abrir **duas abas** no navegador do Claude: aba 1 cria sala (código exibido), aba 2 entra pelo código.
- [ ] **Step 3:** Verificar: os dois quadrados de jogador se movem nas duas abas; inimigos aparecem iguais nas duas; console sem erros; painel mostra bytes/s. Registrar resultado em `docs/TESTES.md` com data, versões de pacotes e evidência (capturas).
- [ ] **Step 4:** **Se falhar**, parar a Fase C e reportar ao autor com as alternativas da spec §11 R1. **Se passar**, commit `test(net): prova de rede WebGL via Relay aprovada`.

---

## Fase C — Jogo apresentável e publicação

### Task 21: Pipeline de pixel art (diretriz 32×32)

**Files:**
- Create: `ArtSource/{package.json,build.mjs}`, `ArtSource/lib/{palette,pixel,png,characters,enemies,tiles,effects,icons,portraits}.mjs`, saída em `Assets/_Project/Art/{Characters,Enemies,Bosses,Weapons,Abilities,Effects,Tilesets,Environment,UI,Portraits}/`, `Assets/_Project/Art/ANIMACOES.md`, `Assets/_Project/Art/manifest.json`
- Remove: a arte vetorial anterior (SVG → PNG)
- Test: `ArtSource/test/build.test.mjs` (node:test)

**Interfaces:**
- Produces: `npm --prefix ArtSource run build` grava PNGs pixel a pixel (sem dependências de raster); `manifest.json` com `{ id, category, file, w, h, pivot:[px,py] (em pixels inteiros), border }`.
- Produces: quadros de personagem `char_<spec>_<anim>_<dir>_<n>` (dir ∈ down/up/left/right; anims sem direção usam `down`), 32×32, para as 7 especializações: mecatronica, computacao, eletrica, civil, mecanica, biomedica, quimica.

- [ ] **Step 1: Testes que falham** — todo sprite com o tamanho declarado; personagens 32×32; **alfa só 0 ou 255** em personagens, inimigos, tiles, ícones e retratos; cores de personagens e inimigos dentro da paleta; base dos pés na mesma linha em todos os quadros de pé; 7 especializações × conjunto completo de animações; `right` = espelho exato de `left`; silhuetas (máscara de alfa do `idle_down_0`) das 7 engenharias diferem duas a duas em ≥ 12 px; retratos 64×64 × 9 emoções × 7; ícones para todas as habilidades e especiais do conteúdo; build determinística; `ANIMACOES.md` lista cada personagem e animação.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar codificador PNG próprio (zlib do Node), mapas de pixels ASCII por vista (frente/costas/lado) e camadas por especialização; animações por deslocamento/substituição de pixels e rotação exata de 90° (queda). **Step 4:** PASS; folha de contato ampliada por fator inteiro (vizinho mais próximo) e revisão visual. **Step 5:** Unity: importador com filtro Point, sem compressão, sem mipmaps, PPU 32, pivôs do manifesto; ativar `Content_AllAbilitiesHaveIcons`. **Step 6:** Commit `feat(art): pixel art 32×32 nativo`.

### Task 22: Pipeline de áudio

**Files:**
- Create: `AudioSource/{package.json,synth.mjs,sfx.json,music.mjs}`, saída `Assets/_Project/Audio/{Sfx,Music}/*.wav`, `Assets/_Project/Audio/ES.mixer`
- Test: `AudioSource/test/synth.test.mjs`

- [ ] **Step 1: Teste que falha** — `cada efeito de sfx.json gera WAV 44,1 kHz mono com pico ≤ −1 dBFS e duração declarada`; `loop de música tem duração múltipla exata do compasso (sem clique na emenda: primeira e última amostra ≈ 0)`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar osciladores (seno, quadrada, dente de serra, ruído), envelopes ADSR, filtro passa-baixa/alta, distorção leve; ~15 efeitos (descarga, arco, impacto metálico, braço giratório, laser, campo, explosão pequena, apagão, turbina, dano no jogador, gema, subida de nível, evolução, alarme de surto, vitória, derrota, clique de UI) e 1 loop industrial. **Step 4:** PASS. **Step 5:** Unity: AudioMixer (Música, Efeitos, Interface), `SfxPlayer` com limite de vozes por som (4). **Step 6:** Commit `feat(audio): síntese de efeitos e música do SP1`.

### Task 23: Apresentação do jogo

**Files:**
- Create: `Assets/_Project/Game/{GameBootstrap,MatchRunner,ViewPool,EnemyViews,PlayerViews,ProjectileViews,GemViews,FxSystem,CameraRig,ArenaBuilder,DamageNumbers,PerfOverlay,SettingsStore}.cs`, cena `Assets/_Project/Scenes/Main.unity`
- Test: `Assets/_Project/Tests/PlayMode/SoloFlowTests.cs`

**Interfaces:**
- Consumes: `MatchHost`/`MatchClient` (Task 19), arte/áudio (Tasks 21–22).
- Produces: `class ViewPool<T> where T : Component { T Get(); void Release(T); }`; `SettingsStore { float Master, Music, Sfx, Shake, UiScale; EffectsLevel Effects; string SpecialBinding; bool Fullscreen; void Load(); void Save(); }` (PlayerPrefs).
- Pixel perfect: `UnityEngine.Rendering.Universal.PixelPerfectCamera` (referência 640×360, PixelSnapping), AA/MSAA/HDR desligados; arcos elétricos como carimbos de sprites pixelados (sem LineRenderer suave).
- Requisitos: visuais por ID com pool; interpolação para inimigos/projéteis; jogador local previsto; câmera segue jogador local (caído → segue aliado); `ChainArc` com `LineRenderer` tremulante; braços/aura derivados do estado; números de dano limitados a 64; luzes 2D e partículas conforme `EffectsLevel`; tremor de câmera × `Shake`; painel F3 (FPS, ms, entidades, bytes/s, latência); retrato muda por evento (vida < 30% → medo; evolução → determinação; vitória/derrota).

- [ ] **Step 1: Testes que falham** — `Solo_StartsWithoutSession_UsesSinglePlayerTransport`, `Solo_LevelUp_PausesSimulation`, `Solo_ChoiceUpdatesHud`, `ViewPool_ReusesInstances`, `Perf_400Enemies_RunsAtLeast45Fps_InEditor` (aviso, não falha, no Editor; a medição oficial é no navegador).
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS + verificação visual no Editor. **Step 5:** Commit `feat(game): visuais, câmera, efeitos e solo jogável`.

### Task 24: Interface (UI Toolkit)

**Files:**
- Create: `Assets/_Project/UI/{Uxml,Uss}/*`, `Assets/_Project/UI/{UiRouter,MainMenuController,SoloSetupController,CoopController,LobbyController,HudController,LevelUpController,PauseController,ResultController,SettingsController,CreditsController,Strings}.cs`, `Assets/_Project/UI/Strings/pt-BR.json`
- Test: `Assets/_Project/Tests/EditMode/UiTests.cs`, `Assets/_Project/Tests/PlayMode/UiFlowTests.cs`

**Interfaces:**
- Produces: `static string Strings.Get(string key)` (chave ausente → `"[chave]"` e erro no log); telas e textos da spec §9 e mensagens da §8.7.
- Lobby: código + copiar, lista com especialização, contagem "N/4", dificuldade (só o host altera; os demais veem), aviso de equipe, Iniciar (host). Entrada tardia bloqueada.
- Configurações persistidas; reatribuição da tecla do especial via Input System (`PerformInteractiveRebinding`), salva em `SettingsStore`.

- [ ] **Step 1: Testes que falham** — EditMode: `AllUxmlButtons_HaveHandlers` (todo `Button` com `name` nas UXML é registrado por algum controlador — nenhum botão sem função), `AllStringKeysExist` (toda chave usada nas UXML/código existe em `pt-BR.json`), `ErrorMessages_MatchSpec` (5 mensagens da §8.7 com texto exato). PlayMode: `Menu_Solo_Start_ReachesHud`, `LevelUp_CardsShowNameRarityTagsAndSynergyHint`, `Settings_SpecialRebind_Persists`.
- [ ] **Step 2:** FAIL. **Step 3:** Implementar. **Step 4:** PASS. **Step 5:** Commit `feat(ui): menus, lobby, HUD e configurações`.

### Task 25: Build, publicação e documentação

**Files:**
- Create: `Assets/WebGLTemplates/ES/{index.html,style.css,loader-ui.js}`, `Hosting/_headers`, `Tools/deploy.ps1`, `docs/COMO-RODAR.md`, `docs/LIMITACOES.md`, `CREDITS.md`, `README.md`; fonte `Assets/_Project/UI/Fonts/ChakraPetch-*.ttf` (OFL, Google Fonts)
- Modify: `docs/TESTES.md`

- [ ] **Step 1:** Template WebGL: barra de progresso, aviso de navegador sem WebGL2, mensagens amigáveis para erros de carregamento. `Hosting/_headers`: `*.br` com `Content-Encoding: br` e `Content-Type` correto para `.wasm.br`, `.js.br`, `.data.br`.
- [ ] **Step 2:** Build final (`Tools/build-webgl.ps1 -Scene Main -Out Build/WebGL`); verificar tamanho comprimido ≤ 25 MB (registrar).
- [ ] **Step 3:** Publicação: pedir ao autor o login (`npx wrangler@<versão fixada> login`); `Tools/deploy.ps1` publica no Cloudflare Pages (projeto `engineering-survivors`). Se arquivo > 25 MiB, usar o plano B (GitHub Pages + Decompression Fallback). Só anunciar a URL após abri-la e jogar.
- [ ] **Step 4:** Testes reais (spec §12.4): 2 abas via URL pública (Claude) — criar/entrar, mover, inimigos sincronizados, subida de nível, especial, fim de partida; medir FPS com 300 e 400 inimigos (comando de depuração `F4` gera inimigos), memória e tamanho; registrar em `docs/TESTES.md` com o roteiro de 2 computadores para o autor.
- [ ] **Step 5:** Escrever `COMO-RODAR.md` (abrir na Unity, testes, build, publicação, atualização dos serviços), `LIMITACOES.md` (spec §15 + o que os testes revelarem), `CREDITS.md`, `README.md`.
- [ ] **Step 6:** Verificar os 8 critérios da spec §14; os itens que dependem do autor ficam marcados "pendente: autor".
- [ ] **Step 7:** Commit `chore(release): build WebGL, publicação e documentação do SP1`.
