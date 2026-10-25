# Gera os nove itens (e suas evoluções) no conteúdo canônico. Uso: python3 Tools/content/itens.py
# Idempotente: substitui pelos ids. Rode depois de personagens.py.
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from personagens import PATH, dump, upsert, st, lv, par  # noqa: E402


def grant(stat, op, scope, levels):
    return {"stat": stat, "op": op, "scope": scope, "value": levels[0], "perLevel": levels}


def item(id, name, desc, rarity, behavior, tags, max_level, levels=None, base=None, grants=None, params=None, sfx=None, evolved=False):
    return {
        "id": id, "name": name, "description": desc, "kind": "component", "rarity": rarity, "behavior": behavior,
        "tags": tags, "maxLevel": max_level, "evolutionOnly": evolved, "exclusive": False, "sfx": sfx or [],
        "baseStats": st(**(base or {})), "levels": levels or [], "grants": grants or [],
        "parameters": par(**({"evolved": 1} if evolved else {}), **(params or {})),
    }


ITEMS = [
    # 1. Protoboard
    item("protoboard", "Protoboard", "Projéteis tecnológicos mais rápidos e armas eletrônicas/de computação atacando mais rápido.",
         "incomum", "item_protoboard", ["Eletronica"], 4,
         levels=[lv("+5% de velocidade de ataque tecnológico"),
                 lv("Acertos de armas elétricas: 10% de chance de soltar uma descarga"),
                 lv("A descarga salta para até 2 inimigos próximos (−30% de dano por salto)")],
         base=dict(Damage=8, Area=1, Duration=1),
         grants=[grant("ProjectileSpeed", "mult", "tag:Eletronica", [0.10, 0.10, 0.10, 0.10]),
                 grant("ProjectileSpeed", "mult", "tag:Software", [0.10, 0.10, 0.10, 0.10]),
                 grant("Cooldown", "mult", "tag:Eletronica", [-0.08, -0.13, -0.13, -0.13]),
                 grant("Cooldown", "mult", "tag:Software", [-0.08, -0.13, -0.13, -0.13])],
         params=dict(chance=0.10, range=3, internalCooldown=0.12), sfx=["arco"]),
    item("protoboard_experimental", "Protoboard Experimental", "Circuitos temporários: 30% dos acertos elétricos soltam uma corrente que salta em até 4 inimigos.",
         "reliquia", "item_protoboard", ["Eletronica"], 1,
         base=dict(Damage=12, Area=1, Duration=1),
         grants=[grant("ProjectileSpeed", "mult", "tag:Eletronica", [0.12]), grant("ProjectileSpeed", "mult", "tag:Software", [0.12]),
                 grant("Cooldown", "mult", "tag:Eletronica", [-0.15]), grant("Cooldown", "mult", "tag:Software", [-0.15])],
         params=dict(evoChance=0.30, evoJumps=4, evoRange=3.5, internalCooldown=0.1), sfx=["arco"], evolved=True),
    # 2. Cenoura Podre
    item("cenoura_podre", "Cenoura Podre", "Arremessa uma cenoura podre que deixa uma nuvem de mau cheiro: dano contínuo e leve lentidão.",
         "comum", "item_cenoura", ["Quimica", "Organico"], 5,
         levels=[lv("Arremessos mais frequentes", mult=dict(Cooldown=-0.2)),
                 lv("A nuvem dura mais", add=dict(Duration=0.5)),
                 lv("15% de chance de atordoar o alvo por 0,6 s", add=dict(Damage=2)),
                 lv("O fedor se espalha uma vez para inimigos vizinhos (−30% de dano)", add=dict(Area=0.2))],
         base=dict(Damage=10, Cooldown=2.8, Range=7, ProjectileSpeed=9, Area=1, Duration=1),
         params=dict(stunChance=0.15, stunSeconds=0.6), sfx=["frasco"]),
    item("hortalica_desgraca", "Hortaliça da Desgraça", "Nuvens maiores e mais longas; o fedor se espalha entre inimigos por até 3 gerações (máx. 60 infectados).",
         "reliquia", "item_cenoura", ["Quimica", "Organico"], 1,
         base=dict(Damage=16, Cooldown=2.2, Range=8, ProjectileSpeed=9, Area=1.2, Duration=1.5),
         params=dict(stunChance=0.2, stunSeconds=0.6), sfx=["frasco"], evolved=True),
    # 3. Iogurte de 80 Calorias
    item("iogurte_80", "Iogurte de 80 Calorias", "Leve e prático: mais velocidade de movimento e mais experiência.",
         "comum", "item_iogurte", ["Mobilidade", "Organico"], 5,
         levels=[lv("Mais velocidade de movimento"), lv("Mais experiência obtida"), lv("Recarga de todas as armas −5%"),
                 lv("Ao subir de nível, recupera 5% da vida (no máximo a cada 3 s)")],
         grants=[grant("MoveSpeed", "mult", "player", [0.08, 0.11, 0.11, 0.11, 0.11]),
                 grant("XpGain", "add", "player", [0.05, 0.05, 0.10, 0.10, 0.10]),
                 grant("Cooldown", "mult", "all", [0, 0, 0, -0.05, -0.05])],
         params=dict(levelHeal=0.05, levelHealCooldown=3)),
    item("dieta_milagrosa", "Dieta Milagrosa", "Ao coletar experiência: +25% de velocidade por 1,5 s (recarga de 4 s).",
         "reliquia", "item_iogurte", ["Mobilidade", "Organico"], 1,
         grants=[grant("MoveSpeed", "mult", "player", [0.12]), grant("XpGain", "add", "player", [0.12]), grant("Cooldown", "mult", "all", [-0.06])],
         params=dict(levelHeal=0.05, levelHealCooldown=3, boost=0.25, boostSeconds=1.5, boostCooldown=4), evolved=True),
    # 4. Motor do Astra
    item("motor_astra", "Motor do Astra", "Ronca e treme: solta periodicamente uma onda de impacto que fere e empurra os inimigos ao redor.",
         "raro", "item_motor", ["Mecanica", "Area"], 5,
         levels=[lv("Onda maior", add=dict(Area=0.25)), lv("Ondas mais frequentes", mult=dict(Cooldown=-0.25)),
                 lv("A onda lança 5 peças metálicas", add=dict(Damage=3)), lv("A cada 3ª onda, sobrecarga: segunda onda logo depois", add=dict(Damage=3))],
         base=dict(Damage=14, Cooldown=4, Area=1, Knockback=2.5, Duration=1),
         sfx=["master_chao"]),
    item("astra_alta_rotacao", "Astra em Alta Rotação", "Ondas maiores a cada 2,5 s. Com o carro do Jean: drift 50% mais rápido deixando rastro de dano.",
         "reliquia", "item_motor", ["Mecanica", "Area"], 1,
         base=dict(Damage=20, Cooldown=2.5, Area=1.2, Knockback=3, Duration=1),
         params=dict(evoInterval=2.5), sfx=["master_chao"], evolved=True),
    # 5. Cachorro Quentamento de Motor
    item("cachorro_quente", "Cachorro Quentamento de Motor", "Um cachorro mecânico superaquecido persegue inimigos, morde e dá investidas que deixam rastro de calor.",
         "incomum", "item_cachorro", ["Mecanica", "Termico"], 5,
         levels=[lv("O cachorro corre mais rápido"), lv("Investidas mais fortes", add=dict(Damage=4)),
                 lv("Rastro de calor dura mais", add=dict(Duration=0.6)), lv("A cada 4ª investida, corrida turbo que atravessa grupos", add=dict(Damage=2))],
         base=dict(Damage=9, Cooldown=3, Range=6, Area=1, Duration=1),
         sfx=["robo_soco", "robo_dash"]),
    item("cao_turbo", "Cão Turbo", "Peças de carro e escapamento: investida turbo a cada 4 s que termina numa explosão de calor.",
         "reliquia", "item_cachorro", ["Mecanica", "Termico"], 1,
         base=dict(Damage=16, Cooldown=4, Range=7, Area=1.1, Duration=1.4),
         params=dict(turboEvery=4), sfx=["robo_soco", "robo_dash"], evolved=True),
    # 6. Camiseta ao Contrário
    item("camiseta_contrario", "Camiseta ao Contrário", "Ninguém entende de que lado você está: chance de esquivar de ataques.",
         "incomum", "item_camiseta", ["Defesa", "Mobilidade"], 5,
         levels=[lv("Mais esquiva"), lv("Ao esquivar, +20% de velocidade por 1,5 s"),
                 lv("A esquiva deixa uma imagem residual que confunde (−60% de velocidade) quem está perto"),
                 lv("Primeira esquiva após 6 s: onda que interrompe inimigos comuns por 0,6 s")],
         base=dict(Damage=12, Area=1),
         grants=[grant("Dodge", "add", "player", [0.08, 0.11, 0.11, 0.11, 0.11])],
         params=dict(dodgeSpeed=0.2, waveCooldown=6)),
    item("moda_incompreensivel", "Moda Incompreensível", "Toda esquiva solta uma onda que fere e empurra os inimigos próximos.",
         "reliquia", "item_camiseta", ["Defesa", "Mobilidade"], 1,
         base=dict(Damage=16, Area=1.1),
         grants=[grant("Dodge", "add", "player", [0.14])], params=dict(dodgeSpeed=0.25, waveCooldown=5), evolved=True),
    # 7. Mamão "Só Quer Mamão"
    item("mamao", "Mamão \"Só Quer Mamão\"", "Um mamão simpático orbita você e solta pulsos de cura (aliados próximos recebem metade).",
         "incomum", "item_mamao", ["Suporte", "Cura"], 5,
         levels=[lv("Pulsos mais frequentes", mult=dict(Cooldown=-0.25)), lv("Pulso maior", add=dict(Area=0.3)),
                 lv("Cura com a vida cheia vira escudo (até 15% da vida máxima)"), lv("A cada 3º pulso, explosão de polpa que empurra e fere")],
         base=dict(Damage=10, Cooldown=6, Area=1),
         params=dict(heal=0.04, shieldCap=0.15), sfx=["gema"]),
    item("mamao_supremo", "Só Quer Mamão Supremo", "Campo de suporte amplo: cura a cada 3 s e excedente vira escudo (até 25% da vida máxima).",
         "reliquia", "item_mamao", ["Suporte", "Cura"], 1,
         base=dict(Damage=14, Cooldown=3, Area=1.15),
         params=dict(evoHeal=0.05, evoShieldCap=0.25, evoInterval=3), sfx=["gema"], evolved=True),
    # 8. Tequila
    item("tequila", "Tequila", "Euforia periódica: armas atacam mais rápido por alguns segundos; depois, projéteis um pouco mais lentos por 2 s.",
         "raro", "item_tequila", ["Quimica"], 5,
         levels=[lv("A euforia dura mais", add=dict(Duration=1.5)), lv("Penalidade depois da euforia cai pela metade"),
                 lv("Na euforia, +10% de velocidade de movimento"), lv("Ao fim da euforia, pulso de dano ao redor", add=dict(Damage=6))],
         base=dict(Damage=18, Cooldown=12, Duration=4, Area=1),
         params=dict(attackSpeed=0.10, penalty=0.15, penaltyReduced=0.07, penaltySeconds=2, moveBonus=0.10), sfx=["garrafa", "grito"]),
    item("fiesta_caotica", "Fiesta Caótica", "Na euforia: +1 projétil/golpe e recarga −20%; no fim, onda de impacto grande e 1,5 s de recuperação.",
         "reliquia", "item_tequila", ["Quimica"], 1,
         base=dict(Damage=26, Cooldown=11, Duration=5.5, Area=1),
         params=dict(evoAttackSpeed=0.20, evoRecovery=1.5, moveBonus=0.10), sfx=["garrafa", "grito"], evolved=True),
    # 9. Chapéu de Japonês (kasa de palha, sem caricatura)
    item("chapeu_japones", "Chapéu de Japonês", "Chapéu de palha que ajuda na mira: projéteis mais rápidos e mais alcance para armas de projétil.",
         "incomum", "item_chapeu", ["Projetil", "Sensores"], 5,
         levels=[lv("Mais alcance"), lv("Projéteis ainda mais rápidos"), lv("20% dos tiros ganham +1 perfuração"),
                 lv("A cada 3 s, o próximo tiro ganha +2 perfuração e +25% de velocidade")],
         grants=[grant("ProjectileSpeed", "mult", "all", [0.10, 0.10, 0.15, 0.15, 0.15]),
                 grant("Range", "mult", "tag:Projetil", [0.05, 0.10, 0.10, 0.10, 0.10])],
         params=dict(pierceChance=0.2, focusEvery=3)),
    item("mestre_projeteis", "Mestre dos Projéteis", "25% dos tiros ganham +1 perfuração ou +1 ricochete (no máximo 3 ricochetes por tiro).",
         "reliquia", "item_chapeu", ["Projetil", "Sensores"], 1,
         grants=[grant("ProjectileSpeed", "mult", "all", [0.18]), grant("Range", "mult", "tag:Projetil", [0.12])],
         params=dict(pierceChance=0.2, focusEvery=3, evoChance=0.25), evolved=True),
]


