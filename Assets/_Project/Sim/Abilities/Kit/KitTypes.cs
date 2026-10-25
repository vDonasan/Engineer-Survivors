namespace ES.Sim
{
    /// <summary>
    /// Aparência de tiros, zonas e companheiros dos jogadores. O valor vai no snapshot
    /// (1 byte); a apresentação procura o sprite "ally_&lt;nome em minúsculas&gt;_&lt;quadro&gt;".
    /// Só acrescente no fim.
    /// </summary>
    public enum AllySprite : byte
    {
        Laser, Bubble, BubbleBig, Briefcase, Coin,
        Digit, Expression, Sushi, Sashimi, Plate,
        Resistor, Transistor, PowerSupply, ScopeWave, ZoneElectric, ZoneStorm,
        Burger, Dorito, Tire, TireOrbit,
        Pill, Syringe, SyringeSpecial,
        BotShooter, BotBrawler, BotDasher, BotMaster, BotBolt, MasterBolt,
        Phone, Paper, Computer, Fragment,
        Jabuti, JabutiWalk, Car,
        Insulin, Vial,
        SoundWave, Bottle,
    }

    /// <summary>Estilo visual de explosões, avisos, golpes e arcos (cor e partículas).</summary>
    public enum FxStyle : byte
    {
        Electric, Charged, Pop, Coins, Math, Food, Impact, Energy, Paper, Tech, Sound, Glass, Insulin, Robot, Drift, Circuit, Storm,
    }

    public enum ShotMotion : byte
    {
        /// <summary>Reta.</summary>
        Linear,
        /// <summary>Reta com ondulação lateral (Curve = amplitude, Frequency = Hz).</summary>
        Wave,
        /// <summary>Curva até o ponto mirado (Curve = giro máximo em rad/s), depois reta.</summary>
        Curve,
        /// <summary>Persegue o inimigo mais próximo (Curve = giro máximo em rad/s).</summary>
        Homing,
        /// <summary>Rola desacelerando (Curve = desaceleração) e quica nas paredes; termina parado.</summary>
        Roll,
    }

    /// <summary>Efeito aplicado ao inimigo atingido por um tiro.</summary>
    public enum ShotStatus : byte
    {
        None,
        /// <summary>Marca por StatusTime segundos (Miyuki).</summary>
        Mark,
        /// <summary>Se o alvo estiver marcado, consome a marca e explode (BurstRadius/BurstDamage).</summary>
        ConsumeMark,
        /// <summary>Gruda: StatusValue de dano depois de StatusTime segundos (Garcia).</summary>
        Sticky,
        /// <summary>Prende: atordoa por StatusTime e causa StatusValue de dano por segundo (Staut).</summary>
        Trap,
    }

    public static class ShotBurst
    {
        public const byte OnHit = 1, OnEnd = 2;
    }

    /// <summary>Bits de estado visual do inimigo enviados no snapshot.</summary>
    public static class EnemyStatus
    {
        public const byte Trapped = 1, Marked = 2, Sticky = 4, Slowed = 8;

        public static byte Bits(EnemyStore e, int i, float now)
        {
            byte b = 0;
            if (e.TrapUntil[i] > now) b |= Trapped;
            if (e.MarkUntil[i] > now) b |= Marked;
            if (e.DelayAt[i] > now) b |= Sticky;
            if (e.SlowUntil[i] > now && e.SlowAmount[i] > 0) b |= Slowed;
            return b;
        }
    }

    /// <summary>
    /// Comportamentos que reagem ao fim de seus tiros e golpes atrasados (moedas da maleta,
    /// fragmentos, jabuti que continua andando). Filhos só até a profundidade <see cref="MaxDepth"/>.
    /// </summary>
    public interface IShotHandler
    {
        void OnShotHit(World w, ref Shot s, int enemy);
        void OnShotEnd(World w, ref Shot s);
        void OnStrike(World w, in Strike s);
    }

    public static class Kit
    {
        public const int MaxDepth = 2;
    }
}
