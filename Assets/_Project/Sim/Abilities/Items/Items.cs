using System;
using System.Collections.Generic;

namespace ES.Sim
{
    // Os nove itens. Bônus fixos (velocidade, esquiva, experiência...) estão em "grants" no conteúdo;
    // aqui ficam os efeitos ativos. A versão evoluída usa a mesma classe com o parâmetro "evolved" = 1.

    /// <summary>
    /// Protoboard: nível 3+ — acertos de armas eletrônicas/de energia têm chance de soltar uma descarga;
    /// nível 4 — a descarga salta para 2 inimigos. Evoluída (Protoboard Experimental): circuitos maiores
    /// e mais frequentes com qualquer arma eletrônica (não só da Giulia).
    /// </summary>
    public sealed class ProtoboardItem : ItemBehavior
    {
        static readonly TagSet Electric = TagSet.Of(Tag.Eletronica, Tag.Energia);

        public override void OnEnemyHit(World w, int slot, AbilityInstance item, int ability, int enemy)
        {
            var c = CtxOf(w, slot, item);
            if (c.Base < 3 || ability == item.AbilityIndex || !w.Db.Ability(ability).Tags.Overlaps(Electric)) return;
            if (!c.Due(0)) return;
            float chance = c.Param(c.Evolved ? "evoChance" : "chance", c.Evolved ? 0.3f : 0.1f);
            if (w.Rng.NextFloat() >= chance) return;
            int jumps = c.Evolved ? (int)c.Param("evoJumps", 4) : c.Base >= 4 ? 2 : 0;
            float range = c.Param(c.Evolved ? "evoRange" : "range", c.Evolved ? 3.5f : 3f);
            Area.Chain(w, slot, item.AbilityIndex, w.Enemies.Pos[enemy], enemy, jumps, range, c.Dmg(), 0.3f, c.Evolved ? FxStyle.Circuit : FxStyle.Electric);
            c.Reset(0, c.Param("internalCooldown", 0.12f));
        }
    }

    /// <summary>
    /// Cenoura Podre: arremessa uma cenoura no inimigo mais próximo; o impacto deixa uma nuvem de mau
    /// cheiro (dano contínuo e leve lentidão). Nível 4: chance de atordoar; nível 5: quem passa pela
    /// nuvem espalha o fedor uma vez. Evoluída (Hortaliça da Desgraça): nuvem maior e mais longa, e o
    /// fedor se espalha em até 3 gerações (limite de 60 infectados).
    /// </summary>
    public sealed class CenouraItem : ItemBehavior
    {
        const int TThrow = 0, TSpread = 1;
        const byte VCarrot = 1;

