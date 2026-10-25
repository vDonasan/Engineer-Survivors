using System;

namespace ES.Sim
{
    /// <summary>
    /// Base dos itens (Protoboard, Cenoura Podre...): um comportamento de habilidade com ganchos
    /// de eventos do jogo. Valores vêm dos atributos e parâmetros da habilidade no conteúdo.
    /// </summary>
    public abstract class ItemBehavior : CharacterWeapon
    {
        public virtual void OnEnemyHit(World w, int slot, AbilityInstance item, int ability, int enemy) { }
        public virtual void OnDodge(World w, int slot, AbilityInstance item) { }
        public virtual void OnLevelUp(World w, int slot, AbilityInstance item) { }
        public virtual void OnXpGained(World w, int slot, AbilityInstance item) { }
        public virtual void OnShotSpawned(World w, AbilityInstance item, ref Shot s) { }

        protected override void Step(ref Ctx c, float dt) { }

        /// <summary>Ctx a partir de um gancho (fora do tick do item).</summary>
        protected static Ctx CtxOf(World w, int slot, AbilityInstance a) =>
            new Ctx(w, slot, a, a.State as WeaponState ?? (WeaponState)(a.State = new WeaponState()));

        /// <summary>Fonte própria para bônus temporários (não colide com os bônus permanentes do item).</summary>
        protected static int BuffSource(World w, WeaponState s)
        {
            if (s.Counters[7] == 0) s.Counters[7] = w.NextSourceId++;
            return s.Counters[7];
        }

        protected static bool HasAbility(PlayerState p, int abilityIndex)
        {
            if (abilityIndex < 0) return false;
            foreach (var a in p.Abilities) if (a.AbilityIndex == abilityIndex) return true;
            return false;
        }

        /// <summary>Cura com teto e, a partir do nível pedido, converte o excesso em escudo (limitado).</summary>
        protected static void Heal(PlayerState p, float amount, float shieldCapFraction)
        {
            float max = p.Stats.Get(StatId.MaxHp);
            float missing = max - p.Hp;
            float heal = Math.Min(missing, amount);
            p.Hp += heal;
            if (shieldCapFraction > 0 && amount > heal) p.Shield = Math.Min(max * shieldCapFraction, p.Shield + (amount - heal));
        }
    }

    /// <summary>Encaminha eventos do jogo para os itens dos jogadores (sem alocar: chamado a cada acerto).</summary>
    public static class ItemHooks
    {
        static bool _inHit;

        static PlayerState Player(World w, int slot) => w != null && slot >= 0 && slot < PlayerStore.MaxSlots ? w.Players.Slots[slot] : null;
        static ItemBehavior Item(AbilityInstance a) => AbilitySystem.For(a.Def.Def.behavior) as ItemBehavior;

        /// <summary>Acerto de uma habilidade; descargas e explosões disparadas aqui não reentram.</summary>
        public static void OnEnemyHit(World w, int slot, int ability, int enemy)
        {
            var p = Player(w, slot);
            if (p == null || _inHit) return;
            _inHit = true;
            try { for (int i = 0; i < p.Abilities.Count; i++) Item(p.Abilities[i])?.OnEnemyHit(w, slot, p.Abilities[i], ability, enemy); }
            finally { _inHit = false; }
        }

        public static void OnDodge(World w, int slot)
        {
            var p = Player(w, slot);
            if (p != null) for (int i = 0; i < p.Abilities.Count; i++) Item(p.Abilities[i])?.OnDodge(w, slot, p.Abilities[i]);
        }

        public static void OnLevelUp(World w, int slot)
        {
            var p = Player(w, slot);
            if (p != null) for (int i = 0; i < p.Abilities.Count; i++) Item(p.Abilities[i])?.OnLevelUp(w, slot, p.Abilities[i]);
        }

        public static void OnXpGained(World w, int slot)
        {
            var p = Player(w, slot);
            if (p != null) for (int i = 0; i < p.Abilities.Count; i++) Item(p.Abilities[i])?.OnXpGained(w, slot, p.Abilities[i]);
        }

        public static void OnShotSpawned(World w, ref Shot s)
        {
            var p = Player(w, s.Owner);
            if (p != null) for (int i = 0; i < p.Abilities.Count; i++) Item(p.Abilities[i])?.OnShotSpawned(w, p.Abilities[i], ref s);
        }
    }
}
