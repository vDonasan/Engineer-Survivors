using System.Collections.Generic;

namespace ES.Sim
{
    public sealed class BuildResult
    {
        /// <summary>Null quando há erros.</summary>
        public GameDatabase Db;
        public string[] Errors;
    }

    /// <summary>
    /// Conteúdo validado e resolvido (strings → índices, tags, stats). Imutável
    /// depois de construído; compartilhado por host, clientes, testes e bots.
    /// </summary>
    public sealed class GameDatabase
    {
        public ContentDef Def { get; private set; }
        public ulong ContentHash { get; private set; }
        public ResolvedBracket[] Brackets { get; private set; }

        ResolvedAbility[] _abilities;
        ResolvedSpecialization[] _specializations;
        ResolvedSpecial[] _specials;
        ResolvedEnemy[] _enemies;
        ResolvedSynergy[] _synergies;
        ResolvedEvolution[] _evolutions;

        Dictionary<string, int> _abilityIds, _specializationIds, _specialIds, _enemyIds, _synergyIds, _evolutionIds, _arenaIds;

        public int AbilityCount => _abilities.Length;
        public int SpecializationCount => _specializations.Length;
        public int SpecialCount => _specials.Length;
        public int EnemyCount => _enemies.Length;
        public int SynergyCount => _synergies.Length;
        public int EvolutionCount => _evolutions.Length;

        public ResolvedAbility Ability(int i) => _abilities[i];
        public ResolvedSpecialization Specialization(int i) => _specializations[i];
        public ResolvedSpecial Special(int i) => _specials[i];
        public ResolvedEnemy Enemy(int i) => _enemies[i];
        public ResolvedSynergy Synergy(int i) => _synergies[i];
        public ResolvedEvolution Evolution(int i) => _evolutions[i];

        public int AbilityIndex(string id) => Find(_abilityIds, id);
        public int SpecializationIndex(string id) => Find(_specializationIds, id);
        public int SpecialIndex(string id) => Find(_specialIds, id);
        public int EnemyIndex(string id) => Find(_enemyIds, id);
        public int SynergyIndex(string id) => Find(_synergyIds, id);
        public int EvolutionIndex(string id) => Find(_evolutionIds, id);

        public ArenaDef Arena(string id)
        {
            int i = Find(_arenaIds, id);
            return i < 0 ? null : Def.arenas[i];
        }

        static int Find(Dictionary<string, int> map, string id) => id != null && map.TryGetValue(id, out int i) ? i : -1;

        public static BuildResult Build(ContentDef def)
        {
            var b = new Builder(def);
            b.Run();
            return b.Result();
        }

        sealed class Builder
        {
            readonly ContentDef _def;
            readonly List<string> _errors = new List<string>();
            readonly GameDatabase _db = new GameDatabase();

            public Builder(ContentDef def) { _def = def; }

            public BuildResult Result()
            {
                if (_errors.Count > 0) return new BuildResult { Db = null, Errors = _errors.ToArray() };
                _db.Def = _def;
                _db.ContentHash = ContentCanonical.Hash(_def);
                return new BuildResult { Db = _db, Errors = new string[0] };
            }

            void Err(string msg) => _errors.Add(msg);

            public void Run()
            {
                if (_def == null) { Err("conteúdo nulo"); return; }
                if (_def.rules == null) Err("rules ausente");
                if (_def.balance == null) Err("balance ausente");
                if (_def.spawn == null) Err("spawn ausente");
                if (_errors.Count > 0) return;

                _db._abilityIds = Index("ability", _def.abilities, a => a.id);
                _db._specializationIds = Index("specialization", _def.specializations, s => s.id);
                _db._specialIds = Index("special", _def.specials, s => s.id);
                _db._enemyIds = Index("enemy", _def.enemies, e => e.id);
                _db._synergyIds = Index("synergy", _def.synergies, s => s.id);
                _db._evolutionIds = Index("evolution", _def.evolutions, e => e.id);
                _db._arenaIds = Index("arena", _def.arenas, a => a.id);
                if (_errors.Count > 0) return;

                ResolveAbilities();
                ResolveSpecials();
                ResolveSpecializations();
                ResolveEnemies();
                ResolveSynergies();
                ResolveEvolutions();
                ResolveSpawn();
                ValidateBalance();
                ValidateArenas();
            }

            Dictionary<string, int> Index<T>(string kind, T[] items, System.Func<T, string> id)
            {
                var map = new Dictionary<string, int>();
                if (items == null) return map;
                for (int i = 0; i < items.Length; i++)
                {
                    string key = items[i] == null ? null : id(items[i]);
                    if (string.IsNullOrEmpty(key)) { Err($"id vazio: {kind} #{i}"); continue; }
                    if (map.ContainsKey(key)) { Err($"duplicado: {kind} {key}"); continue; }
                    map[key] = i;
                }
                return map;
            }

