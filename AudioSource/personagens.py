# Acrescenta (ou atualiza) em sfx.json os sons das armas dos personagens. Uso: python3 personagens.py
import json, os

PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sfx.json')

def L(wave, freq, **kw): return {"wave": wave, "freq": freq, **kw}

SFX = {
    # Staut
    "bolha": (0.18, [L("sine", 500, freqEnd=1400, attack=0.002, decay=0.12, gain=0.7), L("sine", 1900, freqEnd=900, start=0.08, decay=0.06, gain=0.3)]),
    "bolha_grande": (0.35, [L("sine", 260, freqEnd=700, attack=0.01, decay=0.3, vibrato=0.08, vibratoRate=14, gain=0.8)]),
    "maleta": (0.3, [L("noise", 7000, attack=0.001, decay=0.06, hp=2500, gain=0.5), L("square", 180, freqEnd=90, duty=0.4, decay=0.22, lp=1200, gain=0.6)]),
    "moedas": (0.45, [L("square", 1975, duty=0.25, decay=0.08, gain=0.35), L("square", 2637, duty=0.25, start=0.07, length=0.12, decay=0.1, gain=0.35),
                      L("square", 2349, duty=0.25, start=0.15, length=0.12, decay=0.1, gain=0.3), L("square", 3136, duty=0.25, start=0.24, length=0.2, decay=0.18, gain=0.3)]),
    # Miyuki
    "numero": (0.07, [L("square", 2200, freqEnd=2600, duty=0.2, attack=0.001, decay=0.05, gain=0.35)]),
    "expressao": (0.22, [L("square", 900, freqEnd=2400, duty=0.3, decay=0.2, gain=0.45), L("triangle", 1800, start=0.05, decay=0.12, gain=0.3)]),
    "equacao": (0.5, [L("sine", 880, freqEnd=220, decay=0.15, gain=0.5), L("noise", 4000, start=0.12, decay=0.3, lp=2500, gain=0.7), L("sine", 110, freqEnd=50, start=0.12, decay=0.3, gain=0.8)]),
    "sushi": (0.2, [L("noise", 6000, attack=0.02, decay=0.15, lp=4000, lpEnd=800, gain=0.6), L("sine", 600, freqEnd=300, decay=0.12, gain=0.3)]),
    "esteira": (0.6, [L("triangle", 523.25, decay=0.1, gain=0.4), L("triangle", 659.25, start=0.1, length=0.12, decay=0.1, gain=0.4),
                      L("triangle", 783.99, start=0.2, length=0.12, decay=0.1, gain=0.4), L("triangle", 1046.5, start=0.3, length=0.3, decay=0.25, gain=0.4)]),
    # Giulia
    "resistor": (0.12, [L("saw", 2600, freqEnd=1400, attack=0.001, decay=0.1, lp=6000, gain=0.45)]),
    "transistor": (0.2, [L("square", 1600, freqEnd=400, duty=0.25, decay=0.16, drive=2, gain=0.5), L("noise", 7000, decay=0.06, hp=3000, gain=0.4)]),
    "fonte": (0.45, [L("sine", 60, attack=0.01, decay=0.4, gain=0.9), L("square", 120, duty=0.5, decay=0.35, lp=800, gain=0.4), L("noise", 5000, start=0.05, decay=0.2, hp=2000, gain=0.5)]),
    "osciloscopio": (0.4, [L("sine", 440, freqEnd=1760, vibrato=0.2, vibratoRate=20, attack=0.01, decay=0.35, gain=0.55)]),
    "circuito": (0.5, [L("square", 2000, freqEnd=300, duty=0.15, decay=0.45, drive=3, gain=0.5), L("noise", 3000, crush=True, decay=0.4, hp=1500, gain=0.5)]),
    "tempestade": (0.9, [L("noise", 2000, attack=0.05, decay=0.8, lp=1500, gain=0.9), L("sine", 70, freqEnd=40, attack=0.05, decay=0.8, gain=0.8)]),
    # Breno
    "doritos": (0.12, [L("noise", 9000, crush=True, attack=0.001, decay=0.09, hp=3000, gain=0.6)]),
    "hamburguer": (0.3, [L("sine", 200, freqEnd=70, attack=0.002, decay=0.25, gain=0.9), L("noise", 3000, decay=0.12, lp=1500, gain=0.5)]),
    "pneu": (0.6, [L("sine", 90, freqEnd=60, attack=0.02, decay=0.55, vibrato=0.1, vibratoRate=9, gain=0.9), L("noise", 800, decay=0.5, lp=600, gain=0.5)]),
    # Artur e Fufu
    "soco": (0.15, [L("noise", 3000, attack=0.001, decay=0.06, lp=2000, gain=0.7), L("sine", 160, freqEnd=60, decay=0.12, gain=0.8)]),
    "soco_forte": (0.4, [L("sine", 120, freqEnd=40, attack=0.001, decay=0.35, drive=2, gain=1.0), L("noise", 2000, decay=0.2, lp=1200, gain=0.7)]),
    "pilulas": (0.18, [L("square", 1400, duty=0.3, decay=0.04, gain=0.35), L("square", 1700, duty=0.3, start=0.05, length=0.05, decay=0.04, gain=0.35), L("square", 1500, duty=0.3, start=0.1, length=0.06, decay=0.05, gain=0.35)]),
    "seringa": (0.2, [L("saw", 3000, freqEnd=5000, attack=0.001, decay=0.15, hp=1500, gain=0.4)]),
    "soco_fufu": (0.18, [L("noise", 2500, attack=0.001, decay=0.08, lp=1500, gain=0.8), L("sine", 130, freqEnd=50, decay=0.15, gain=0.9)]),
    "impacto_fufu": (0.55, [L("sine", 90, freqEnd=30, attack=0.001, decay=0.5, drive=3, gain=1.0), L("noise", 1500, decay=0.3, lp=900, gain=0.8)]),
    "insulina": (0.15, [L("saw", 2400, freqEnd=4200, decay=0.12, hp=1200, gain=0.4)]),
    "frasco": (0.35, [L("noise", 9000, attack=0.001, decay=0.25, hp=4000, gain=0.6), L("square", 3500, duty=0.2, decay=0.1, gain=0.25)]),
    # Badotti
    "robo_tiro": (0.15, [L("square", 1200, freqEnd=600, duty=0.5, decay=0.12, crush=True, gain=0.4)]),
    "robo_soco": (0.15, [L("square", 220, freqEnd=110, duty=0.3, decay=0.1, gain=0.5), L("noise", 6000, decay=0.05, hp=3000, gain=0.4)]),
    "robo_dash": (0.3, [L("saw", 300, freqEnd=1200, attack=0.005, decay=0.25, lp=3000, gain=0.5)]),
    "robo_combo": (0.45, [L("square", 523.25, duty=0.25, decay=0.08, gain=0.4), L("square", 783.99, duty=0.25, start=0.08, length=0.1, decay=0.08, gain=0.4), L("sine", 100, freqEnd=40, start=0.15, decay=0.3, gain=0.9)]),
    "master_soco": (0.35, [L("square", 110, freqEnd=55, duty=0.4, decay=0.3, drive=2, gain=0.7), L("noise", 3000, decay=0.15, lp=2000, gain=0.6)]),
    "master_chao": (0.8, [L("sine", 70, freqEnd=28, attack=0.002, decay=0.75, drive=3, gain=1.0), L("noise", 1200, decay=0.6, lp=700, gain=0.8)]),
    "master_salto": (0.8, [L("saw", 150, freqEnd=600, attack=0.01, decay=0.3, gain=0.5), L("sine", 80, freqEnd=30, start=0.35, decay=0.4, gain=1.0)]),
    "montagem": (1.2, [L("square", 220, freqEnd=880, duty=0.3, attack=0.02, decay=0.6, crush=True, gain=0.35),
                       L("noise", 5000, start=0.2, length=0.5, decay=0.4, hp=2000, gain=0.3),
                       L("square", 523.25, duty=0.25, start=0.7, length=0.5, decay=0.45, gain=0.35), L("square", 783.99, duty=0.25, start=0.8, length=0.4, decay=0.35, gain=0.3)]),
    # Garcia
    "celular": (0.25, [L("square", 1318.5, duty=0.5, decay=0.06, gain=0.35), L("square", 1760, duty=0.5, start=0.08, length=0.08, decay=0.06, gain=0.35), L("noise", 4000, start=0.16, decay=0.08, lp=3000, gain=0.4)]),
    "papel": (0.15, [L("noise", 8000, attack=0.01, decay=0.12, hp=4000, gain=0.45)]),
    "computador": (0.4, [L("square", 160, freqEnd=80, duty=0.5, decay=0.3, lp=1500, gain=0.6), L("noise", 6000, decay=0.25, hp=2000, gain=0.5)]),
    "tv": (0.7, [L("noise", 5000, crush=True, attack=0.01, decay=0.4, gain=0.4), L("sine", 90, freqEnd=35, start=0.3, decay=0.4, drive=2, gain=1.0)]),
    "chuva": (1.0, [L("noise", 3000, attack=0.1, decay=0.9, lp=2500, gain=0.6), L("square", 900, freqEnd=200, duty=0.3, decay=0.8, crush=True, gain=0.25)]),
    # Jean
    "jabuti": (0.2, [L("sine", 300, freqEnd=180, attack=0.005, decay=0.15, gain=0.6), L("noise", 1500, decay=0.05, lp=1200, gain=0.3)]),
    "drift": (0.7, [L("noise", 2500, attack=0.05, decay=0.6, hp=1500, vibrato=0.05, vibratoRate=30, gain=0.6), L("saw", 140, freqEnd=180, decay=0.6, lp=900, gain=0.4)]),
    "volta": (0.8, [L("saw", 110, freqEnd=440, attack=0.02, decay=0.7, lp=2000, gain=0.6), L("noise", 3000, decay=0.6, hp=2000, gain=0.4)]),
    # Donasa
    "fala": (0.3, [L("saw", 220, freqEnd=180, attack=0.01, decay=0.25, lp=1400, vibrato=0.04, vibratoRate=7, gain=0.7), L("saw", 660, freqEnd=540, decay=0.25, lp=2000, gain=0.25)]),
    "grito": (0.6, [L("saw", 260, freqEnd=420, attack=0.02, decay=0.55, lp=2500, drive=2, vibrato=0.06, vibratoRate=8, gain=0.8), L("noise", 2000, decay=0.4, lp=2500, gain=0.3)]),
    "onda_sonora": (0.35, [L("sine", 180, freqEnd=90, attack=0.005, decay=0.3, vibrato=0.15, vibratoRate=25, gain=0.8)]),
    "garrafa": (0.4, [L("noise", 10000, attack=0.001, decay=0.3, hp=5000, gain=0.6), L("triangle", 2400, freqEnd=1900, decay=0.25, gain=0.35)]),
    "grito_gigante": (1.1, [L("saw", 200, freqEnd=520, attack=0.03, decay=1.0, lp=3000, drive=3, vibrato=0.08, vibratoRate=7, gain=0.9), L("sine", 60, decay=0.9, gain=0.7), L("noise", 1500, decay=0.8, lp=2000, gain=0.4)]),
    # Overclock
    "overclock": (0.7, [L("square", 220, freqEnd=1760, duty=0.25, attack=0.01, decay=0.6, gain=0.4), L("sine", 3520, start=0.4, decay=0.25, gain=0.25)]),
}

def main():
    defs = json.load(open(PATH, encoding='utf-8'))
    for k, (dur, layers) in SFX.items():
        item = {"id": k, "duration": dur, "layers": layers}
        for i, d in enumerate(defs):
            if d["id"] == k: defs[i] = item; break
        else: defs.append(item)
    with open(PATH, 'w', encoding='utf-8', newline='\n') as f:
        f.write('[\n' + ',\n'.join('  { "id": %s, "duration": %s, "layers": [\n%s ] }' % (json.dumps(d["id"]), json.dumps(d["duration"]),
                ',\n'.join('    ' + json.dumps(l, ensure_ascii=False) for l in d["layers"])) for d in defs) + '\n]\n')

if __name__ == '__main__': main()
