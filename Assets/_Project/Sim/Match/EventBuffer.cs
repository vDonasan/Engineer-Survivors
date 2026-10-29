using System.Collections.Generic;

namespace ES.Sim
{
    public enum RuleEventKind
    {
        EnemyDied, GemSpawned, GemCollected, LevelUpOffer, ChoiceApplied, SynergyChanged,
        EvolutionApplied, SpecialActivated, PlayerDowned, PlayerInactive, MatchEnded,
        /// <summary>A = boss (255 dueto), X = segundo de chegada.</summary>
        BossIncoming,
        /// <summary>Próximo boss da contagem: A = boss (254 nenhum, 255 dueto), X = segundo.</summary>
        BossSchedule,
        /// <summary>A = boss, B = id do inimigo, C = ciclo do Modo Infinito, X/Y = posição.</summary>
        BossSpawned,
        /// <summary>Fúria (BRAVO): A = boss, B = id do inimigo.</summary>
        BossBravo,
        /// <summary>A = id do inimigo, B = <see cref="BossAttack"/>, C = <see cref="BossState"/>, X = duração da preparação.</summary>
        BossAction,
        /// <summary>Área sinalizada: A = forma | estilo &lt;&lt; 4 (255 = limpar todas), B = tamanho × 20, C = atraso em ms, Z/W = extra/largura.</summary>
        BossHazard,
        /// <summary>A = boss, B = id do inimigo, C = 1 se encerrou a partida padrão.</summary>
        BossDefeated,
        /// <summary>O anfitrião continuou no Modo Infinito: X = segundo.</summary>
        MatchContinued,
        /// <summary>Evento aleatório: A = evento, B = jogador (255 todos/arena), C = id, X = duração restante, Y = aviso.</summary>
        EventStarted,
        /// <summary>A = id, B = abates, C = meta.</summary>
        EventProgress,
        /// <summary>A = id, B = <see cref="EventOutcome"/>, C = evento, X = jogador (−1 arena).</summary>
        EventEnded,
        /// <summary>Recompensa/efeito pontual: A = jogador (255 arena), B = tipo, X/Y = posição, Z = valor.</summary>
        EventBonus,
        /// <summary>Trajetória da moto: A = id, B = comprimento × 10, C = atraso em ms, X/Y = início, Z = ângulo, W = velocidade.</summary>
        EventMoto,
        /// <summary>Inimigo reciclado (longe de todos): some sem abate, explosão ou som. A = id, C = tick.</summary>
        EnemyRemoved,
        /// <summary>Totem no chão (Coxinha): A = id do evento, B = 1 se Suprema, X/Y = posição, Z = alcance.</summary>
        EventTotem,
    }

    /// <summary>Evento de regra: entregue de forma confiável a todos os clientes.</summary>
    public struct RuleEvent
    {
        public RuleEventKind Kind;
        public int A, B, C;
        public float X, Y;
        /// <summary>Só BossHazard: ângulo/raio interno e largura.</summary>
        public float Z, W;
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
