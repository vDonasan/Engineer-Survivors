using System.Collections.Generic;

namespace ES.Sim
{
    public struct OwnedAbility
    {
        public int Ability;
        public int Level;
    }

    public struct RequirementProgress
    {
        public int Met, Total;
        public bool Done => Total > 0 && Met >= Total;
    }

    /// <summary>Habilidades de um jogador como a interface as vê (igual no host e nos clientes).</summary>
    public sealed class PlayerLoadout
    {
        public int Specialization;
        public readonly List<OwnedAbility> Abilities = new List<OwnedAbility>(16);
        public readonly HashSet<int> ActiveSynergies = new HashSet<int>();
        public readonly HashSet<int> Evolved = new HashSet<int>();

        public int LevelOf(int ability)
        {
            foreach (var a in Abilities) if (a.Ability == ability) return a.Level;
            return 0;
        }

        public int IndexOf(int ability)
        {
            for (int i = 0; i < Abilities.Count; i++) if (Abilities[i].Ability == ability) return i;
            return -1;
        }
    }

    /// <summary>
    /// Reconstrói as habilidades de cada jogador a partir da escalação e dos eventos de
    /// regra (ChoiceApplied, EvolutionApplied, SynergyChanged), sem acesso ao World.
    /// </summary>
    public sealed class LoadoutTracker
    {
        readonly GameDatabase _db;
        public readonly PlayerLoadout[] Slots = new PlayerLoadout[PlayerStore.MaxSlots];
        /// <summary>Evoluções já descobertas nesta partida (descobríveis deixam de ser "???").</summary>
        public readonly HashSet<int> Discovered = new HashSet<int>();

        public LoadoutTracker(GameDatabase db) { _db = db; }

        public void Reset(MatchConfig cfg)
        {
            Discovered.Clear();
            for (int i = 0; i < Slots.Length; i++) Slots[i] = null;
            if (cfg?.Roster == null) return;
            for (int i = 0; i < cfg.Roster.Length && i < Slots.Length; i++)
            {
                int spec = _db.SpecializationIndex(cfg.Roster[i].SpecializationId);
                var l = new PlayerLoadout { Specialization = spec };
                if (spec >= 0) l.Abilities.Add(new OwnedAbility { Ability = _db.Specialization(spec).StartingAbility, Level = 1 });
                Slots[i] = l;
            }
        }

        public void Apply(in RuleEvent e)
        {
            if (e.A < 0 || e.A >= Slots.Length) return;
            var l = Slots[e.A];
            if (l == null) return;
            switch (e.Kind)
            {
                case RuleEventKind.ChoiceApplied:
                    if ((OptionKind)e.B == OptionKind.Evolution || e.C < 0 || e.C >= _db.AbilityCount) return;
                    switch ((OptionKind)e.B)
                    {
                        case OptionKind.NewAbility:
                        case OptionKind.Attribute:
                            if (l.IndexOf(e.C) < 0) l.Abilities.Add(new OwnedAbility { Ability = e.C, Level = 1 });
                            break;
                        case OptionKind.Upgrade:
                            int at = l.IndexOf(e.C);
                            if (at >= 0)
                            {
                                var a = l.Abilities[at];
                                if (a.Level < _db.Ability(a.Ability).MaxLevel) a.Level++;
                                l.Abilities[at] = a;
                            }
                            break;
                    }
                    break;
                case RuleEventKind.EvolutionApplied:
                    if (e.B < 0 || e.B >= _db.EvolutionCount) return;
                    var evo = _db.Evolution(e.B);
                    int from = l.IndexOf(evo.From);
                    var result = new OwnedAbility { Ability = evo.Result, Level = 1 };
                    if (from >= 0) l.Abilities[from] = result;
                    else l.Abilities.Add(result);
                    l.Evolved.Add(e.B);
                    Discovered.Add(e.B);
                    break;
                case RuleEventKind.SynergyChanged:
                    if (e.C != 0) l.ActiveSynergies.Add(e.B);
                    else l.ActiveSynergies.Remove(e.B);
                    break;
            }
        }

        /// <summary>Quantos requisitos da evolução o jogador já atende.</summary>
        public RequirementProgress Progress(int slot, int evolution)
        {
            var p = new RequirementProgress();
            var l = slot >= 0 && slot < Slots.Length ? Slots[slot] : null;
            if (l == null || evolution < 0 || evolution >= _db.EvolutionCount) return p;
            foreach (var r in _db.Evolution(evolution).Requires)
            {
                p.Total++;
                switch (r.Kind)
                {
                    case RequirementKind.AbilityLevel:
                        if (l.LevelOf(r.TargetIndex) >= r.Value) p.Met++;
                        break;
                    case RequirementKind.AnyAbilityLevel:
                        foreach (int t in r.Targets) if (l.LevelOf(t) >= r.Value) { p.Met++; break; }
                        break;
                    case RequirementKind.TagCount:
                        int count = 0;
                        foreach (var a in l.Abilities) if (_db.Ability(a.Ability).Tags.Has(r.Tag)) count++;
                        if (count >= r.Value) p.Met++;
                        break;
                    case RequirementKind.Specialization:
                        if (l.Specialization == r.TargetIndex) p.Met++;
                        break;
                }
            }
            return p;
        }
    }
}
