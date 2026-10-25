using System;
using System.Collections.Generic;

namespace ES.Sim
{
    /// <summary>Evento em andamento. Os campos genéricos servem a todos os comportamentos.</summary>
    public sealed class ActiveEvent
    {
        public int Id, Event, Target = -1;
        /// <summary>Fim do aviso (o efeito começa aqui) e fim do evento.</summary>
        public float StartAt, EndsAt, NextTick;
        public bool Started, Finished, Success, Rewarded, Child;
        public int Goal, Progress, KillsAtStart, Source, Hits;
        public readonly int[] SlotKills = new int[PlayerStore.MaxSlots];
        public float A, B, C;
        public Vec2 Pos, Dir;
        public byte HitPlayers;
        public readonly HashSet<ushort> HitEnemies = new HashSet<ushort>();
    }

    /// <summary>Estado dos eventos aleatórios da partida (só no host).</summary>
    public sealed class EventDirector
    {
        public readonly List<ActiveEvent> Active = new List<ActiveEvent>(8);
        public readonly List<int> Recent = new List<int>(8);
        public float NextAt = -1;
        public int NextId = 1;
        public bool Enabled = true;
        /// <summary>Multiplicador do surgimento de inimigos (Laboratório Incompleto).</summary>
        public float SpawnMult = 1f;
        public readonly int[] TimesTargeted = new int[PlayerStore.MaxSlots];
        public readonly List<TimedBuff> Buffs = new List<TimedBuff>(16);
        public int Started;
    }

    public struct TimedBuff { public int Slot, Source; public float Until; public byte Look; }

    /// <summary>Bits de aparência temporária do jogador (vão no snapshot).</summary>
    public static class PlayerLook
    {
        public const byte Euphoria = 1, Hangover = 2, Jalapeno = 4, IogurteBoost = 8, Bubble = 16, Insulted = 32, Zero = 64, Reflex = 128;
    }

    /// <summary>Resultado de um evento (EventEnded.B) e bônus (EventBonus.B).</summary>
    public static class EventOutcome
    {
        public const int Failed = 0, Success = 1, Expired = 2;
        public const int BonusReflex = 1, BonusInsultBeaten = 2, BonusZeroRecovered = 3, BonusBubblePop = 4, BonusSupreme = 5,
            BonusCoxinha = 6, BonusUpgrade = 7, BonusBubbleGoal = 8, BonusJalapenoEnd = 9, BonusGrade = 10;
    }

    /// <summary>Comportamento de um evento: o que acontece ao começar, a cada tick e ao terminar.</summary>
    public abstract class GameEventBehavior
    {
        /// <summary>Chamado na escolha (antes do aviso). False desiste do evento.</summary>
        public virtual bool Prepare(World w, ActiveEvent a, ResolvedEvent ev) => true;
        public virtual void Start(World w, ActiveEvent a, ResolvedEvent ev) { }
        /// <summary>False encerra o evento antes do tempo.</summary>
        public virtual bool Tick(World w, ActiveEvent a, ResolvedEvent ev, float dt) => true;
        public virtual void Finish(World w, ActiveEvent a, ResolvedEvent ev) { }
        public virtual byte Look => 0;
    }

    /// <summary>
    /// Eventos aleatórios: agenda por pesos e raridade, sem repetir os últimos, com aviso antes dos perigosos.
    /// Tudo é decidido no host; os clientes recebem anúncios, progresso e fim por eventos de regra confiáveis.
    /// </summary>
    public static class EventSystem
    {
        public static readonly Dictionary<string, GameEventBehavior> Behaviors = new Dictionary<string, GameEventBehavior>
        {
            ["bubble"] = new BubbleEvent(),
            ["grade"] = new GradeEvent(),
            ["zero"] = new ZeroEvent(),
            ["insult"] = new InsultEvent(),
            ["lab"] = new LabEvent(),
            ["moto"] = new MotoEvent(),
            ["coxinha"] = new CoxinhaEvent(),
            ["jalapeno"] = new JalapenoEvent(),
        };

        public static readonly HashSet<string> KnownBehaviors = new HashSet<string>(Behaviors.Keys);

        static readonly List<int> Pool = new List<int>(16);
        static readonly List<float> Weights = new List<float>(16);