def req(kind, target, value): return {"kind": kind, "target": target, "value": value}


EVOLUTIONS = [
    ("protoboard", "protoboard_experimental", [req("ability_level", "protoboard", 4), req("tag_count", "Eletronica", 2)],
     "Protoboard nível 4 + outra arma ou item eletrônico."),
    ("cenoura_podre", "hortalica_desgraca", [req("ability_level", "cenoura_podre", 5), req("tag_count", "Area", 1)],
     "Cenoura Podre nível 5 + uma habilidade de área."),
    ("iogurte_80", "dieta_milagrosa", [req("ability_level", "iogurte_80", 5), req("tag_count", "Mobilidade", 2)],
     "Iogurte nível 5 + outro item de mobilidade (Turbina ou Camiseta ao Contrário)."),
    ("motor_astra", "astra_alta_rotacao", [req("ability_level", "motor_astra", 5), req("tag_count", "Mecanica", 2)],
     "Motor do Astra nível 5 + outra peça mecânica. Com o carro do Jean, o drift acelera."),
    ("cachorro_quente", "cao_turbo", [req("ability_level", "cachorro_quente", 5), req("any_ability_level", "motor_astra|astra_alta_rotacao", 1)],
     "Cachorro Quentamento de Motor nível 5 + Motor do Astra."),
    ("camiseta_contrario", "moda_incompreensivel", [req("ability_level", "camiseta_contrario", 5), req("tag_count", "Mobilidade", 2)],
     "Camiseta ao Contrário nível 5 + outro item de mobilidade (Turbina ou Iogurte)."),
    ("mamao", "mamao_supremo", [req("ability_level", "mamao", 5), req("tag_count", "Cura", 2)],
     "Mamão nível 5 + um item de cura ou proteção (Marmita, Vida Máxima ou Recuperação)."),
    ("tequila", "fiesta_caotica", [req("ability_level", "tequila", 5), req("any_ability_level", "attr_vel_ataque|attr_dano", 2)],
     "Tequila nível 5 + Velocidade de Ataque ou Dano nível 2."),
    ("chapeu_japones", "mestre_projeteis", [req("ability_level", "chapeu_japones", 5), req("tag_count", "Projetil", 2)],
     "Chapéu de Japonês nível 5 + uma arma de projéteis."),
]