            void ResolveAbilities()
            {
                var list = new ResolvedAbility[_def.abilities.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    var d = _def.abilities[i];
                    var r = new ResolvedAbility { Index = i, Def = d, BaseStats = new StatBlock() };
                    string where = "ability " + d.id;

                    switch (d.kind)
                    {
                        case "weapon": r.Kind = AbilityKind.Weapon; break;
                        case "component": r.Kind = AbilityKind.Component; break;
                        case "attribute": r.Kind = AbilityKind.Attribute; break;
                        case "survival": r.Kind = AbilityKind.Survival; break;
                        default: Err($"kind desconhecido: {d.kind} em {where}"); break;
                    }
                    switch (d.rarity)
                    {
                        case "comum": r.Rarity = Rarity.Comum; break;
                        case "incomum": r.Rarity = Rarity.Incomum; break;
                        case "raro": r.Rarity = Rarity.Raro; break;
                        case "reliquia": r.Rarity = Rarity.Reliquia; break;
                        default: Err($"raridade desconhecida: {d.rarity} em {where}"); break;
                    }
                    if (!KnownBehaviors.Contains(d.behavior ?? "")) Err($"behavior desconhecido: {d.behavior} em {where}");

                    r.Tags = ParseTags(d.tags, where);
                    foreach (var sv in d.baseStats ?? new StatValueDef[0])
                        if (ParseStat(sv.stat, where, out var st)) r.BaseStats.SetBase(st, sv.value);

                    var levels = d.levels ?? new LevelDef[0];
                    if (r.Kind == AbilityKind.Weapon && d.maxLevel > 1 && levels.Length != d.maxLevel - 1)
                        Err($"{where}: levels tem {levels.Length}, esperado {d.maxLevel - 1}");
                    r.Levels = new ResolvedLevel[levels.Length];
                    for (int l = 0; l < levels.Length; l++)
                    {
                        r.Levels[l] = new ResolvedLevel
                        {
                            Add = ParseStatValues(levels[l].add, where),
                            Mult = ParseStatValues(levels[l].mult, where),
                            Text = levels[l].text ?? "",
                        };
                    }

                    r.Grants = ParseModifiers(d.grants, where);

                    var ps = d.parameters ?? new ParamDef[0];
                    r.ParamKeys = new string[ps.Length];
                    r.ParamValues = new float[ps.Length];
                    for (int p = 0; p < ps.Length; p++) { r.ParamKeys[p] = ps[p].key; r.ParamValues[p] = ps[p].value; }

                    list[i] = r;
                }
                _db._abilities = list;
            }

            void ResolveSpecials()
            {
                var list = new ResolvedSpecial[_def.specials.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    var d = _def.specials[i];
                    var r = new ResolvedSpecial { Index = i, Def = d };
                    if (d.kind == "blackout") r.Kind = SpecialKind.Blackout;
                    else if (d.kind == "turbine") r.Kind = SpecialKind.Turbine;
                    else if (d.kind == "overclock") r.Kind = SpecialKind.Overclock;
                    else Err($"kind desconhecido: {d.kind} em special {d.id}");
                    if (!string.IsNullOrEmpty(d.stunTag)) r.StunTags = ParseTags(new[] { d.stunTag }, "special " + d.id);
                    r.BoostTag = Tag.Mecanica;
                    if (!string.IsNullOrEmpty(d.boostTag) && !Tags.TryParse(d.boostTag, out r.BoostTag)) Err($"special {d.id}: boostTag desconhecida: {d.boostTag}");
                    list[i] = r;
                }
                _db._specials = list;
            }

            void ResolveSpecializations()
            {
                var list = new ResolvedSpecialization[_def.specializations.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    var d = _def.specializations[i];
                    string where = "specialization " + d.id;
                    var r = new ResolvedSpecialization
                    {
                        Index = i,
                        Def = d,
                        StartingAbility = Ref(_db._abilityIds, d.startingAbility, where + ".startingAbility"),
                        Special = Ref(_db._specialIds, d.special, where + ".special"),
                        AffinityTags = ParseTags(d.affinityTags, where),
                        AdvancedAffinity = ParseModifiers(d.advancedAffinity, where),
                    };
                    if (r.StartingAbility >= 0 && _db._abilities[r.StartingAbility].Kind != AbilityKind.Weapon)
                        Err($"{where}: startingAbility {d.startingAbility} precisa ser arma");
                    list[i] = r;
                }
                _db._specializations = list;
            }

