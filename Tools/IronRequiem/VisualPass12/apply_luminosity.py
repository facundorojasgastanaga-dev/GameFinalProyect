#!/usr/bin/env python3
"""Pase visual 12 / F2 / rol 3: aplica la luminosidad por tramos a Levels/Map01/Data/Lighting/R01..R18.json.

Idempotente: SIEMPRE lee los originales del respaldo
  Tools/IronRequiem/Backups/20261121-visualpass-f2f5/Assets/IronRequiem/Levels/Map01/Data/Lighting/
y escribe el resultado en Assets/IronRequiem/Levels/Map01/Data/Lighting/ (mismo formato: indent 2, LF, salto final).
Uso:  python Tools/IronRequiem/VisualPass12/apply_luminosity.py [--dry]

Tramo 1 (R01-R05): ambiente x2.2, luces x1.5, alcance x1.25, +0.6 EV, niebla x0.8
Tramo 2 (R06-R13): ambiente x2.2, luces x1.7, alcance x1.3,  +0.6 EV, niebla x0.8
Tramo 3 (R14-R18): ambiente x2.7, luces x1.8, alcance x1.35, +0.8 EV, niebla x0.7
El ambiente se multiplica en los colores sky/equator/ground (en modo Trilight de URP ambientIntensity no escala los colores,
asi que ambientIntensity se deja en su valor). Las luces con intensidad 0 (alarma, fases) siguen en 0.
Los campos nuevos "tramo" y "exposureEV" los lee LightingAuthoring.RoomData (Newtonsoft ignora los desconocidos).
"""
import json, os, sys
from collections import OrderedDict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
SRC = os.path.join(ROOT, "Tools", "IronRequiem", "Backups", "20261121-visualpass-f2f5", "Assets", "IronRequiem", "Levels", "Map01", "Data", "Lighting")
DST = os.path.join(ROOT, "Assets", "IronRequiem", "Levels", "Map01", "Data", "Lighting")

#            tramo: (ambient, lights, range, EV, fog)
# Alcance x1: ampliarlo subia de 10 a 13 los cuartos con mas de 4 luces por objeto (validador, 21/11/2026). Se compensa con intensidad.
TRAMOS = {1: (2.2, 1.6, 1.0, 0.6, 0.8), 2: (2.2, 1.8, 1.0, 0.6, 0.8), 3: (2.7, 1.9, 1.0, 0.8, 0.7)}

def tramo_of(n):
    return 1 if n <= 5 else (2 if n <= 13 else 3)

def r(v, nd):
    v = round(v, nd)
    return int(v) if v == int(v) else v

def main():
    dry = "--dry" in sys.argv
    for n in range(1, 19):
        rid = "R%02d" % n
        with open(os.path.join(SRC, rid + ".json"), encoding="utf-8") as f:
            d = json.load(f, object_pairs_hook=OrderedDict)
        t = tramo_of(n); amb, li, rg, ev, fog = TRAMOS[t]
        out = OrderedDict()
        for k, v in d.items():
            out[k] = v
            if k == "zone":
                out["tramo"] = t; out["exposureEV"] = ev
        if "tramo" not in out: out["tramo"] = t; out["exposureEV"] = ev
        env = out["environment"]
        for key in ("sky", "equator", "ground"): env[key] = [r(c * amb, 3) for c in env[key]]
        env["fogDensity"] = r(env["fogDensity"] * fog, 5)
        for l in out["lights"]:
            l["intensity"] = r(l["intensity"] * li, 2); l["range"] = r(l["range"] * rg, 2)
        text = json.dumps(out, indent=2, ensure_ascii=False) + "\n"
        if not dry:
            with open(os.path.join(DST, rid + ".json"), "w", encoding="utf-8", newline="\n") as f: f.write(text)
        print(rid, "tramo", t, "EV", ev, "luces", len(out["lights"]), "fog", env["fogDensity"])

if __name__ == "__main__":
    main()
