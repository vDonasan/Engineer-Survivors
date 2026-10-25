# Gera as entradas dos personagens no conteúdo canônico (sp1-content.json).
# Uso: python3 Tools/content/personagens.py  (idempotente: substitui as entradas pelos ids)
import json, os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
PATH = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json')


def st(**kw): return [{"stat": k, "value": v} for k, v in kw.items()]
def lv(text, add=None, mult=None): return {"add": st(**(add or {})), "mult": st(**(mult or {})), "text": text}
def par(**kw): return [{"key": k, "value": v} for k, v in kw.items()]


def weapon(id, name, desc, behavior, tags, base, levels, sfx, params=None, evolved=False):
    return {
        "id": id, "name": name, "description": desc, "kind": "weapon", "rarity": "raro" if evolved else "comum",
        "behavior": behavior, "tags": tags, "maxLevel": 3 if evolved else 5, "evolutionOnly": evolved, "exclusive": True,
        "sfx": sfx, "baseStats": st(**base), "levels": levels, "grants": [],
        "parameters": par(**({"evolved": 1} if evolved else {}), **(params or {})),
    }


def component(id, name, desc, tags, grants):
    return {
        "id": id, "name": name, "description": desc, "kind": "component", "rarity": "comum", "behavior": "passive",
        "tags": tags, "maxLevel": 5, "evolutionOnly": False, "exclusive": False, "sfx": [], "baseStats": [], "levels": [],
        "grants": [{"stat": s, "op": o, "scope": sc, "value": v} for s, o, sc, v in grants], "parameters": [],
    }