            void ResolveEnemies()
            {
                var list = new ResolvedEnemy[_def.enemies.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    var d = _def.enemies[i];
                    var r = new ResolvedEnemy { Index = i, Def = d, Tags = ParseTags(d.tags, "enemy " + d.id) };
                    if (d.behavior == "chase") r.Behavior = EnemyBehavior.Chase;
                    else if (d.behavior == "kite_shoot") r.Behavior = EnemyBehavior.KiteShoot;
                    else Err($"behavior desconhecido: {d.behavior} em enemy {d.id}");
                    if (d.hp <= 0 || d.radius <= 0) Err($"enemy {d.id}: hp e radius precisam ser > 0");
                    list[i] = r;
                }
                _db._enemies = list;
            }

            void ResolveSynergies()
            {
                var list = new ResolvedSynergy[_def.synergies.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    var d = _def.synergies[i];
                    string where = "synergy " + d.id;
                    var r = new ResolvedSynergy
                    {
                        Index = i,
                        Def = d,
                        TargetAbility = Ref(_db._abilityIds, d.targetAbility, where + ".targetAbility"),
                        Requires = ParseRequirements(d.requires, where),
                    };
                    if (d.effect == "charge_every_n") r.Effect = SynergyEffect.ChargeEveryN;
                    else Err($"effect desconhecido: {d.effect} em {where}");
                    list[i] = r;
                }
                _db._synergies = list;
            }

            void ResolveEvolutions()
            {
                var list = new ResolvedEvolution[_def.evolutions.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    var d = _def.evolutions[i];
                    string where = "evolution " + d.id;
                    var r = new ResolvedEvolution
                    {
                        Index = i,
                        Def = d,
                        From = Ref(_db._abilityIds, d.fromAbility, where + ".fromAbility"),
                        Result = Ref(_db._abilityIds, d.resultAbility, where + ".resultAbility"),
                        Requires = ParseRequirements(d.requires, where),
                    };
                    if (r.Result >= 0 && !_db._abilities[r.Result].Def.evolutionOnly)
                        Err($"{where}: resultAbility {d.resultAbility} precisa ser evolutionOnly");
                    list[i] = r;
                }
                _db._evolutions = list;
            }

            void ResolveSpawn()
            {
                var brackets = _def.spawn.brackets ?? new BracketDef[0];
                if (brackets.Length == 0) Err("spawn: nenhum bracket");
                var list = new ResolvedBracket[brackets.Length];
                float last = -1;
                for (int i = 0; i < brackets.Length; i++)
                {
                    var b = brackets[i];
                    if (i == 0 && b.fromMinute != 0) Err("spawn: o primeiro bracket precisa começar no minuto 0");
                    if (b.fromMinute <= last) Err($"spawn: brackets fora de ordem no índice {i}");
                    last = b.fromMinute;
                    var w = b.weights ?? new EnemyWeightDef[0];
                    var r = new ResolvedBracket { FromMinute = b.fromMinute, Enemies = new int[w.Length], Weights = new float[w.Length] };
                    for (int j = 0; j < w.Length; j++)
                    {
                        r.Enemies[j] = Ref(_db._enemyIds, w[j].enemyId, $"spawn.brackets[{i}]");
                        r.Weights[j] = w[j].weight;
                        r.TotalWeight += w[j].weight;
                    }
                    if (r.TotalWeight <= 0) Err($"spawn: bracket {i} sem peso");
                    list[i] = r;
                }
                _db.Brackets = list;
            }

            void ValidateBalance()
            {
                var diffs = _def.balance.difficulties ?? new DifficultyProfileDef[0];
                foreach (var id in new[] { "easy", "normal", "hard" })
                {
                    int count = 0;
                    foreach (var d in diffs) if (d != null && d.id == id) count++;
                    if (count != 1) Err($"balance: dificuldade {id} precisa aparecer exatamente 1 vez");
                }
                var teams = _def.balance.teams ?? new TeamProfileDef[0];
                for (int size = 1; size <= 4; size++)
                {
                    int count = 0;
                    foreach (var t in teams) if (t != null && t.size == size) count++;
                    if (count != 1) Err($"balance: equipe de {size} precisa aparecer exatamente 1 vez");
                }
                foreach (var o in _def.balance.overrides ?? new CellOverrideDef[0])
                {
                    if (!Difficulties.TryParse(o.difficulty, out _)) Err($"balance: ajuste com dificuldade inválida: {o.difficulty}");
                    if (o.teamSize < 1 || o.teamSize > 4) Err($"balance: ajuste com equipe inválida: {o.teamSize}");
                    if (!BalanceResolver.IsKnownParam(o.param)) Err($"balance: parâmetro de ajuste desconhecido: {o.param}");
                }
            }

            void ValidateArenas()
            {
                if (_def.arenas == null || _def.arenas.Length == 0) Err("nenhuma arena");
                else foreach (var a in _def.arenas)
                    if (a.width <= 0 || a.height <= 0) Err($"arena {a.id}: dimensões inválidas");
            }

