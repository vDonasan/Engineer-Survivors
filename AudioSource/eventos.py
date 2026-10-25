# Acrescenta (ou atualiza) em sfx.json os sons dos eventos aleatórios. Uso: python3 eventos.py && npm run build
import json
from personagens import PATH, L

SFX = {
    # anúncios por tipo de evento
    "evento_positivo": (0.6, [L("square", 659.25, duty=0.25, decay=0.08, gain=0.35), L("square", 783.99, duty=0.25, start=0.09, length=0.1, decay=0.08, gain=0.35),
                              L("square", 1046.5, duty=0.25, start=0.18, length=0.35, decay=0.3, gain=0.35)]),
    "evento_negativo": (0.6, [L("square", 392, duty=0.4, decay=0.15, lp=1800, gain=0.4), L("square", 311.13, duty=0.4, start=0.18, length=0.35, decay=0.32, lp=1800, gain=0.4)]),
    "evento_especial": (0.7, [L("triangle", 523.25, decay=0.1, gain=0.4), L("triangle", 739.99, start=0.1, length=0.1, decay=0.08, gain=0.4),
                              L("triangle", 523.25, start=0.2, length=0.1, decay=0.08, gain=0.4), L("triangle", 739.99, start=0.3, length=0.35, decay=0.3, gain=0.4)]),
    "evento_sucesso": (0.5, [L("square", 1046.5, duty=0.25, decay=0.06, gain=0.3), L("square", 1318.5, duty=0.25, start=0.07, length=0.08, decay=0.06, gain=0.3),
                             L("square", 1568, duty=0.25, start=0.14, length=0.3, decay=0.25, gain=0.3)]),
    # efeitos
    "notificacao": (0.18, [L("sine", 1568, decay=0.05, gain=0.35), L("sine", 2093, start=0.06, length=0.1, decay=0.08, gain=0.35)]),
    "carimbo": (0.3, [L("noise", 2000, attack=0.001, decay=0.08, lp=1500, gain=0.7), L("sine", 140, freqEnd=60, decay=0.2, gain=0.8)]),
    "papel_amassado": (0.4, [L("noise", 6000, crush=True, attack=0.01, decay=0.35, hp=2500, gain=0.5)]),
    "buzina_burro": (0.5, [L("square", 330, freqEnd=300, duty=0.5, decay=0.2, lp=1600, gain=0.5), L("square", 262, freqEnd=230, duty=0.5, start=0.22, length=0.25, decay=0.22, lp=1600, gain=0.5)]),
    "moto_aviso": (1.4, [L("square", 880, duty=0.5, decay=0.12, gain=0.3), L("square", 880, duty=0.5, start=0.35, length=0.15, decay=0.12, gain=0.3),
                         L("saw", 60, freqEnd=90, start=0.6, decay=0.8, lp=900, gain=0.5)]),
    "moto": (1.2, [L("saw", 90, freqEnd=260, attack=0.05, decay=1.1, lp=1400, vibrato=0.15, vibratoRate=35, gain=0.6), L("noise", 1500, decay=1.0, lp=800, gain=0.4)]),
    "coxinha": (0.35, [L("noise", 3000, attack=0.002, decay=0.08, lp=2500, gain=0.6), L("noise", 3000, start=0.12, decay=0.08, lp=2500, gain=0.5),
                       L("sine", 523.25, freqEnd=784, start=0.2, decay=0.12, gain=0.35)]),
    "coxinha_suprema": (0.9, [L("sine", 523.25, freqEnd=1046.5, decay=0.3, gain=0.4), L("noise", 4000, start=0.25, decay=0.5, lp=3000, gain=0.6),
                              L("sine", 80, freqEnd=40, start=0.25, decay=0.5, drive=2, gain=0.9)]),
    "jalapeno": (0.8, [L("noise", 5000, attack=0.05, decay=0.7, hp=2500, gain=0.5), L("saw", 220, freqEnd=660, decay=0.5, lp=2200, gain=0.3)]),
    "alivio": (0.5, [L("sine", 600, freqEnd=300, attack=0.05, decay=0.45, gain=0.35)]),
    "tontura": (0.6, [L("sine", 440, vibrato=0.3, vibratoRate=6, attack=0.05, decay=0.55, gain=0.35)]),
    "reflexo": (0.4, [L("saw", 400, freqEnd=1600, attack=0.002, decay=0.35, lp=4000, gain=0.35)]),
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
    print("sons dos eventos:", len(SFX))


if __name__ == '__main__': main()