        sealed class Stink { public readonly Dictionary<ushort, int> Gen = new Dictionary<ushort, int>(); public readonly List<ushort> Ids = new List<ushort>(); }
        static readonly int[] Hits = new int[64];

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.Due(TThrow))
            {
                int t = c.Nearest(c.Range);
                if (t >= 0)
                {
                    var s = c.NewShot(AllySprite.Carrot, c.DirTo(t), c.Speed(9f), c.Dmg(), 0.3f, c.Range);
                    s.Variant = VCarrot;
                    if (c.Base >= 4 && c.W.Rng.NextFloat() < c.Param("stunChance", 0.15f)) s.Stun = c.Param("stunSeconds", 0.6f);
                    c.Fire(s);
                    c.Sound(0);
                    c.Reset(TThrow, c.Stat(StatId.Cooldown));
                }
            }
            if (c.Base >= 5 && c.Due(TSpread)) { Spread(ref c); c.Reset(TSpread, 0.5f); }
        }

        public override void OnShotEnd(World w, ref Shot s)
        {
            if (s.Variant != VCarrot) return;
            var a = InstanceOf(w, s.Owner, s.Ability);
            if (a == null) return;
            var c = CtxOf(w, s.Owner, a);
            float r = c.Radius(c.Evolved ? 2.2f : 1.4f), life = c.Time(c.Evolved ? 6f : 3f);
            w.Zones.Spawn(new Zone
            {
                Owner = s.Owner, Ability = s.Ability, Sprite = AllySprite.ZoneStink, Pos = s.Pos, Radius = r, Life = life,
                Interval = 0.5f, Damage = c.Dmg(0.25f), Slow = 0.15f, Follow = -1,
            });
            Area.Burst(w, s.Owner, s.Ability, s.Pos, r, 0, 0, 0, FxStyle.Stink);
        }

        /// <summary>Quem está na nuvem pega o fedor; infectados passam para vizinhos (gerações limitadas).</summary>
        void Spread(ref Ctx c)
        {
            var st = c.S.Extra as Stink ?? (Stink)(c.S.Extra = new Stink());
            var e = c.W.Enemies;
            int maxGen = c.Evolved ? 3 : 1, cap = c.Evolved ? 60 : 25;
            float dps = c.Dmg(c.Evolved ? 0.3f : 0.15f), hop = c.Evolved ? 1.6f : 1.1f;
            var zs = c.W.Zones;
            for (int z = 0; z < zs.Count; z++)
            {
                ref var zone = ref zs.Items[z];
                if (zone.Owner != c.Slot || zone.Sprite != AllySprite.ZoneStink) continue;
                int n = AbilitySystem.Overlapping(c.W, zone.Pos, zone.Radius, Hits);
                for (int h = 0; h < n && st.Gen.Count < cap; h++) Infect(ref c, st, Hits[h], 0, dps);
            }
            // propaga a partir dos infectados ainda vivos
            int count = st.Ids.Count;
            for (int k = 0; k < count && st.Gen.Count < cap; k++)
            {
                ushort id = st.Ids[k];
                int gen = st.Gen[id];
                if (gen >= maxGen) continue;
                int i = IndexOf(e, id);
                if (i < 0) continue;
                int n = AbilitySystem.Overlapping(c.W, e.Pos[i], hop, Hits);
                for (int h = 0; h < n && st.Gen.Count < cap; h++) Infect(ref c, st, Hits[h], gen + 1, dps * 0.7f);
            }
            // esquece quem morreu ou já não está fedendo
            for (int k = st.Ids.Count - 1; k >= 0; k--)
            {
                int i = IndexOf(e, st.Ids[k]);
                if (i < 0 || e.DotUntil[i] < c.W.Elapsed) { st.Gen.Remove(st.Ids[k]); st.Ids.RemoveAt(k); }
            }
        }

        static void Infect(ref Ctx c, Stink st, int enemy, int gen, float dps)
        {
            ushort id = c.W.Enemies.Id[enemy];
            if (st.Gen.ContainsKey(id)) return;
            st.Gen[id] = gen;
            st.Ids.Add(id);
            Area.ApplyDot(c.W, enemy, dps, 2f, c.Slot, c.Ability);
        }

        static int IndexOf(EnemyStore e, ushort id)
        {
            for (int i = 0; i < e.Count; i++) if (e.Id[i] == id && (e.Flags[i] & EnemyFlags.Dead) == 0) return i;
            return -1;
        }
    }

    /// <summary>
    /// Iogurte de 80 Calorias: velocidade, experiência e (nível 4) recarga vêm dos "grants". Nível 5:
    /// ao subir de nível recupera vida (com intervalo de proteção). Evoluído (Dieta Milagrosa): coletar
    /// experiência dá um impulso de velocidade curto, com recarga.
    /// </summary>
    public sealed class IogurteItem : ItemBehavior
    {
        const int THealCd = 0, TBoostCd = 1, TBoostLeft = 2;

        public override void OnLevelUp(World w, int slot, AbilityInstance item)
        {
            var c = CtxOf(w, slot, item);
            if (c.Base < 5 || !c.Due(THealCd)) return;
            Heal(c.P, c.P.Stats.Get(StatId.MaxHp) * c.Param("levelHeal", 0.05f), 0);
            w.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = slot, B = (int)FxStyle.Heal, X = c.Pos.X, Y = c.Pos.Y, Value = 0.9f });
            c.Reset(THealCd, c.Param("levelHealCooldown", 3f));
        }

        public override void OnXpGained(World w, int slot, AbilityInstance item)
        {
            var c = CtxOf(w, slot, item);
            if (!c.Evolved || !c.Due(TBoostCd)) return;
            int src = BuffSource(w, c.S);
            c.P.Stats.RemoveSource(src);
            c.P.Stats.Add(new Modifier { Stat = StatId.MoveSpeed, Op = ModOp.Mult, Value = c.Param("boost", 0.25f), SourceId = src });
            c.Reset(TBoostLeft, c.Param("boostSeconds", 1.5f));
            c.Reset(TBoostCd, c.Param("boostCooldown", 4f));
            c.S.Counters[0] = 1;
        }

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.S.Counters[0] == 1 && c.Due(TBoostLeft)) { c.P.Stats.RemoveSource(BuffSource(c.W, c.S)); c.S.Counters[0] = 0; }
        }
    }

    /// <summary>
    /// Motor do Astra: onda de impacto mecânica periódica ao redor de quem o carrega (qualquer
    /// personagem). Nível 4: lança peças metálicas; nível 5: a cada 3ª onda, sobrecarga com onda dupla.
    /// Evoluído (Astra em Alta Rotação): ondas maiores e mais frequentes; com o carro do Jean, o drift
    /// acelera e deixa rastro de dano (ver JeanBehavior).
    /// </summary>
    public sealed class MotorItem : ItemBehavior
    {
        const int TWave = 0;

        protected override void Step(ref Ctx c, float dt)
        {
            if (!c.Due(TWave)) return;
            float r = c.Radius(c.Evolved ? 3.2f : 2.2f);
            if (c.Nearest(r + 1f) < 0) return;
            Area.Burst(c.W, c.Slot, c.Ability, c.Pos, r, c.Dmg(), c.Stat(StatId.Knockback), 0f, FxStyle.Impact);
            c.Sound(0);
            if (c.Base >= 4)
                for (int k = 0; k < 5; k++)
                {
                    var dir = Rotate(new Vec2(1, 0), k * 6.2831853f / 5 + c.W.Elapsed);
                    c.Fire(c.NewShot(AllySprite.ScrapPart, dir, 8f, c.Dmg(0.4f), 0.22f, 3.5f));
                }
            if (c.Base >= 5 && c.Every(0, 3)) c.Strike(c.Pos, 0.3f, r * 1.15f, c.Dmg(0.8f), FxStyle.Impact, 0, c.Stat(StatId.Knockback));
            c.Reset(TWave, c.Evolved ? c.Period(c.Param("evoInterval", 2.5f)) : c.Stat(StatId.Cooldown));
        }
    }

    /// <summary>
    /// Cachorro Quentamento de Motor: companheiro que persegue inimigos e bate por contato, faz
    /// investidas curtas deixando um rastro de calor e volta se ficar longe. Nível 5: a cada 4ª
    /// investida, corrida turbo. Evoluído (Cão Turbo): investidas turbo que explodem no fim.
    /// </summary>
    public sealed class CachorroItem : ItemBehavior
    {
        const int THit = 0, TDash = 1, TDashing = 2, TTrail = 3;
        static readonly int[] Hits = new int[32];

        sealed class Dog { public Vec2 DashVel; public readonly HashSet<ushort> DashHits = new HashSet<ushort>(); public bool Turbo; }

        protected override void Step(ref Ctx c, float dt)
        {
            var comp = c.W.Companions;
            var d = c.S.Extra as Dog ?? (Dog)(c.S.Extra = new Dog());
            var sprite = c.Evolved ? AllySprite.HotDogTurbo : AllySprite.HotDog;
            int id = c.S.Companions[0];
            if (id < 0 || !comp.Items[id].Active || comp.Items[id].Ability != c.Ability) id = c.S.Companions[0] = comp.Spawn(c.Slot, c.Ability, sprite, c.Pos + new Vec2(1, -1));
            if (id < 0) return;
            ref var dog = ref comp.Items[id];
            var e = c.W.Enemies;

            if (!c.Due(TDashing))
            {
                dog.Pos += d.DashVel * dt;
                dog.Param = AllyList.Angle(d.DashVel);
                int n = AbilitySystem.Overlapping(c.W, dog.Pos, 0.6f, Hits);
                for (int h = 0; h < n; h++)
                    if (d.DashHits.Add(e.Id[Hits[h]])) CombatSystem.DamageEnemy(c.W, Hits[h], c.Dmg(d.Turbo ? 2f : 1.4f), d.DashVel.Normalized(), 2f, c.Slot, c.Ability);
                if (c.Due(TTrail))
                {
                    c.W.Zones.Spawn(new Zone { Owner = (byte)c.Slot, Ability = (short)c.Ability, Sprite = AllySprite.ZoneHeat, Pos = dog.Pos, Radius = 0.6f, Life = c.Time(1.2f), Interval = 0.4f, Damage = c.Dmg(0.25f), Follow = -1 });
                    c.Reset(TTrail, 0.09f);
                }
                if (c.S.Timers[TDashing] <= dt && d.Turbo && c.Evolved)
                    Area.Burst(c.W, c.Slot, c.Ability, dog.Pos, c.Radius(2.2f), c.Dmg(2f), 3f, 0f, FxStyle.Heat);
                return;
            }

            float speed = c.Base >= 2 ? 7f : 5.5f;
            int t = AbilitySystem.Nearest(c.W, dog.Pos, c.Range, 1, Targets, null, 0) > 0 ? Targets[0] : -1;
            bool far = Vec2.DistanceSq(dog.Pos, c.Pos) > 64f;
            var goal = !far && t >= 0 ? e.Pos[t] : c.Pos + new Vec2(1.2f, -0.8f);
            var to = goal - dog.Pos;
            float len = to.Length();
            float step = (far ? speed * 1.8f : speed) * dt;
            if (len > 0.3f) dog.Pos += to / len * Math.Min(step, len);
            dog.Param = AllyList.Angle(to);

            if (c.Due(THit))
            {
                int n = AbilitySystem.Overlapping(c.W, dog.Pos, 0.55f, Hits);
                for (int h = 0; h < n; h++) CombatSystem.DamageEnemy(c.W, Hits[h], c.Dmg(0.8f), (e.Pos[Hits[h]] - dog.Pos).Normalized(), 1.2f, c.Slot, c.Ability);
                if (n > 0) c.Reset(THit, 0.5f);
            }
            if (t >= 0 && !far && c.Due(TDash))
            {
                d.Turbo = c.Evolved || (c.Base >= 5 && c.Every(0, 4));
                float dashSpeed = d.Turbo ? 17f : 13f;
                d.DashVel = (e.Pos[t] - dog.Pos).Normalized() * dashSpeed;
                d.DashHits.Clear();
                c.Reset(TDashing, d.Turbo ? 0.7f : 0.3f);
                c.Reset(TDash, c.Evolved ? c.Period(c.Param("turboEvery", 4f)) : c.Stat(StatId.Cooldown));
                c.Sound(d.Turbo ? 1 : 0, dog.Pos);
            }
        }
    }

    /// <summary>
    /// Camiseta ao Contrário: esquiva vem dos "grants". Ao esquivar — nível 3: impulso de velocidade;
    /// nível 4: imagem residual que confunde (desacelera muito) inimigos próximos; nível 5: a primeira
    /// esquiva depois de 6 s interrompe inimigos comuns ao redor. Evoluída (Moda Incompreensível): toda
    /// esquiva solta uma onda que fere e empurra.
    /// </summary>
    public sealed class CamisetaItem : ItemBehavior
    {
        const int TSpeedLeft = 0, TWaveCd = 1;
        static readonly int[] Hits = new int[128];

        public override void OnDodge(World w, int slot, AbilityInstance item)
        {
            var c = CtxOf(w, slot, item);
            if (c.Base >= 3)
            {
                int src = BuffSource(w, c.S);
                c.P.Stats.RemoveSource(src);
                c.P.Stats.Add(new Modifier { Stat = StatId.MoveSpeed, Op = ModOp.Mult, Value = c.Param("dodgeSpeed", 0.2f), SourceId = src });
                c.Reset(TSpeedLeft, 1.5f);
                c.S.Counters[0] = 1;
            }
            if (c.Base >= 4)
                w.Zones.Spawn(new Zone { Owner = (byte)slot, Ability = (short)c.Ability, Sprite = AllySprite.Afterimage, Pos = c.Pos, Radius = c.Radius(2f), Life = 1.5f, Interval = 0.3f, Slow = 0.6f, Follow = -1 });
            if (c.Base >= 5 && c.Due(TWaveCd))
            {
                var e = w.Enemies;
                float r = c.Radius(3f);
                int n = AbilitySystem.Overlapping(w, c.Pos, r, Hits);
                for (int h = 0; h < n; h++)
                    if ((e.Flags[Hits[h]] & EnemyFlags.Elite) == 0) e.StunUntil[Hits[h]] = Math.Max(e.StunUntil[Hits[h]], w.Elapsed + 0.6f);
                Area.Burst(w, slot, c.Ability, c.Pos, r, 0, 0, 0, FxStyle.Dodge);
                c.Reset(TWaveCd, c.Param("waveCooldown", 6f));
            }
            if (c.Evolved) Area.Burst(w, slot, c.Ability, c.Pos, c.Radius(2.5f), c.Dmg(1.5f), 3f, 0f, FxStyle.Dodge);
        }

        protected override void Step(ref Ctx c, float dt)
        {
            if (c.S.Counters[0] == 1 && c.Due(TSpeedLeft)) { c.P.Stats.RemoveSource(BuffSource(c.W, c.S)); c.S.Counters[0] = 0; }
        }
    }

    /// <summary>
    /// Mamão "Só Quer Mamão": orbita o jogador e solta pulsos de cura (dono e, reduzida, aliados
    /// próximos). Nível 4: cura excedente vira escudo (até 15% da vida máxima); nível 5: a cada 3º
    /// pulso, explosão de polpa que empurra e fere. Evoluído (Supremo): campo de suporte amplo, pulso
    /// mais frequente e escudo até 25%.
    /// </summary>
    public sealed class MamaoItem : ItemBehavior
    {
        const int TPulse = 0;

        protected override void Step(ref Ctx c, float dt)
        {
            var comp = c.W.Companions;
            int id = c.S.Companions[0];
            if (id < 0 || !comp.Items[id].Active || comp.Items[id].Ability != c.Ability) id = c.S.Companions[0] = comp.Spawn(c.Slot, c.Ability, AllySprite.Papaya, c.Pos);
            if (id >= 0)
            {
                double ang = c.W.Elapsed * Math.PI * 2 / 3;
                comp.Items[id].Pos = c.Pos + new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang) * 0.7f) * 1.3f;
            }
            if (c.Evolved && (c.S.Companions[1] < 0 || !HasZone(c.W, c.Slot)))
            {
                c.W.Zones.Spawn(new Zone { Owner = (byte)c.Slot, Ability = (short)c.Ability, Sprite = AllySprite.ZoneHeal, Pos = c.Pos, Radius = c.Radius(3.5f), Life = 9999f, Follow = (sbyte)c.Slot });
                c.S.Companions[1] = 1;
            }
            if (!c.Due(TPulse)) return;
            float radius = c.Radius(c.Evolved ? 3.5f : 3f);
            float shieldCap = c.Evolved ? c.Param("evoShieldCap", 0.25f) : c.Base >= 4 ? c.Param("shieldCap", 0.15f) : 0f;
            float pct = c.Param(c.Evolved ? "evoHeal" : "heal", c.Evolved ? 0.05f : 0.04f);
            foreach (var p in c.W.Players.Slots)
            {
                if (p == null || !p.IsActive) continue;
                bool self = p.Slot == c.Slot;
                if (!self && Vec2.DistanceSq(p.Pos, c.Pos) > radius * radius) continue;
                Heal(p, p.Stats.Get(StatId.MaxHp) * (self ? pct : pct * 0.5f), shieldCap);
            }
            c.W.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = c.Slot, B = (int)FxStyle.Heal, X = c.Pos.X, Y = c.Pos.Y, Value = radius });
            if (c.Base >= 5 && c.Every(0, 3)) Area.Burst(c.W, c.Slot, c.Ability, c.Pos, c.Radius(2.2f), c.Dmg(), 3f, 0f, FxStyle.Food);
            c.Sound(0);
            c.Reset(TPulse, c.Evolved ? c.Period(c.Param("evoInterval", 3f)) : c.Stat(StatId.Cooldown));
        }

        static bool HasZone(World w, int slot)
        {
            for (int i = 0; i < w.Zones.Count; i++) if (w.Zones.Items[i].Owner == slot && w.Zones.Items[i].Sprite == AllySprite.ZoneHeal) return true;
            return false;
        }
    }

    /// <summary>
    /// Tequila: euforia periódica (+10% de velocidade de ataque, nível 4 também +10% de movimento);
    /// depois, penalidade curta de velocidade dos projéteis (menor no nível 3). Nível 5: pulso ao fim da
    /// euforia. Evoluída (Fiesta Caótica): durante a euforia +1 projétil/golpe e recarga −20%; no fim,
    /// onda de impacto grande e breve recuperação. Efeito fictício de jogo; nunca mexe nos controles.
    /// </summary>
    public sealed class TequilaItem : ItemBehavior
    {
        const int TNext = 0, TPhase = 1;
        const int Idle = 0, Euphoria = 1, Hangover = 2;

        protected override void Step(ref Ctx c, float dt)
        {
            int src = BuffSource(c.W, c.S);
            ref int phase = ref c.S.Counters[0];
            var stats = c.P.Stats;
            if (phase == Idle && c.Due(TNext) && c.Nearest(8f) >= 0)
            {
                stats.RemoveSource(src);
                stats.Add(new Modifier { Stat = StatId.Cooldown, Op = ModOp.Mult, Value = -c.Param(c.Evolved ? "evoAttackSpeed" : "attackSpeed", c.Evolved ? 0.2f : 0.1f), SourceId = src, Scope = ModScope.AllAbilities });
                if (c.Base >= 4) stats.Add(new Modifier { Stat = StatId.MoveSpeed, Op = ModOp.Mult, Value = c.Param("moveBonus", 0.1f), SourceId = src });
                if (c.Evolved) stats.Add(new Modifier { Stat = StatId.Count, Op = ModOp.Add, Value = 1, SourceId = src, Scope = ModScope.AllAbilities });
                c.W.Events.AddCosmetic(new CosmeticEvent { Kind = CosmeticKind.Burst, A = c.Slot, B = (int)FxStyle.Fiesta, X = c.Pos.X, Y = c.Pos.Y, Value = 1.2f });
                c.Sound(0);
                phase = Euphoria;
                c.Reset(TPhase, Math.Max(1f, c.Stat(StatId.Duration)));
            }
            else if (phase == Euphoria && c.Due(TPhase))
            {
                stats.RemoveSource(src);
                float penalty = c.Base >= 3 ? c.Param("penaltyReduced", 0.07f) : c.Param("penalty", 0.15f);
                stats.Add(new Modifier { Stat = StatId.ProjectileSpeed, Op = ModOp.Mult, Value = -penalty, SourceId = src, Scope = ModScope.AllAbilities });
                if (c.Evolved) Area.Burst(c.W, c.Slot, c.Ability, c.Pos, c.Radius(3.5f), c.Dmg(2f), 4f, 0.3f, FxStyle.Fiesta);
                else if (c.Base >= 5) Area.Burst(c.W, c.Slot, c.Ability, c.Pos, c.Radius(2.5f), c.Dmg(), 2.5f, 0f, FxStyle.Fiesta);
                c.Sound(1);
                phase = Hangover;
                c.Reset(TPhase, c.Param(c.Evolved ? "evoRecovery" : "penaltySeconds", c.Evolved ? 1.5f : 2f));
            }
            else if (phase == Hangover && c.Due(TPhase))
            {
                stats.RemoveSource(src);
                phase = Idle;
                c.Reset(TNext, c.Stat(StatId.Cooldown));
            }
        }
    }

    /// <summary>
    /// Chapéu de Japonês: velocidade dos projéteis e alcance vêm dos "grants". Nível 4: 20% dos tiros
    /// ganham +1 perfuração; nível 5: a cada 3 s, o próximo tiro ganha +2 perfuração e 25% de
    /// velocidade. Evoluído (Mestre dos Projéteis): 25% dos tiros ganham +1 perfuração ou +1 ricochete
    /// (no máximo 3 ricochetes por tiro).
    /// </summary>
    public sealed class ChapeuItem : ItemBehavior
    {
        const int TFocus = 0;

        public override void OnShotSpawned(World w, AbilityInstance item, ref Shot s)
        {
            if (s.Depth > 0 || s.Ability == item.AbilityIndex) return;
            var c = CtxOf(w, s.Owner, item);
            if (c.Base >= 4 && w.Rng.NextFloat() < c.Param("pierceChance", 0.2f)) s.Pierce++;
            if (c.Base >= 5 && c.Due(TFocus))
            {
                s.Pierce += 2;
                s.Vel = s.Vel * 1.25f;
                c.Reset(TFocus, c.Param("focusEvery", 3f));
            }
            if (c.Evolved && w.Rng.NextFloat() < c.Param("evoChance", 0.25f))
            {
                if (s.Ricochets < 3 && w.Rng.NextFloat() < 0.5f) { s.Ricochets++; if (s.RicochetRange <= 0) s.RicochetRange = 4f; }
                else s.Pierce++;
            }
        }
    }
}
