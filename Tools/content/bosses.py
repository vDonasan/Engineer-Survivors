# Gera os dois bosses (Guirotto e Mago da Elétrica), as regras de fim de partida e do Modo Infinito.
# Uso: python3 Tools/content/bosses.py  (idempotente: substitui pelos ids)
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from personagens import PATH, dump  # noqa: E402


def par(**kw):
    return [{"key": k, "value": float(v)} for k, v in kw.items()]


def enemy(id, name, hp, speed, contact, radius, xp):
    return {
        "id": id, "name": name, "role": "boss", "behavior": "boss", "tags": ["Maquina"],
        "hp": hp, "speed": speed, "contactDamage": contact, "contactInterval": 0.8, "radius": radius, "xp": xp,
        "cost": 0, "knockbackResist": 1, "keepMin": 0, "keepMax": 0, "fireInterval": 0, "telegraph": 0,
        "projectileSpeed": 0, "projectileDamage": 0,
    }


ENEMIES = [
    enemy("boss_guirotto", "Guirotto", 2600, 2.4, 14, 1.0, 120),
    enemy("boss_mago", "Mago da Elétrica", 7200, 2.2, 16, 0.9, 300),
]

BOSSES = [
    {
        "id": "guirotto", "enemyId": "boss_guirotto", "name": "GUIROTTO", "title": "O Sapo do Banjo",
        "spawnSeconds": 240, "final": False, "bravoThreshold": 0.3, "entrySeconds": 2.0, "rageSeconds": 1.6,
        "teamHp": 0.55, "teamCooldown": 0.08,
        "parameters": par(
            # Acorde do Brejo: ondas sonoras que atravessam
            waveCount=3, waveCountBravo=5, waveSpread=14, waveSpeed=6.5, waveSpeedBravo=8.5, waveDamage=14, waveRadius=0.5,
            # Ressonância do Banjo: área circular sinalizada
            resonanceRadius=3.6, resonanceRadiusBravo=4.6, resonanceDelay=1.2, resonanceDelayBravo=0.9, resonanceDamage=26,
            # Chuva de Notas: leque com curvas e ricochete
            noteCount=7, noteCountBravo=11, noteArc=110, noteSpeed=5.2, noteTurn=0.7, noteDamage=10,
            # Concerto do Brejo: espiral + áreas nos jogadores
            concertSeconds=2.4, concertEvery=0.3, concertEveryBravo=0.22, concertRing=8, concertRingBravo=12,
            concertMarks=1, concertMarkRadius=1.8, concertMarkDelay=1.1,
            # Refrão Furioso (só BRAVO): anel de notas + ressonância + ondas
            chorusRing=16,
            # ritmo
            cooldown=2.2, cooldownBravo=1.45, windup=0.55, windupBravo=0.4, recover=0.8, recoverBravo=0.5,
            keepMin=2.5, keepMax=5.5,
        ),
    },
    {
        "id": "mago", "enemyId": "boss_mago", "name": "MAGO DA ELÉTRICA", "title": "Chefe final",
        "spawnSeconds": 540, "final": True, "bravoThreshold": 0.3, "entrySeconds": 2.4, "rageSeconds": 1.8,
        "teamHp": 0.55, "teamCooldown": 0.08,
        "parameters": par(
            # Raio Direcionado: linha mirada
            boltAim=0.7, boltAimBravo=0.45, boltLength=16, boltWidth=0.9, boltDamage=22, boltCountBravo=3, boltFan=22,
            # Cadeia de Relâmpagos
            chainWindup=0.6, chainJumps=3, chainJumpsBravo=5, chainRange=6.5, chainDamage=18, chainFalloff=0.75,
            # Tempestade Elétrica
            stormMarks=4, stormMarksBravo=7, stormRadius=1.7, stormDelay=1.25, stormDelayBravo=0.95, stormDamage=24, stormSpread=4,
            # Orbes de Energia
            orbCount=6, orbCountBravo=9, orbSpeed=3.2, orbTurn=0.9, orbDamage=12, orbHoming=2, orbHomingBravo=4, orbHomingSeconds=2.2,
            # Sobrecarga Arcana
            overloadCharge=2.0, overloadRadius=6.5, overloadRadiusBravo=7.5, overloadDamage=40, overloadRecover=1.6,
            overloadRingInner=7.5, overloadRingOuter=11.5, overloadRingDelay=1.0,
            # ritmo e posicionamento
            cooldown=2.0, cooldownBravo=1.3, windup=0.5, windupBravo=0.35, recover=0.7, recoverBravo=0.45,
            keepMin=5, keepMax=9, teleportBelow=3,
        ),
    },
]

RULES = {
    # vitória: só pela morte do boss final (o relógio pode passar de 10:00)
    "bossVictoryDelay": 3.0,
    # Modo Infinito
    "infiniteFirstBoss": 60, "infiniteBossEvery": 150,
    "infiniteHpPerMinute": 0.10, "infiniteHpMax": 10,
    "infiniteDamagePerMinute": 0.05, "infiniteDamageMax": 3.5,
    "infiniteSpawnPerMinute": 0.07, "infiniteSpawnMax": 3,
    "infiniteElitePerMinute": 0.008, "infiniteEliteMax": 0.35,
    "infiniteStormEvery": 40, "infiniteStormMarks": 4, "infiniteStormMarksMax": 12,
    "infiniteBossHpPerCycle": 0.6,
}


def main():
    with open(PATH, encoding='utf-8') as f:
        d = json.load(f)
    ids = {e["id"] for e in ENEMIES}
    d["enemies"] = [e for e in d["enemies"] if e["id"] not in ids] + ENEMIES
    d["bosses"] = BOSSES
    d["rules"].update(RULES)
    with open(PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write(dump(d))
    print("bosses ok:", [b["id"] for b in BOSSES])


if __name__ == "__main__":
    main()