        public static void Step(World w, float dt)
        {
            var d = w.EventDir;
            Buffs(w);
            for (int i = d.Active.Count - 1; i >= 0; i--)
            {
                var a = d.Active[i];
                var ev = w.Db.Event(a.Event);
                var b = Behaviors[ev.Def.behavior];
                if (!a.Started)
                {
                    if (w.Elapsed < a.StartAt) continue;
                    a.Started = true;
                    a.KillsAtStart = w.Stats.Kills;
                    for (int s = 0; s < PlayerStore.MaxSlots; s++) a.SlotKills[s] = w.Stats.KillsBySlot[s];
                    b.Start(w, a, ev);
                }
                bool keep = !a.Finished && b.Tick(w, a, ev, dt) && w.Elapsed < a.EndsAt;
                if (keep) continue;
                End(w, a, ev, b);
                d.Active.RemoveAt(i);
            }
            if (d.Enabled) Schedule(w);
        }

        static void End(World w, ActiveEvent a, ResolvedEvent ev, GameEventBehavior b)
        {
            if (!a.Started) return;
            b.Finish(w, a, ev);
            if (a.Child) return;
            w.Events.AddRule(new RuleEvent
            {
                Kind = RuleEventKind.EventEnded, A = a.Id, B = a.Success ? EventOutcome.Success : a.Goal > 0 ? EventOutcome.Failed : EventOutcome.Expired,
                C = a.Event, X = a.Target,
            });
        }

        // ---------------- agenda ----------------

        static void Schedule(World w)
        {
            var d = w.EventDir;
            var r = w.Rules;
            if (w.Db.EventCount == 0) return;
            if (d.NextAt < 0) d.NextAt = r.eventFirst;
            if (w.Elapsed < d.NextAt) return;
            float interval = r.eventIntervalMin + w.Rng.NextFloat() * Math.Max(0, r.eventIntervalMax - r.eventIntervalMin);
            if (w.Bosses.Infinite) interval *= r.eventInfiniteInterval;
            d.NextAt = w.Elapsed + Math.Max(20f, interval);
            int main = 0;
            foreach (var x in d.Active) if (!x.Child) main++;
            if (main >= r.eventMaxActive) return;
            int pick = Pick(w);
            if (pick >= 0) Begin(w, pick, -1);
        }

        static int Pick(World w)
        {
            var d = w.EventDir;
            Pool.Clear(); Weights.Clear();
            float minutes = w.Elapsed / 60f, total = 0;
            for (int i = 0; i < w.Db.EventCount; i++)
            {
                var ev = w.Db.Event(i);
                if (!Eligible(w, ev, minutes)) continue;
                float weight = ev.Def.weight * ev.RarityWeight + (w.Bosses.Infinite ? ev.Def.infiniteWeightPerMinute * BossSystem.InfiniteMinutes(w) : 0);
                if (weight <= 0) continue;
                Pool.Add(i); Weights.Add(weight); total += weight;
            }
            if (total <= 0) return -1;
            float roll = w.Rng.NextFloat() * total;
            for (int k = 0; k < Pool.Count; k++) { roll -= Weights[k]; if (roll < 0) return Pool[k]; }
            return Pool[Pool.Count - 1];
        }

        static bool Eligible(World w, ResolvedEvent ev, float minutes)
        {
            var d = w.EventDir;
            if (minutes < ev.Def.minMinute) return false;
            if (ev.Def.noBoss && w.Bosses.Active.Count > 0) return false;
            if (d.Recent.Contains(ev.Index)) return false;
            foreach (var a in d.Active) if (a.Event == ev.Index) return false;
            if (ev.Scope == EventScope.Player && FreeTarget(w) < 0) return false;
            return w.Players.CountWith(PlayerStatus.Active) > 0;
        }

        /// <summary>Jogador ativo menos sorteado e sem outro evento pessoal em andamento.</summary>
        static int FreeTarget(World w)
        {
            var d = w.EventDir;
            int best = -1, bestCount = int.MaxValue;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                bool busy = false;
                foreach (var a in d.Active) if (a.Target == p.Slot) busy = true;
                if (busy) continue;
                int c = d.TimesTargeted[p.Slot] * 4 + w.Rng.Range(0, 3);
                if (c < bestCount) { bestCount = c; best = p.Slot; }
            }
            return best;
        }

        /// <summary>Começa um evento (também usado pelos testes). Retorna o evento ou null.</summary>
        public static ActiveEvent Begin(World w, int eventIndex, int target)
        {
            var d = w.EventDir;
            var ev = w.Db.Event(eventIndex);
            if (ev.Scope == EventScope.Player)
            {
                if (target < 0) target = FreeTarget(w);
                if (target < 0) return null;
            }
            else target = -1;
            var a = new ActiveEvent
            {
                Id = d.NextId++, Event = eventIndex, Target = target,
                StartAt = w.Elapsed + ev.Def.warning * w.Balance.Telegraph,
            };
            a.EndsAt = a.StartAt + ev.Def.duration;
            if (!Behaviors[ev.Def.behavior].Prepare(w, a, ev)) return null;
            if (target >= 0) d.TimesTargeted[target]++;
            d.Active.Add(a);
            d.Started++;
            d.Recent.Add(eventIndex);
            while (d.Recent.Count > Math.Max(0, w.Rules.eventNoRepeat)) d.Recent.RemoveAt(0);
            w.Events.AddRule(new RuleEvent
            {
                Kind = RuleEventKind.EventStarted, A = eventIndex, B = target < 0 ? 255 : target, C = a.Id,
                X = a.EndsAt - w.Elapsed, Y = a.StartAt - w.Elapsed,
            });
            return a;
        }

