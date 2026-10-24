using System;

namespace ES.Sim
{
    public static class XpCurve
    {
        /// <summary>XP do nível L para L+1: round(a + b·(L−1) + c·(L−1)²).</summary>
        public static int ToNext(RulesDef r, int level)
        {
            int n = Math.Max(0, level - 1);
            return (int)Math.Round(r.xpA + r.xpB * n + r.xpC * n * n, MidpointRounding.AwayFromZero);
        }
    }
}
