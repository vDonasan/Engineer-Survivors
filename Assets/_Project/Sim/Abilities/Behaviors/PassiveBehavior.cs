namespace ES.Sim
{
    /// <summary>Componentes e atributos: só modificadores (aplicados em Grant/LevelUp).</summary>
    public sealed class PassiveBehavior : IAbilityBehavior
    {
        public void OnAcquire(World w, int slot, AbilityInstance a) { }
        public void Tick(World w, int slot, AbilityInstance a, float dt) { }
    }
}