COMP = "Engenharia da Computação"
CHARS = [
    dict(id="staut", name="Staut", eng=COMP, special="overclock", tags=["Software", "Quimica"], comp="cartao_corporativo",
         weapon=weapon("bolhas", "Bolhas", "Bolhas flutuam em zigue-zague e estouram em área ao tocar um inimigo.", "staut", ["Software", "Quimica"],
                       dict(Damage=9, Cooldown=1.1, Range=7, ProjectileSpeed=3.2, Count=2, Area=1, Duration=1),
                       [lv("+1 bolha por rajada", add=dict(Count=1)),
                        lv("Bolha-prisão: a cada 3ª rajada uma bolha grande prende o alvo", add=dict(Damage=2)),
                        lv("+3 de dano, bolhas mais rápidas", add=dict(Damage=3, ProjectileSpeed=0.6)),
                        lv("Bolhas grandes atravessam um inimigo antes de prender; +1 bolha", add=dict(Count=1, Area=0.2))],
                       ["bolha", "bolha_grande", "maleta", "moedas"], dict(bubbleLife=3)),
         evo=weapon("bolhas_e_maletas", "Bolhas e Maletas", "As bolhas continuam; maletas de dinheiro atravessam a fila e abrem numa chuva de moedas.", "staut", ["Software", "Quimica"],
                    dict(Damage=14, Cooldown=1.0, Range=7.5, ProjectileSpeed=3.8, Count=4, Area=1.2, Duration=1),
                    [lv("+1 maleta por arremesso e +2 moedas por maleta", add=dict(Damage=3)),
                     lv("Avançada: as moedas ricocheteiam entre inimigos próximos", add=dict(Damage=3))],
                    ["bolha", "bolha_grande", "maleta", "moedas"], dict(bubbleLife=3, caseEvery=2.2), evolved=True)),
    dict(id="miyuki", name="Miyuki", eng=COMP, special="overclock", tags=["Software", "Sensores"], comp="cafe_expresso",
         weapon=weapon("calculos", "Cálculos Ultrarrápidos", "Rajadas de dígitos; expressões atravessam a fila e equações explodem em área.", "miyuki", ["Software", "Sensores"],
                       dict(Damage=6, Cooldown=0.85, Range=9, ProjectileSpeed=15, Count=3, Area=1, Duration=1),
                       [lv("+1 dígito por rajada", add=dict(Count=1)),
                        lv("Os dígitos ricocheteiam em outro alvo", add=dict(Damage=1)),
                        lv("+2 de dano, recarga −10%", add=dict(Damage=2), mult=dict(Cooldown=-0.1)),
                        lv("Ricochete duplo; equação maior a cada 6 s; +1 dígito", add=dict(Count=1))],
                       ["numero", "expressao", "equacao", "sushi", "esteira"]),
         evo=weapon("matematica_sushi", "Matemática, Sushi e Sashimi", "Os cálculos continuam e marcam; sushi em curva e sashimi ricocheteante explodem em alvos marcados.", "miyuki", ["Software", "Sensores"],
                    dict(Damage=10, Cooldown=0.75, Range=9.5, ProjectileSpeed=16, Count=5, Area=1.1, Duration=1),
                    [lv("+1 sushi por lançamento", add=dict(Damage=2)),
                     lv("Avançada: esteira de pratos explode em cadeia numa linha de inimigos", add=dict(Damage=2))],
                    ["numero", "expressao", "equacao", "sushi", "esteira"], evolved=True)),
    dict(id="giulia", name="Giulia", eng="Engenharia Elétrica", special="apagao", tags=["Energia", "Eletronica"], comp="bateria_portatil",
         weapon=weapon("componentes_eletricos", "Resistores e Transistores", "Resistores atravessam a fila; transistores soltam descargas que saltam. Acertos agrupados formam um campo elétrico.", "giulia", ["Energia", "Eletronica"],
                       dict(Damage=10, Cooldown=0.8, Range=9, ProjectileSpeed=16, Count=1, Pierce=3, Chains=2, ChainRange=3, ChainFalloff=0.2, Area=1, Duration=1),
                       [lv("+3 de dano", add=dict(Damage=3)),
                        lv("+1 resistor por disparo; a descarga salta +1", add=dict(Count=1, Chains=1)),
                        lv("Recarga −12%", mult=dict(Cooldown=-0.12)),
                        lv("A descarga salta +2; campo elétrico maior", add=dict(Chains=2, Area=0.15))],
                       ["resistor", "transistor", "fonte", "osciloscopio", "circuito", "tempestade"]),
         evo=weapon("fontes_osciloscopios", "Fontes e Osciloscópios", "Componentes continuam; fontes explodem em pulso, o osciloscópio dispara ondas e um circuito liga os inimigos.", "giulia", ["Energia", "Eletronica"],
                    dict(Damage=16, Cooldown=0.7, Range=10, ProjectileSpeed=17, Count=2, Pierce=4, Chains=5, ChainRange=3.5, ChainFalloff=0.15, Area=1.2, Duration=1),
                    [lv("O circuito liga até 10 inimigos", add=dict(Damage=3)),
                     lv("Avançada: tempestade eletromagnética ao seu redor", add=dict(Damage=3))],
                    ["resistor", "transistor", "fonte", "osciloscopio", "circuito", "tempestade"], evolved=True)),
    dict(id="breno", name="Breno", eng=COMP, special="overclock", tags=["Software", "Biomecanica"], comp="halter",
         weapon=weapon("lanches", "Hambúrgueres e Doritos", "Rajadas de Doritos perfurantes e hambúrgueres que empurram e fazem onda ao cair.", "breno", ["Software", "Biomecanica"],
                       dict(Damage=7, Cooldown=0.9, Range=8, ProjectileSpeed=14, Count=3, Pierce=2, Area=1, Duration=1),
                       [lv("+1 Dorito por rajada", add=dict(Count=1)),
                        lv("Um Dorito por rajada ricocheteia", add=dict(Damage=2)),
                        lv("+2 de dano, recarga −10%", add=dict(Damage=2), mult=dict(Cooldown=-0.1)),
                        lv("Todos os Doritos ricocheteiam, +1 perfuração, onda do hambúrguer maior", add=dict(Pierce=1))],
                       ["doritos", "hamburguer", "pneu"]),
         evo=weapon("pneus_crossfit", "Pneus de Crossfit", "Lanches continuam; pneus rolam atropelando, quicam nas paredes e fazem onda ao parar.", "breno", ["Software", "Biomecanica"],
                    dict(Damage=11, Cooldown=0.8, Range=8.5, ProjectileSpeed=15, Count=5, Pierce=3, Area=1.2, Duration=1),
                    [lv("Pneus maiores e mais pesados", add=dict(Damage=2)),
                     lv("Avançada: dois pneus orbitais giram ao seu redor", add=dict(Damage=2))],
                    ["doritos", "hamburguer", "pneu"], evolved=True)),
    dict(id="artur", name="Artur", eng=COMP, special="overclock", tags=["Biomecanica", "Suporte"], comp="marmita",
         weapon=weapon("bonesmashing", "Bonesmashing", "Socos em cone; a sequência termina num golpe carregado com onda de choque.", "artur", ["Biomecanica", "Suporte"],
                       dict(Damage=18, Cooldown=0.6, Range=2.1, Knockback=2, Area=1, Duration=1),
                       [lv("+4 de dano", add=dict(Damage=4)),
                        lv("Os socos interrompem o inimigo por um instante", add=dict(Knockback=1)),
                        lv("+2 de dano, recarga −12%", add=dict(Damage=2), mult=dict(Cooldown=-0.12)),
                        lv("Golpe carregado a cada 3 socos; onda de choque maior", add=dict(Range=0.3))],
                       ["soco", "soco_forte", "pilulas", "seringa"]),
         evo=weapon("pilulas_seringas", "Bonesmashing, Pílulas e Seringas", "Os socos continuam; rajadas de pílulas e seringas perfurantes, e a cada 3ª uma seringa que explode.", "artur", ["Biomecanica", "Suporte"],
                    dict(Damage=26, Cooldown=0.5, Range=2.2, Knockback=3, Area=1.15, Duration=1),
                    [lv("+1 seringa por disparo", add=dict(Damage=4)),
                     lv("Avançada: o golpe carregado dispara um anel de seringas", add=dict(Damage=4))],
                    ["soco", "soco_forte", "pilulas", "seringa"], evolved=True)),
    dict(id="badotti", name="Badotti", eng="Engenharia Mecatrônica", special="turbina_emergencia", tags=["Robotica", "Automacao"], comp="kit_arduino",
         weapon=weapon("cabazords", "Os Três Cabazords", "Três robôs autônomos: um atira, um briga de perto, um faz investidas. Os três no mesmo alvo = ataque combinado.", "badotti", ["Robotica", "Automacao"],
                       dict(Damage=9, Cooldown=0.9, Range=6, ProjectileSpeed=12, Area=1, Duration=1),
                       [lv("+3 de dano", add=dict(Damage=3)),
                        lv("Os robôs atacam mais rápido", mult=dict(Cooldown=-0.15)),
                        lv("O atirador dispara dois tiros", add=dict(Damage=2)),
                        lv("A investida termina em impacto; combo maior", add=dict(Damage=3, Area=0.2))],
                       ["robo_tiro", "robo_soco", "robo_dash", "robo_combo", "master_soco", "master_chao", "master_salto"]),
         evo=weapon("cabazord_master", "Cabazord Master", "Os três se unem: soco mecânico em área, disparo de energia, investida e golpe no chão.", "badotti", ["Robotica", "Automacao"],
                    dict(Damage=18, Cooldown=0.8, Range=7, ProjectileSpeed=13, Area=1.2, Duration=1),
                    [lv("Golpe no chão mais frequente", add=dict(Damage=4)),
                     lv("Avançada: salto combinado com o Badotti", add=dict(Damage=4))],
                    ["robo_tiro", "robo_soco", "robo_dash", "robo_combo", "master_soco", "master_chao", "master_salto"], evolved=True)),
    dict(id="garcia", name="Garcia", eng=COMP, special="overclock", tags=["Software", "Eletronica"], comp="cabo_hdmi",
         weapon=weapon("celulares_cola", "Celulares e Papéis de Cola", "Celulares ricocheteiam; leques de papéis de cola grudam e estouram depois.", "garcia", ["Software", "Eletronica"],
                       dict(Damage=11, Cooldown=1.1, Range=9, ProjectileSpeed=11, Count=1, Area=1, Duration=1),
                       [lv("+1 papel no leque", add=dict(Damage=2)),
                        lv("Celulares ricocheteiam duas vezes", add=dict(Damage=2)),
                        lv("+1 celular por arremesso", add=dict(Count=1)),
                        lv("+2 papéis; a cola estoura mais forte", add=dict(Damage=3))],
                       ["celular", "papel", "computador", "tv", "chuva"]),
         evo=weapon("computadores_tvs", "Computadores e Televisões", "Celulares e cola continuam; computadores explodem em fragmentos e TVs caem em áreas marcadas.", "garcia", ["Software", "Eletronica"],
                    dict(Damage=17, Cooldown=1.0, Range=9.5, ProjectileSpeed=12, Count=2, Area=1.2, Duration=1),
                    [lv("+1 TV por queda", add=dict(Damage=3)),
                     lv("Avançada: chuva de eletrônicos ao seu redor", add=dict(Damage=3))],
                    ["celular", "papel", "computador", "tv", "chuva"], evolved=True)),
    dict(id="jean", name="Jean", eng=COMP, special="overclock", tags=["Software", "Mecanica"], comp="turbina",
         weapon=weapon("jabutis", "Jabutis", "Montado no porco, lança jabutis em curva que continuam andando quando caem.", "jean", ["Software", "Mecanica"],
                       dict(Damage=10, Cooldown=1.2, Range=8, ProjectileSpeed=7, Count=2, Pierce=2, Area=1, Duration=1),
                       [lv("+3 de dano", add=dict(Damage=3)),
                        lv("Os jabutis ricocheteiam", add=dict(Damage=1)),
                        lv("Recarga −12%", mult=dict(Cooldown=-0.12)),
                        lv("Os jabutis perseguem o inimigo mais próximo; +1 jabuti", add=dict(Count=1))],
                       ["jabuti", "drift", "volta"]),
         evo=weapon("carro_drift", "Carro Fazendo Drift", "Jabutis continuam; um carro faz drift ao seu redor atropelando e derrapando em área.", "jean", ["Software", "Mecanica"],
                    dict(Damage=15, Cooldown=1.0, Range=8.5, ProjectileSpeed=8, Count=3, Pierce=3, Area=1.1, Duration=1.2),
                    [lv("Carro mais rápido e mais forte", add=dict(Damage=3)),
                     lv("Avançada: volta acelerada fecha uma zona de impacto", add=dict(Damage=3))],
                    ["jabuti", "drift", "volta"], evolved=True)),
    dict(id="fufu", name="Fufu", eng=COMP, special="overclock", tags=["Biomecanica", "Quimica"], comp="pote_whey",
         weapon=weapon("socos_fortes", "Braços Fortes", "Socos em arco largo que empurram; acertos seguidos dão fúria e o 5º soco solta uma onda.", "fufu", ["Biomecanica", "Quimica"],
                       dict(Damage=14, Cooldown=0.7, Range=2.0, Knockback=3, Area=1, Duration=1),
                       [lv("+4 de dano", add=dict(Damage=4)),
                        lv("Arco maior e mais empurrão", add=dict(Knockback=0.5)),
                        lv("A fúria acumula até 8", add=dict(Damage=2)),
                        lv("Golpe carregado maior e atordoa", add=dict(Damage=3))],
                       ["soco_fufu", "impacto_fufu", "insulina", "frasco"]),
         evo=weapon("socos_insulina", "Socos e Insulina", "Os socos continuam; seringas de insulina perfuram e frascos estouram em área.", "fufu", ["Biomecanica", "Quimica"],
                    dict(Damage=24, Cooldown=0.6, Range=2.3, Knockback=4, Area=1.15, Duration=1),
                    [lv("Frascos estouram em área maior", add=dict(Area=0.2)),
                     lv("Avançada: na fúria máxima cada soco dispara seringas", add=dict(Damage=4))],
                    ["soco_fufu", "impacto_fufu", "insulina", "frasco"], evolved=True)),
    dict(id="donasa", name="Donasa", eng=COMP, special="overclock", tags=["Software", "Sensores"], comp="megafone",
         weapon=weapon("fala_alta", "Fala Extremamente Alta", "Ondas sonoras ao redor empurram e interrompem; ondas direcionais atravessam a fila; um grito periódico.", "donasa", ["Software", "Sensores"],
                       dict(Damage=9, Cooldown=1.2, Range=7, ProjectileSpeed=10, Knockback=1.5, Area=1, Duration=1),
                       [lv("+3 de dano", add=dict(Damage=3)),
                        lv("Duas ondas direcionais por vez", add=dict(Damage=1)),
                        lv("Recarga −12%", mult=dict(Cooldown=-0.12)),
                        lv("Grito a cada 4,5 s e maior", add=dict(Area=0.15))],
                       ["fala", "grito", "onda_sonora", "garrafa", "grito_gigante"]),
         evo=weapon("fala_amplificada", "Fala Amplificada e Garrafas Verdes", "Ondas maiores atravessam grupos; garrafas verdes quebram em área.", "donasa", ["Software", "Sensores"],
                    dict(Damage=15, Cooldown=1.05, Range=8, ProjectileSpeed=11, Knockback=2, Area=1.15, Duration=1),
                    [lv("+1 garrafa por arremesso", add=dict(Damage=3)),
                     lv("Avançada: grito gigante com chuva de garrafas", add=dict(Damage=3))],
                    ["fala", "grito", "onda_sonora", "garrafa", "grito_gigante"], evolved=True)),
]

