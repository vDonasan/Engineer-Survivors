# Gera os eventos aleatórios (acontecimentos universitários) e suas regras de agenda.
# Uso: python3 Tools/content/eventos.py  (idempotente). Um evento novo pode reaproveitar um "behavior" existente
# só com outra configuração; um comportamento novo precisa de uma classe em Sim/Systems/EventSystem.cs.
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from personagens import PATH, dump  # noqa: E402


def par(**kw):
    return [{"key": k, "value": float(v)} for k, v in kw.items()]


def ev(id, name, desc, banner, kind, scope, behavior, weight, rarity, duration, warning=0, min_minute=0, infinite_weight=0,
       no_boss=False, params=None):
    return {
        "id": id, "name": name, "description": desc, "banner": banner, "kind": kind, "scope": scope, "behavior": behavior,
        "weight": weight, "rarity": rarity, "duration": duration, "warning": warning, "minMinute": min_minute,
        "infiniteWeightPerMinute": infinite_weight, "noBoss": no_boss, "parameters": par(**(params or {})),
    }


EVENTS = [
    ev("bolha_social", "Bolha Social", "Mais experiência e uma bolha que atrai inimigos. Derrote a meta para ganhar um bônus; cheia, a bolha explode.",
       "Sua rede de contatos está crescendo... e os inimigos também perceberam.", "especial", "player", "bubble",
       10, "incomum", 20, params=dict(xpGain=0.3, lureRadius=8, killGoal=25, rewardXp=0.4, growXp=0.6, popRadius=3.5, popDamage=30)),
    ev("dez_na_prova", "Dez na Prova", "Muita experiência para todos e sorte para cartas raras por 30 s.",
       "Nota máxima! Seu coeficiente de destruição aumentou.", "positivo", "all", "grade",
       7, "raro", 30, min_minute=1, params=dict(xpFraction=0.55, luck=1)),
    ev("zero_na_prova", "Zero na Prova", "Perde parte da experiência ainda não convertida, ataques 15% mais lentos e inimigos próximos mais rápidos. Derrote a meta para recuperar tudo.",
       "A prova veio difícil. Hora de recuperar os pontos no campo de batalha.", "negativo", "player", "zero",
       7, "incomum", 18, min_minute=2, params=dict(xpLoss=0.25, cooldown=0.15, hasteRadius=7, haste=0.25, hasteSeconds=6, killGoal=20, recoverBonus=0.2)),
    ev("chamado_de_burro", "Chamado de Burro", "Ataques 12% mais lentos, mas cada abate dá experiência extra. Bata a meta e ganhe +20% de dano.",
       "Duvidaram da sua capacidade. Mostre o contrário.", "negativo", "player", "insult",
       9, "comum", 15, params=dict(cooldown=0.12, xpPerKill=1.5, killGoal=15, damage=0.2, damageSeconds=8, burstRadius=2.5, burstDamage=20)),
    ev("laboratorio_incompleto", "Laboratório Incompleto", "Emergência: ondas extras e vazamentos de vapor. Derrote a meta antes do prazo para ganhar experiência e talvez uma melhoria.",
       "O prazo acabou. Sobreviva ao caos.", "especial", "global", "lab",
       7, "incomum", 30, warning=3, min_minute=2, infinite_weight=0.6, no_boss=True,
       params=dict(spawnMult=1.8, surge=15, killGoalPerPlayer=40, rewardXp=0.5, survivalXp=0.15, upgradeChance=0.35,
                   hazardEvery=3.5, hazardRadius=1.3, hazardDamage=10, hazardDelay=1.2)),
    ev("motoqueiro", "Atropelou o Motoqueiro", "Uma moto cruza a arena: derruba inimigos e machuca quem estiver no caminho. Desvie por pouco para ganhar Reflexo de Piloto.",
       "Cuidado! Trânsito caótico na arena.", "especial", "global", "moto",
       9, "comum", 4, warning=1.6, min_minute=1, infinite_weight=0.5,
       params=dict(speed=22, width=1.2, enemyDamage=60, playerDamage=18, knock=3, nearMiss=3.5, perfectKills=3, reflexSpeed=0.25, reflexSeconds=6, extraBikesAfterMinutes=5)),
    ev("coxinha", "Ganhou uma Coxinha", "Uma coxinha GIGANTE vira totem no chão por 20 s: solta experiência e quem fica no alcance ganha +15% de dano, +15% de velocidade e regeneração. Às vezes é a Coxinha Suprema, que também pulsa dano.",
       "A verdadeira recompensa universitária chegou.", "positivo", "player", "coxinha",
       12, "comum", 20, params=dict(radius=3.5, supremeRadius=4.5, xpEvery=1.2, xp=3, damage=0.15, speed=0.15, regen=3,
                                     supremeChance=0.15, supremePulseEvery=2, supremeDamage=25)),
    ev("jalapeno", "Jalapeño", "Calor extremo: +20% de dano, ataques 20% mais rápidos e queimadura ao redor; no fim, uma explosão de calor.",
       "Está quente demais! Aproveite o poder da pimenta.", "positivo", "player", "jalapeno",
       9, "incomum", 10, min_minute=1,
       params=dict(damage=0.2, cooldown=-0.2, burnEvery=0.5, burnRadius=2.2, burnDamage=6, endRadius=3.2, endDamage=35, endSlow=0.25, endSlowSeconds=1.5,
                   stinkDps=8, stinkSeconds=3)),
]

RULES = {
    "eventFirst": 75, "eventIntervalMin": 60, "eventIntervalMax": 120, "eventInfiniteInterval": 0.7, "eventMaxActive": 3,
    "eventNoRepeat": 2,
}


def main():
    with open(PATH, encoding='utf-8') as f:
        d = json.load(f)
    d["events"] = EVENTS
    d["rules"].update(RULES)
    with open(PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write(dump(d))
    print("eventos ok:", len(EVENTS))


if __name__ == "__main__":
    main()
