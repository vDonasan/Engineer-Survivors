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

    /// <summary>
    /// ChainArc: Value = <see cref="FxStyle"/>. ProjectileFired: disparo (som), B = habilidade, Value = variante.
    /// Burst: Value = raio, B = estilo. AreaMark: X2 = raio, Value = atraso, B = estilo.
    /// Swing: (X2, Y2) = direção × alcance, Value = abertura em graus, B = estilo.
    /// </summary>
    public enum CosmeticKind { Damage, ChainArc, ProjectileFired, Hit, EnemyTelegraph, WaveStarted, Burst, AreaMark, Swing }

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

        /// <summary>
        /// Retorna false (e descarta) quando o limite foi atingido. Números de dano só usam
        /// metade da cota, para não apagar os efeitos dos ataques.
        /// </summary>
        public bool AddCosmetic(in CosmeticEvent e)
        {
            if (Cosmetic.Count >= _cosmeticCap) return false;
            if (e.Kind == CosmeticKind.Damage && ++_damageCount > _cosmeticCap / 2) return false;
            Cosmetic.Add(e);
            return true;
        }

        int _damageCount;

        public void Clear()
        {
            Rule.Clear();
            Cosmetic.Clear();
            _damageCount = 0;
        }
    }
}