COMPONENTS = [
    component("cartao_corporativo", "Cartão Corporativo", "Coleta mais longe e as armas batem um pouco mais forte.", ["Software", "Quimica"],
              [("PickupRadius", "mult", "player", 0.1), ("Damage", "mult", "all", 0.03)]),
    component("cafe_expresso", "Café Expresso", "Projéteis mais rápidos e recarga um pouco menor.", ["Software", "Sensores"],
              [("ProjectileSpeed", "mult", "all", 0.06), ("Cooldown", "mult", "all", -0.03)]),
    component("halter", "Halter", "Mais empurrão em todas as armas e mais vida.", ["Biomecanica", "Mecanica"],
              [("Knockback", "mult", "all", 0.1), ("MaxHp", "add", "player", 6)]),
    component("marmita", "Marmita", "Recupera vida aos poucos e aumenta a vida máxima.", ["Suporte", "Biomecanica"],
              [("Regen", "add", "player", 0.2), ("MaxHp", "add", "player", 5)]),
    component("pote_whey", "Pote de Whey", "Mais dano nas armas de Biomecânica e mais vida.", ["Biomecanica", "Quimica"],
              [("Damage", "mult", "tag:Biomecanica", 0.05), ("MaxHp", "add", "player", 4)]),
    component("kit_arduino", "Kit Arduino", "Robôs e automação atacam mais rápido e mais forte.", ["Robotica", "Automacao"],
              [("Cooldown", "mult", "tag:Robotica", -0.05), ("Damage", "mult", "tag:Robotica", 0.03)]),
    component("cabo_hdmi", "Cabo HDMI", "Mais alcance e um pouco mais de área.", ["Eletronica", "Software"],
              [("Range", "mult", "all", 0.06), ("Area", "mult", "all", 0.03)]),
    component("megafone", "Megafone", "Áreas de efeito maiores.", ["Sensores", "Software"],
              [("Area", "mult", "all", 0.06)]),
]