# Categorias de jogo nas habilidades existentes (o que conta para as evoluções dos itens).
EXTRA_TAGS = {
    "Projetil": ["laser_industrial", "bolhas", "bolhas_e_maletas", "calculos", "matematica_sushi", "componentes_eletricos",
                 "fontes_osciloscopios", "lanches", "pneus_crossfit", "pilulas_seringas", "cabazords", "cabazord_master",
                 "celulares_cola", "computadores_tvs", "jabutis", "carro_drift", "socos_insulina", "fala_amplificada"],
    "Area": ["campo_eletromagnetico", "fala_alta", "fala_amplificada", "bonesmashing", "pilulas_seringas", "socos_fortes",
             "socos_insulina", "computadores_tvs", "pneus_crossfit", "cabazord_master", "rotor_de_impacto"],
    "Mobilidade": ["turbina"],
    "Cura": ["marmita", "attr_vida", "attr_recuperacao"],
}


def main():
    with open(PATH, encoding='utf-8') as f:
        data = json.load(f)
    for it in ITEMS:
        upsert(data["abilities"], it)
    names = {a["id"]: a["name"] for a in data["abilities"]}
    for frm, to, reqs, text in EVOLUTIONS:
        upsert(data["evolutions"], {"id": "evo_" + to, "name": names[to], "description": text,
                                    "fromAbility": frm, "resultAbility": to, "requires": reqs, "discoverable": False})
    for tag, ids in EXTRA_TAGS.items():
        for a in data["abilities"]:
            if a["id"] in ids and tag not in a["tags"]:
                a["tags"].append(tag)
    for a in data["abilities"]:
        for g in a.get("grants", []):
            g.setdefault("perLevel", [])
        for g in a.get("advancedAffinity", []):
            g.setdefault("perLevel", [])
    for s in data["specializations"]:
        for g in s.get("advancedAffinity", []):
            g.setdefault("perLevel", [])
    with open(PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write(dump(data) + '\n')


if __name__ == '__main__':
    main()
