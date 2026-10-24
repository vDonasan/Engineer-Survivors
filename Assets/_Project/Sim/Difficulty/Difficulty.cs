namespace ES.Sim
{
    public enum Difficulty { Easy, Normal, Hard }

    public static class Difficulties
    {
        public static string Id(Difficulty d) => d == Difficulty.Easy ? "easy" : d == Difficulty.Hard ? "hard" : "normal";

        public static bool TryParse(string id, out Difficulty d)
        {
            switch (id)
            {
                case "easy": d = Difficulty.Easy; return true;
                case "normal": d = Difficulty.Normal; return true;
                case "hard": d = Difficulty.Hard; return true;
                default: d = Difficulty.Normal; return false;
            }
        }
    }
}