OVERCLOCK = {
    "id": "overclock", "name": "Overclock", "kind": "overclock",
    "description": "Por 6 s: armas recarregam 40% mais rápido e você corre 15% mais.",
    "cooldown": 20, "radius": 0, "expandSeconds": 0, "damage": 0, "stunSeconds": 0, "stunTag": "",
    "duration": 6, "moveBonus": 0.15, "damageBonus": 0, "damageTakenMult": 1, "ramDamage": 0, "ramKnockback": 0,
    "cooldownBonus": -0.4, "boostTag": "",
}


def upsert(lst, item):
    for i, x in enumerate(lst):
        if x["id"] == item["id"]:
            lst[i] = item
            return
    lst.append(item)


def dump(v, ind=0):
    """JSON legível: objetos pequenos numa linha só, como o arquivo escrito à mão."""
    flat = json.dumps(v, ensure_ascii=False, separators=(', ', ': '))
    if len(flat) + ind <= 150 or not isinstance(v, (dict, list)):
        return flat
    pad, inner = ' ' * ind, ' ' * (ind + 2)
    if isinstance(v, list):
        return '[\n' + ',\n'.join(inner + dump(x, ind + 2) for x in v) + '\n' + pad + ']'
    return '{\n' + ',\n'.join(f'{inner}{json.dumps(k, ensure_ascii=False)}: {dump(x, ind + 2)}' for k, x in v.items()) + '\n' + pad + '}'


