using System.Collections.Generic;

namespace ES.Sim
{
    public enum RuleEventKind
    {
        EnemyDied, GemSpawned, GemCollected, LevelUpOffer, ChoiceApplied, SynergyChanged,
        EvolutionApplied, SpecialActivated, PlayerDowned, PlayerInactive, MatchEnded,
    }

    /// <summary>Evento de regra: entregue de forma confiável a todos os clientes.</summary>
    public struct RuleEvent
    {
        public RuleEventKind Kind;
        public int A, B, C;
        public float X, Y;
    }

    public enum CosmeticKind { Damage, ChainArc, ProjectileFired, Hit, EnemyTelegraph, WaveStarted }

    /// <summary>Evento cosmético: pode se perder sem afetar a partida.</summary>
    public struct CosmeticEvent
    {
        public CosmeticKind Kind;
        public int A, B;
        public float X, Y, X2, Y2, Value;
    }

    public sealed class EventBuffer
    {
        public readonly List<RuleEvent> Rule = new List<RuleEvent>(64);
        public readonly List<CosmeticEvent> Cosmetic;
        readonly int _cosmeticCap;

        public EventBuffer(int cosmeticCap)
        {
            _cosmeticCap = cosmeticCap;
            Cosmetic = new List<CosmeticEvent>(cosmeticCap);
        }

        public void AddRule(in RuleEvent e) => Rule.Add(e);

        /// <summary>Retorna false (e descarta) quando o limite foi atingido.</summary>
        public bool AddCosmetic(in CosmeticEvent e)
        {
            if (Cosmetic.Count >= _cosmeticCap) return false;
            Cosmetic.Add(e);
            return true;
        }

        public void Clear()
        {
            Rule.Clear();
            Cosmetic.Clear();
        }
    }
}