        // ---------------- utilitários dos comportamentos ----------------

        public static int KillsSince(World w, ActiveEvent a) => a.Target >= 0 ? w.Stats.KillsBySlot[a.Target] - a.SlotKills[a.Target] : w.Stats.Kills - a.KillsAtStart;

        /// <summary>Progresso de meta (abates) para a interface; envia só quando muda.</summary>
        public static void SetProgress(World w, ActiveEvent a, int progress)
        {
            progress = Math.Min(progress, Math.Max(a.Goal, progress));
            if (progress == a.Progress) return;
            a.Progress = progress;
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.EventProgress, A = a.Id, B = Math.Min(progress, 65535), C = Math.Min(a.Goal, 65535) });
        }

        public static void Bonus(World w, int slot, int bonus, Vec2 at, float value = 0) =>
            w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.EventBonus, A = slot < 0 ? 255 : slot, B = bonus, X = at.X, Y = at.Y, Z = value });

        /// <summary>Modificador que dura <paramref name="seconds"/> (removido sozinho).</summary>
        public static int AddBuff(World w, int slot, StatId stat, ModOp op, float value, float seconds, bool abilities, byte look = 0)
        {
            var p = w.Players[slot];
            if (p == null) return 0;
            int source = w.NextSourceId++;
            p.Stats.Add(new Modifier { Stat = stat, Op = op, Value = value, SourceId = source, Scope = abilities ? ModScope.AllAbilities : ModScope.Player });
            w.EventDir.Buffs.Add(new TimedBuff { Slot = slot, Source = source, Until = w.Elapsed + seconds, Look = look });
            return source;
        }

        /// <summary>Modificador sem prazo (o evento remove no fim).</summary>
        public static void AddMod(World w, int slot, StatId stat, ModOp op, float value, int source, bool abilities)
        {
            var p = w.Players[slot];
            p?.Stats.Add(new Modifier { Stat = stat, Op = op, Value = value, SourceId = source, Scope = abilities ? ModScope.AllAbilities : ModScope.Player });
        }

        public static void RemoveMods(World w, int slot, int source)
        {
            if (source != 0) w.Players[slot]?.Stats.RemoveSource(source);
        }

        static void Buffs(World w)
        {
            var list = w.EventDir.Buffs;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (w.Elapsed < list[i].Until) continue;
                w.Players[list[i].Slot]?.Stats.RemoveSource(list[i].Source);
                list.RemoveAt(i);
            }
        }

        /// <summary>Fração da barra de experiência atual como XP direto (pode completar o nível).</summary>
        public static void GiveBarFraction(World w, PlayerState p, float fraction)
        {
            if (p == null || fraction <= 0) return;
            ProgressionSystem.AddXp(w, p, XpCurve.ToNext(w.Rules, p.Progress.Level) * fraction, false);
        }

        /// <summary>XP total acumulado (para a bolha crescer com a coleta).</summary>
        public static float TotalXp(World w, PlayerState p)
        {
            float t = p.Progress.Xp;
            for (int l = 1; l < p.Progress.Level; l++) t += XpCurve.ToNext(w.Rules, l);
            return t;
        }

        /// <summary>Jogador com a Bolha Social que atrai o inimigo em <paramref name="pos"/>, ou null.</summary>
        public static PlayerState Lure(World w, Vec2 pos)
        {
            var d = w.EventDir;
            for (int i = 0; i < d.Active.Count; i++)
            {
                var a = d.Active[i];
                if (!a.Started || a.Target < 0 || w.Db.Event(a.Event).Def.behavior != "bubble") continue;
                var p = w.Players[a.Target];
                if (p == null || !p.IsActive) continue;
                if (Vec2.DistanceSq(p.Pos, pos) <= a.A * a.A) return p;
            }
            return null;
        }

        /// <summary>Aparência temporária do jogador: eventos, bônus e itens (Tequila, Iogurte).</summary>
        public static byte Look(World w, PlayerState p)
        {
            if (p == null) return 0;
            byte look = 0;
            var d = w.EventDir;
            foreach (var a in d.Active)
                if (a.Started && a.Target == p.Slot) look |= Behaviors[w.Db.Event(a.Event).Def.behavior].Look;
            foreach (var b in d.Buffs) if (b.Slot == p.Slot) look |= b.Look;
            foreach (var ab in p.Abilities)
            {
                string id = w.Db.Ability(ab.AbilityIndex).Def.id;
                if (!(ab.State is WeaponState s)) continue;
                if (id == "tequila" || id == "fiesta_caotica")
                {
                    if (s.Counters[0] == 1) look |= PlayerLook.Euphoria;
                    else if (s.Counters[0] == 2) look |= PlayerLook.Hangover;
                }
                else if ((id == "iogurte_80" || id == "dieta_milagrosa") && s.Counters[0] == 1) look |= PlayerLook.IogurteBoost;
            }
            return look;
        }

        public static bool Owns(World w, PlayerState p, string abilityId)
        {
            int idx = w.Db.AbilityIndex(abilityId);
            if (idx < 0 || p == null) return false;
            foreach (var a in p.Abilities) if (a.AbilityIndex == idx) return true;
            return false;
        }

        public static PlayerState RandomActive(World w)
        {
            int n = w.Players.CountWith(PlayerStatus.Active);
            if (n == 0) return null;
            int k = w.Rng.Range(0, n);
            foreach (var p in w.Players.Slots) if (p != null && p.IsActive && k-- == 0) return p;
            return null;
        }

        /// <summary>Coleta de coxinha (chamado pela coleta de itens). Retorna false se não é do jogador.</summary>
        public static bool CanCollect(World w, int gem, PlayerState p) => w.Gems.Owner[gem] == 255 || w.Gems.Owner[gem] == p.Slot;

        public static void OnCoxinha(World w, PlayerState p, PickupKind kind, Vec2 pos)
        {
            var ev = FindByBehavior(w, "coxinha", p.Slot, out var a);
            float heal = ev?.Param("heal", 0.2f) ?? 0.2f;
            float max = p.Stats.Get(StatId.MaxHp);
            p.Hp = Math.Min(max, p.Hp + max * heal);
            ProgressionSystem.AddXp(w, p, ev?.Param("xp", 8) ?? 8, true);
            AddBuff(w, p.Slot, StatId.MoveSpeed, ModOp.Mult, ev?.Param("speed", 0.2f) ?? 0.2f, ev?.Param("speedSeconds", 5) ?? 5, false);
            if (kind == PickupKind.CoxinhaSuprema)
            {
                Area.Burst(w, p.Slot, -1, p.Pos, ev?.Param("supremeRadius", 3) ?? 3, (ev?.Param("supremeDamage", 40) ?? 40) * w.Balance.EnemyHp, 0.6f, 0, FxStyle.Coins);
                Bonus(w, p.Slot, EventOutcome.BonusSupreme, pos);
            }
            else Bonus(w, p.Slot, EventOutcome.BonusCoxinha, pos);
            if (a != null) { a.Success = true; a.Finished = true; }
        }

        static ResolvedEvent FindByBehavior(World w, string behavior, int slot, out ActiveEvent active)
        {
            active = null;
            foreach (var a in w.EventDir.Active)
            {
                var ev = w.Db.Event(a.Event);
                if (ev.Def.behavior == behavior && (a.Target == slot || a.Target < 0)) { active = a; return ev; }
            }
            int idx = -1;
            for (int i = 0; i < w.Db.EventCount; i++) if (w.Db.Event(i).Def.behavior == behavior) { idx = i; break; }
            return idx >= 0 ? w.Db.Event(idx) : null;
        }
    }

    // =====================================================================
    // Comportamentos
    // =====================================================================

    /// <summary>Bolha Social: +XP, atrai inimigos, meta de abates; cresce com a coleta e estoura.</summary>
    sealed class BubbleEvent : GameEventBehavior
    {
        public override byte Look => PlayerLook.Bubble;

        public override void Start(World w, ActiveEvent a, ResolvedEvent ev)
        {
            var p = w.Players[a.Target];
            a.Source = w.NextSourceId++;
            EventSystem.AddMod(w, a.Target, StatId.XpGain, ModOp.Add, ev.Param("xpGain", 0.3f), a.Source, false);
            a.A = ev.Param("lureRadius", 8);
            a.Goal = (int)ev.Param("killGoal", 25);
            a.B = EventSystem.TotalXp(w, p);
            a.C = 0;
        }

        public override bool Tick(World w, ActiveEvent a, ResolvedEvent ev, float dt)
        {
            var p = w.Players[a.Target];
            if (p == null || !p.IsActive) return false;
            int kills = EventSystem.KillsSince(w, a);
            EventSystem.SetProgress(w, a, kills);
            if (!a.Success && kills >= a.Goal)
            {
                a.Success = true;
                EventSystem.GiveBarFraction(w, p, ev.Param("rewardXp", 0.4f));
                EventSystem.Bonus(w, p.Slot, EventOutcome.BonusBubbleGoal, p.Pos);
            }
            // cresce com a experiência coletada; cheia, estoura numa onda de notificações (uma vez)
            if (a.C == 0)
            {
                float need = XpCurve.ToNext(w.Rules, p.Progress.Level) * ev.Param("growXp", 0.6f);
                if (EventSystem.TotalXp(w, p) - a.B >= need)
                {
                    a.C = 1;
                    Area.Burst(w, p.Slot, -1, p.Pos, ev.Param("popRadius", 3.5f), ev.Param("popDamage", 30) * w.Balance.EnemyHp, 0.8f, 0, FxStyle.Fiesta);
                    EventSystem.Bonus(w, p.Slot, EventOutcome.BonusBubblePop, p.Pos);
                }
            }
            return true;
        }

        public override void Finish(World w, ActiveEvent a, ResolvedEvent ev) => EventSystem.RemoveMods(w, a.Target, a.Source);
    }

    /// <summary>Dez na Prova: cada jogador ganha parte da barra (pode completar o nível) e sorte para cartas raras.</summary>
    sealed class GradeEvent : GameEventBehavior
    {
        public override void Start(World w, ActiveEvent a, ResolvedEvent ev)
        {
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                EventSystem.GiveBarFraction(w, p, ev.Param("xpFraction", 0.55f));
                p.Progress.LuckUntil = w.Elapsed + ev.Def.duration;
                EventSystem.Bonus(w, p.Slot, EventOutcome.BonusGrade, p.Pos);
            }
            a.Success = true;
        }
    }

    /// <summary>Zero na Prova: perde parte do XP não convertido (nunca o nível), ataques mais lentos, inimigos acelerados; meta devolve tudo.</summary>
    sealed class ZeroEvent : GameEventBehavior
    {
        public override byte Look => PlayerLook.Zero;

        public override void Start(World w, ActiveEvent a, ResolvedEvent ev)
        {
            var p = w.Players[a.Target];
            a.A = p.Progress.Xp * ev.Param("xpLoss", 0.25f);
            p.Progress.Xp -= a.A;
            a.Source = w.NextSourceId++;
            EventSystem.AddMod(w, a.Target, StatId.Cooldown, ModOp.Mult, ev.Param("cooldown", 0.15f), a.Source, true);
            a.Goal = (int)ev.Param("killGoal", 20);
            // inimigos próximos aproveitam a distração
            float r = ev.Param("hasteRadius", 7), haste = ev.Param("haste", 0.25f), secs = ev.Param("hasteSeconds", 6);
            var e = w.Enemies;
            for (int i = 0; i < e.Count; i++)
            {
                if (Vec2.DistanceSq(e.Pos[i], p.Pos) > r * r || e.SlowUntil[i] > w.Elapsed) continue;
                if (w.Db.Enemy(e.Type[i]).Behavior == EnemyBehavior.Boss) continue;
                e.SlowAmount[i] = -haste;
                e.SlowUntil[i] = w.Elapsed + secs;
            }
        }

        public override bool Tick(World w, ActiveEvent a, ResolvedEvent ev, float dt)
        {
            int kills = EventSystem.KillsSince(w, a);
            EventSystem.SetProgress(w, a, kills);
            if (kills < a.Goal) return true;
            a.Success = true;
            var p = w.Players[a.Target];
            EventSystem.RemoveMods(w, a.Target, a.Source);
            a.Source = 0;
            if (p != null) ProgressionSystem.AddXp(w, p, a.A * (1f + ev.Param("recoverBonus", 0.2f)), false);
            EventSystem.Bonus(w, a.Target, EventOutcome.BonusZeroRecovered, p?.Pos ?? Vec2.Zero, a.A);
            return false;
        }

        public override void Finish(World w, ActiveEvent a, ResolvedEvent ev) => EventSystem.RemoveMods(w, a.Target, a.Source);
    }

    /// <summary>Chamado de Burro: ataques um pouco mais lentos, XP extra por abate; a meta vira bônus de dano.</summary>
    sealed class InsultEvent : GameEventBehavior
    {
        public override byte Look => PlayerLook.Insulted;

        public override void Start(World w, ActiveEvent a, ResolvedEvent ev)
        {
            a.Source = w.NextSourceId++;
            EventSystem.AddMod(w, a.Target, StatId.Cooldown, ModOp.Mult, ev.Param("cooldown", 0.12f), a.Source, true);
            a.Goal = (int)ev.Param("killGoal", 15);
        }

        public override bool Tick(World w, ActiveEvent a, ResolvedEvent ev, float dt)
        {
            var p = w.Players[a.Target];
            int kills = EventSystem.KillsSince(w, a);
            if (kills > a.Progress && p != null) ProgressionSystem.AddXp(w, p, ev.Param("xpPerKill", 1.5f) * (kills - a.Progress), true);
            EventSystem.SetProgress(w, a, kills);
            if (kills < a.Goal) return true;
            a.Success = true;
            EventSystem.RemoveMods(w, a.Target, a.Source);
            a.Source = 0;
            if (p != null)
            {
                EventSystem.AddBuff(w, a.Target, StatId.Damage, ModOp.Mult, ev.Param("damage", 0.2f), ev.Param("damageSeconds", 8), true);
                // os símbolos se reorganizam numa pequena explosão de energia
                Area.Burst(w, p.Slot, -1, p.Pos, ev.Param("burstRadius", 2.5f), ev.Param("burstDamage", 20) * w.Balance.EnemyHp, 0.5f, 0, FxStyle.Math);
                EventSystem.Bonus(w, p.Slot, EventOutcome.BonusInsultBeaten, p.Pos);
            }
            return false;
        }

        public override void Finish(World w, ActiveEvent a, ResolvedEvent ev) => EventSystem.RemoveMods(w, a.Target, a.Source);
    }

    /// <summary>Laboratório Incompleto: emergência global com ondas extras e vazamentos sinalizados; meta de abates no prazo.</summary>
    sealed class LabEvent : GameEventBehavior
    {
        public override void Start(World w, ActiveEvent a, ResolvedEvent ev)
        {
            w.EventDir.SpawnMult = ev.Param("spawnMult", 1.8f);
            a.Goal = (int)(ev.Param("killGoalPerPlayer", 40) * Math.Max(1, w.Players.CountWith(PlayerStatus.Active)));
            SpawnDirector.Surge(w, (int)ev.Param("surge", 15));
            a.NextTick = w.Elapsed + 1f;
            a.A = 0;
        }

        public override bool Tick(World w, ActiveEvent a, ResolvedEvent ev, float dt)
        {
            int kills = EventSystem.KillsSince(w, a);
            EventSystem.SetProgress(w, a, kills);
            if (kills >= a.Goal) { a.Success = true; return false; }
            if (a.A == 0 && w.Elapsed >= a.StartAt + ev.Def.duration * 0.5f)
            {
                a.A = 1;
                SpawnDirector.Surge(w, (int)ev.Param("surge", 15));
            }
            if (w.Elapsed >= a.NextTick)
            {
                // máquinas falham: vapor e descargas em áreas sinalizadas perto de alguém (nunca bloqueia a arena)
                a.NextTick = w.Elapsed + ev.Param("hazardEvery", 3.5f);
                var p = EventSystem.RandomActive(w);
                if (p != null)
                {
                    float ang = w.Rng.NextFloat() * (float)Math.PI * 2, dist = 2.5f + w.Rng.NextFloat() * 2.5f;
                    var at = p.Pos + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * dist;
                    BossSystem.AddHazard(w, HazardShape.Circle, w.Rng.NextFloat() < 0.5f ? FxStyle.Heat : FxStyle.Electric, at,
                        ev.Param("hazardRadius", 1.3f), 0, 0, ev.Param("hazardDelay", 1.2f) * w.Balance.Telegraph, ev.Param("hazardDamage", 10) * w.Balance.EnemyDamage);
                }
            }
            return true;
        }

        public override void Finish(World w, ActiveEvent a, ResolvedEvent ev)
        {
            w.EventDir.SpawnMult = 1f;
            if (!a.Success) return;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                EventSystem.GiveBarFraction(w, p, ev.Param("rewardXp", 0.5f));
                if (w.Rng.NextFloat() < ev.Param("upgradeChance", 0.35f))
                {
                    p.Progress.PendingLevels++;
                    ProgressionSystem.EnsureOffer(w, p.Slot);
                    EventSystem.Bonus(w, p.Slot, EventOutcome.BonusUpgrade, p.Pos);
                }
            }
        }
    }

    /// <summary>Motoqueiro: aviso com a trajetória; a moto derruba inimigos e machuca quem fica no caminho.</summary>
    sealed class MotoEvent : GameEventBehavior
    {
        const float Length = 36f;

        public override bool Prepare(World w, ActiveEvent a, ResolvedEvent ev)
        {
            var p = EventSystem.RandomActive(w);
            if (p == null) return false;
            Plan(w, a, ev, p.Pos, w.Rng.NextFloat() * (float)Math.PI * 2);
            // Modo Infinito avançado: uma segunda moto em cruz
            if (w.Bosses.Infinite && BossSystem.InfiniteMinutes(w) >= ev.Param("extraBikesAfterMinutes", 5))
            {
                var b = new ActiveEvent { Id = w.EventDir.NextId++, Event = a.Event, Target = -1, Child = true, StartAt = a.StartAt + 0.8f, EndsAt = a.EndsAt + 0.8f };
                Plan(w, b, ev, p.Pos, (float)Math.Atan2(a.Dir.Y, a.Dir.X) + (float)Math.PI / 2);
                w.EventDir.Active.Add(b);
            }
            return true;
        }

        static void Plan(World w, ActiveEvent a, ResolvedEvent ev, Vec2 through, float angle)
        {
            a.Dir = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
            // passa ao lado do alvo: parado, é atingido; um passo para o lado basta
            var side = new Vec2(-a.Dir.Y, a.Dir.X) * 1.1f;
            a.Pos = through + side - a.Dir * (Length * 0.5f);
            float speed = ev.Param("speed", 22);
            a.EndsAt = a.StartAt + Length / speed + 0.1f;
            w.Events.AddRule(new RuleEvent
            {
                Kind = RuleEventKind.EventMoto, A = a.Id, B = (int)(Length * 10), C = (int)Math.Round((a.StartAt - w.Elapsed) * 1000),
                X = a.Pos.X, Y = a.Pos.Y, Z = angle, W = speed,
            });
        }

        public override bool Tick(World w, ActiveEvent a, ResolvedEvent ev, float dt)
        {
            float speed = ev.Param("speed", 22), width = ev.Param("width", 0.9f);
            float t = w.Elapsed - a.StartAt;
            var head = a.Pos + a.Dir * Math.Min(Length, speed * t);
            var tail = a.Pos + a.Dir * Math.Max(0, Math.Min(Length, speed * (t - dt * 1.5f)));
            var e = w.Enemies;
            for (int i = 0; i < e.Count; i++)
            {
                if ((e.Flags[i] & EnemyFlags.Dead) != 0 || a.HitEnemies.Contains(e.Id[i])) continue;
                if (SegDist2(e.Pos[i], tail, head) > (width + e.Radius[i]) * (width + e.Radius[i])) continue;
                a.HitEnemies.Add(e.Id[i]);
                if (CombatSystem.DamageEnemy(w, i, ev.Param("enemyDamage", 60) * w.Balance.EnemyHp, a.Dir, ev.Param("knock", 3), -1, -1)) a.Hits++;
            }
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive || (a.HitPlayers & (1 << p.Slot)) != 0) continue;
                if (SegDist2(p.Pos, tail, head) > (width + p.Radius) * (width + p.Radius)) continue;
                a.HitPlayers |= (byte)(1 << p.Slot);
                CombatSystem.DamagePlayer(w, p.Slot, ev.Param("playerDamage", 18) * w.Balance.EnemyDamage);
                // empurrão breve para fora da pista (não tira o controle)
                var side = new Vec2(-a.Dir.Y, a.Dir.X);
                float s = (p.Pos - head).X * side.X + (p.Pos - head).Y * side.Y >= 0 ? 1 : -1;
                p.Pos = ArenaCollision.Resolve(p.Pos + side * (s * 1.2f), p.Radius, w.Arena);
            }
            return speed * t < Length;
        }

        public override void Finish(World w, ActiveEvent a, ResolvedEvent ev)
        {
            // Reflexo de Piloto: passou raspando, sem ser atingido, e a moto derrubou vários inimigos
            a.Success = a.Hits >= (int)ev.Param("perfectKills", 3) && a.HitPlayers == 0;
            if (a.Hits < (int)ev.Param("perfectKills", 3)) return;
            float near = ev.Param("nearMiss", 3.5f);
            var end = a.Pos + a.Dir * Length;
            foreach (var p in w.Players.Slots)
            {
                if (p == null || !p.IsActive || (a.HitPlayers & (1 << p.Slot)) != 0) continue;
                if (SegDist2(p.Pos, a.Pos, end) > near * near) continue;
                EventSystem.AddBuff(w, p.Slot, StatId.MoveSpeed, ModOp.Mult, ev.Param("reflexSpeed", 0.25f), ev.Param("reflexSeconds", 6), false, PlayerLook.Reflex);
                EventSystem.Bonus(w, p.Slot, EventOutcome.BonusReflex, p.Pos);
            }
        }

        static float SegDist2(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            float len2 = ab.LengthSq();
            float t = len2 > 1e-6f ? Math.Max(0, Math.Min(1, ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2)) : 0;
            return Vec2.DistanceSq(p, a + ab * t);
        }
    }

    /// <summary>Coxinha: aparece perto do jogador sorteado (só ele coleta); às vezes é a Suprema.</summary>
    sealed class CoxinhaEvent : GameEventBehavior
    {
        public override void Start(World w, ActiveEvent a, ResolvedEvent ev)
        {
            var p = w.Players[a.Target];
            if (p == null) { a.Finished = true; return; }
            var kind = w.Rng.NextFloat() < ev.Param("supremeChance", 0.1f) ? PickupKind.CoxinhaSuprema : PickupKind.Coxinha;
            Vec2 at = p.Pos;
            for (int k = 0; k < 8; k++)
            {
                float ang = w.Rng.NextFloat() * (float)Math.PI * 2;
                var c = p.Pos + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang)) * (3f + w.Rng.NextFloat() * 2f);
                if (ArenaCollision.Blocked(c, 0.4f, w.Arena)) continue;
                at = c;
                break;
            }
            int g = CombatSystem.SpawnPickup(w, kind, at, a.Id, (byte)p.Slot);
            a.Goal = 0;
            a.A = g >= 0 ? w.Gems.Id[g] : 0;
            a.B = (float)kind;
        }

        public override void Finish(World w, ActiveEvent a, ResolvedEvent ev)
        {
            if (a.Success) return;
            // não coletada: some com o fim do evento
            var g = w.Gems;
            for (int i = 0; i < g.Count; i++)
            {
                if (g.Id[i] != (ushort)a.A || (g.Kind[i] != PickupKind.Coxinha && g.Kind[i] != PickupKind.CoxinhaSuprema)) continue;
                w.Events.AddRule(new RuleEvent { Kind = RuleEventKind.GemCollected, A = g.Id[i], B = -1 });
                g.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>Jalapeño: mais dano e ataques mais rápidos, queima ao redor; no fim uma explosão de calor e um breve alívio lento.</summary>
    sealed class JalapenoEvent : GameEventBehavior
    {
        public override byte Look => PlayerLook.Jalapeno;
        static readonly int[] Near = new int[64];

        public override void Start(World w, ActiveEvent a, ResolvedEvent ev)
        {
            a.Source = w.NextSourceId++;
            EventSystem.AddMod(w, a.Target, StatId.Damage, ModOp.Mult, ev.Param("damage", 0.2f), a.Source, true);
            EventSystem.AddMod(w, a.Target, StatId.Cooldown, ModOp.Mult, ev.Param("cooldown", -0.2f), a.Source, true);
            a.NextTick = w.Elapsed;
            a.C = EventSystem.Owns(w, w.Players[a.Target], "cenoura_podre") || EventSystem.Owns(w, w.Players[a.Target], "hortalica_desgraca") ? 1 : 0;
        }

        public override bool Tick(World w, ActiveEvent a, ResolvedEvent ev, float dt)
        {
            var p = w.Players[a.Target];
            if (p == null || !p.IsActive) return false;
            if (w.Elapsed < a.NextTick) return true;
            a.NextTick = w.Elapsed + ev.Param("burnEvery", 0.5f);
            float r = ev.Param("burnRadius", 2.2f);
            Area.Burst(w, p.Slot, -1, p.Pos, r, ev.Param("burnDamage", 6) * w.Balance.EnemyHp, 0, 0, FxStyle.Heat);
            if (a.C == 1)
            {
                // com a Cenoura Podre: nuvem picante que continua queimando
                int n = AbilitySystem.Overlapping(w, p.Pos, r + 0.8f, Near);
                for (int k = 0; k < n; k++) Area.ApplyDot(w, Near[k], ev.Param("stinkDps", 8), ev.Param("stinkSeconds", 3), p.Slot, -1);
                w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = p.Slot, B = (int)FxStyle.Stink, X = p.Pos.X, Y = p.Pos.Y, Value = r + 0.8f });
            }
            return true;
        }

        public override void Finish(World w, ActiveEvent a, ResolvedEvent ev)
        {
            EventSystem.RemoveMods(w, a.Target, a.Source);
            var p = w.Players[a.Target];
            if (p == null) return;
            Area.Burst(w, p.Slot, -1, p.Pos, ev.Param("endRadius", 3.2f), ev.Param("endDamage", 35) * w.Balance.EnemyHp, 0.8f, 0, FxStyle.Heat);
            EventSystem.AddBuff(w, p.Slot, StatId.MoveSpeed, ModOp.Mult, -ev.Param("endSlow", 0.25f), ev.Param("endSlowSeconds", 1.5f), false);
            EventSystem.Bonus(w, p.Slot, EventOutcome.BonusJalapenoEnd, p.Pos);
            a.Success = true;
        }
    }
}