def main():
    with open(PATH, encoding='utf-8') as f:
        data = json.load(f)
    for d in data["balance"]["difficulties"]:
        d.setdefault("abilityDamage", 1.0)
    for t in data["balance"]["teams"]:
        t.setdefault("abilityDamage", 1.0)
    upsert(data["specials"], OVERCLOCK)
    for c in COMPONENTS:
        upsert(data["abilities"], c)
    heroes = []
    names = {a["id"]: a["name"] for a in data["abilities"]}
    for c in CHARS:
        upsert(data["abilities"], c["weapon"])
        upsert(data["abilities"], c["evo"])
        heroes.append({
            "id": c["id"], "name": c["name"], "engineering": c["eng"], "hidden": False,
            "startingAbility": c["weapon"]["id"], "special": c["special"], "portrait": c["id"],
            "affinityTags": c["tags"],
            "advancedAffinity": [{"stat": "Damage", "op": "mult", "scope": "self", "value": 0.05}],
            "advancedAffinityText": "+5% de dano numa arma afim ao levá-la ao nível máximo.",
        })
        upsert(data["evolutions"], {
            "id": "evo_" + c["evo"]["id"], "name": c["evo"]["name"],
            "description": f'{c["weapon"]["name"]} no nível máximo + {names[c["comp"]]} nível 2.',
            "fromAbility": c["weapon"]["id"], "resultAbility": c["evo"]["id"],
            "requires": [{"kind": "ability_level", "target": c["weapon"]["id"], "value": 5},
                         {"kind": "ability_level", "target": c["comp"], "value": 2}],
            "discoverable": False,
        })
    ids = {h["id"] for h in heroes}
    legacy = [s for s in data["specializations"] if s["id"] not in ids]
    for s in legacy:
        s["hidden"] = True
        s.setdefault("engineering", s["name"])
    data["specializations"] = heroes + legacy
    for a in data["abilities"]:
        a.setdefault("exclusive", False)
        a.setdefault("sfx", [])
    for s in data["specials"]:
        s.setdefault("cooldownBonus", 0)
        s.setdefault("boostTag", "")
    with open(PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write(dump(data) + '\n')


if __name__ == '__main__':
    main()