            static readonly HashSet<string> KnownBehaviors = new HashSet<string>
            {
                "chain", "orbit", "projectile", "aura", "passive", "attribute", "heal",
                "staut", "miyuki", "giulia", "breno", "artur", "badotti", "garcia", "jean", "fufu", "donasa",
                "item_protoboard", "item_cenoura", "item_iogurte", "item_motor", "item_cachorro", "item_camiseta", "item_mamao", "item_tequila", "item_chapeu",
            };

            int Ref(Dictionary<string, int> map, string id, string where)
            {
                int i = Find(map, id);
                if (i < 0) Err($"referência inexistente: {where} = {id}");
                return i;
            }

            TagSet ParseTags(string[] tags, string where)
            {
                var set = new TagSet();
                if (tags == null) return set;
                foreach (var t in tags)
                {
                    if (Tags.TryParse(t, out var tag)) set = set.With(tag);
                    else Err($"tag desconhecida: {t} em {where}");
                }
                return set;
            }

            bool ParseStat(string name, string where, out StatId stat)
            {
                if (StatIds.TryParse(name, out stat)) return true;
                Err($"stat desconhecido: {name} em {where}");
                return false;
            }

            StatValue[] ParseStatValues(StatValueDef[] defs, string where)
            {
                defs = defs ?? new StatValueDef[0];
                var list = new List<StatValue>(defs.Length);
                foreach (var d in defs)
                    if (ParseStat(d.stat, where, out var st)) list.Add(new StatValue { Stat = st, Value = d.value });
                return list.ToArray();
            }

            ModifierTemplate[] ParseModifiers(ModifierDef[] defs, string where)
            {
                defs = defs ?? new ModifierDef[0];
                var list = new List<ModifierTemplate>(defs.Length);
                foreach (var d in defs)
                {
                    if (!ParseStat(d.stat, where, out var st)) continue;
                    var m = new ModifierTemplate { Stat = st, Value = d.value, AbilityIndex = -1, PerLevel = d.perLevel ?? new float[0] };
                    if (d.op == "add") m.Op = ModOp.Add;
                    else if (d.op == "mult") m.Op = ModOp.Mult;
                    else { Err($"op desconhecida: {d.op} em {where}"); continue; }

                    string scope = d.scope ?? "";
                    if (scope == "player") m.Scope = TemplateScope.Player;
                    else if (scope == "all") m.Scope = TemplateScope.All;
                    else if (scope == "self") m.Scope = TemplateScope.Self;
                    else if (scope.StartsWith("tag:"))
                    {
                        m.Scope = TemplateScope.Tag;
                        m.Tags = ParseTags(new[] { scope.Substring(4) }, where);
                    }
                    else if (scope.StartsWith("ability:"))
                    {
                        m.Scope = TemplateScope.Ability;
                        m.AbilityIndex = Ref(_db._abilityIds, scope.Substring(8), where + ".scope");
                    }
                    else { Err($"escopo inválido: {scope} em {where}"); continue; }
                    list.Add(m);
                }
                return list.ToArray();
            }

            ResolvedRequirement[] ParseRequirements(RequirementDef[] defs, string where)
            {
                defs = defs ?? new RequirementDef[0];
                var list = new List<ResolvedRequirement>(defs.Length);
                foreach (var d in defs)
                {
                    var r = new ResolvedRequirement { Value = d.value, TargetIndex = -1 };
                    switch (d.kind)
                    {
                        case "ability_level":
                            r.Kind = RequirementKind.AbilityLevel;
                            r.TargetIndex = Ref(_db._abilityIds, d.target, where + ".requires");
                            break;
                        case "any_ability_level":
                            r.Kind = RequirementKind.AnyAbilityLevel;
                            var names = (d.target ?? "").Split('|');
                            r.Targets = new int[names.Length];
                            for (int k = 0; k < names.Length; k++) r.Targets[k] = Ref(_db._abilityIds, names[k], where + ".requires");
                            r.TargetIndex = r.Targets.Length > 0 ? r.Targets[0] : -1;
                            break;
                        case "tag_count":
                            r.Kind = RequirementKind.TagCount;
                            if (Tags.TryParse(d.target, out var tag)) r.Tag = tag;
                            else Err($"tag desconhecida: {d.target} em {where}");
                            break;
                        case "specialization":
                            r.Kind = RequirementKind.Specialization;
                            r.TargetIndex = Ref(_db._specializationIds, d.target, where + ".requires");
                            break;
                        case "stat_at_least":
                            r.Kind = RequirementKind.StatAtLeast;
                            if (ParseStat(d.target, where, out var st)) r.Stat = st;
                            break;
                        case "branch":
                            r.Kind = RequirementKind.Branch;
                            r.Branch = d.target;
                            break;
                        default:
                            Err($"requisito desconhecido: {d.kind} em {where}");
                            continue;
                    }
                    list.Add(r);
                }
                return list.ToArray();
            }
        }
    }
}
