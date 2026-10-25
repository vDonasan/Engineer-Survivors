# Acrescenta (ou atualiza) em sfx.json os sons dos bosses. Uso: python3 bosses.py && npm run build
import json, os
from personagens import PATH, L

SFX = {
    # chegada: sirene grave + estrondo
    "boss_aviso": (1.2, [L("square", 440, freqEnd=330, duty=0.5, decay=0.25, lp=1800, gain=0.35), L("square", 440, freqEnd=330, duty=0.5, start=0.4, length=0.3, decay=0.25, lp=1800, gain=0.35),
                         L("sine", 55, freqEnd=35, start=0.7, decay=0.5, drive=2, gain=0.9)]),
    "boss_guirotto_entrada": (1.4, [L("triangle", 196, decay=0.15, gain=0.5), L("triangle", 246.94, start=0.15, length=0.15, decay=0.12, gain=0.5),
                                    L("triangle", 293.66, start=0.3, length=0.15, decay=0.12, gain=0.5), L("triangle", 392, start=0.45, length=0.5, decay=0.45, vibrato=0.04, vibratoRate=8, gain=0.55),
                                    L("sine", 90, freqEnd=60, start=0.9, decay=0.45, vibrato=0.15, vibratoRate=11, gain=0.8)]),
    "boss_mago_entrada": (1.6, [L("noise", 3000, crush=True, attack=0.2, decay=1.2, hp=1500, gain=0.5), L("saw", 110, freqEnd=880, attack=0.3, decay=0.9, lp=3000, gain=0.35),
                                L("sine", 60, freqEnd=30, start=1.0, decay=0.55, drive=3, gain=1.0), L("noise", 6000, start=1.0, decay=0.4, hp=2500, gain=0.7)]),
    # Guirotto
    "banjo": (0.3, [L("saw", 392, attack=0.001, decay=0.25, lp=2600, lpEnd=900, gain=0.45), L("saw", 587.33, attack=0.001, decay=0.2, lp=2600, lpEnd=900, gain=0.3)]),
    "onda_sonora": (0.45, [L("sine", 180, freqEnd=90, attack=0.01, decay=0.4, vibrato=0.25, vibratoRate=16, gain=0.8), L("saw", 360, freqEnd=180, decay=0.3, lp=1200, gain=0.3)]),
    "ressonancia": (0.8, [L("sine", 70, freqEnd=35, attack=0.002, decay=0.75, drive=2, gain=1.0), L("triangle", 220, freqEnd=110, decay=0.6, vibrato=0.3, vibratoRate=22, gain=0.5)]),
    "notas": (0.35, [L("square", 784, duty=0.25, decay=0.07, gain=0.3), L("square", 988, duty=0.25, start=0.08, length=0.08, decay=0.07, gain=0.3),
                     L("square", 1175, duty=0.25, start=0.16, length=0.15, decay=0.12, gain=0.3)]),
    "coaxar": (0.5, [L("square", 140, freqEnd=90, duty=0.35, attack=0.01, decay=0.2, vibrato=0.3, vibratoRate=30, lp=1500, gain=0.6),
                     L("square", 150, freqEnd=80, duty=0.35, start=0.22, length=0.25, decay=0.22, vibrato=0.3, vibratoRate=30, lp=1500, gain=0.6)]),
    # Mago
    "raio_mira": (0.6, [L("sine", 1200, freqEnd=2400, attack=0.05, decay=0.55, vibrato=0.05, vibratoRate=30, gain=0.25)]),
    "raio": (0.45, [L("noise", 4000, crush=True, attack=0.001, decay=0.4, hp=800, gain=0.8), L("square", 1800, freqEnd=200, duty=0.2, decay=0.3, drive=3, gain=0.4)]),
    "carga": (2.0, [L("saw", 80, freqEnd=640, attack=0.1, decay=1.8, lp=2000, vibrato=0.04, vibratoRate=12, gain=0.35), L("noise", 5000, attack=1.0, decay=0.9, hp=3000, gain=0.25)]),
    "sobrecarga": (1.1, [L("sine", 50, freqEnd=25, attack=0.001, decay=1.0, drive=4, gain=1.0), L("noise", 2500, crush=True, decay=0.9, lp=2500, gain=0.9)]),
    "teleporte": (0.35, [L("sine", 400, freqEnd=2400, attack=0.001, decay=0.15, gain=0.4), L("sine", 2400, freqEnd=400, start=0.15, decay=0.15, gain=0.4)]),
    # fúria, dano forte, morte e vitória
    "boss_furia": (1.0, [L("saw", 110, freqEnd=55, attack=0.02, decay=0.9, drive=4, lp=1600, gain=0.6), L("noise", 1500, attack=0.05, decay=0.8, lp=1200, gain=0.6),
                         L("square", 220, freqEnd=440, duty=0.4, start=0.3, decay=0.6, crush=True, gain=0.25)]),
    "boss_morte": (1.8, [L("square", 392, freqEnd=98, duty=0.4, attack=0.01, decay=1.2, lp=2000, gain=0.45), L("sine", 60, freqEnd=25, start=0.4, decay=1.3, drive=2, gain=1.0),
                         L("noise", 3000, start=0.4, decay=1.2, lp=1800, gain=0.6)]),
    "modo_infinito": (1.0, [L("square", 261.63, duty=0.25, decay=0.12, gain=0.35), L("square", 392, duty=0.25, start=0.12, length=0.12, decay=0.1, gain=0.35),
                            L("square", 523.25, duty=0.25, start=0.24, length=0.12, decay=0.1, gain=0.35), L("square", 783.99, duty=0.25, start=0.36, length=0.6, decay=0.55, vibrato=0.02, vibratoRate=6, gain=0.35)]),
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
    print("sons dos bosses:", len(SFX))


if __name__ == '__main__': main()
