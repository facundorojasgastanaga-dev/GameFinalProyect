#!/usr/bin/env python3
"""IRON REQUIEM · Room00_Hangar · capas sonoras de la cinemática de despertar (v2).

Síntesis procedural y reproducible (solo biblioteca estándar: wave, math, array, random con semilla fija).
Escribe WAV PCM 16 bits / 48 kHz en Assets/IronRequiem/Audio/SFX/HangarIntro_v2/ y un informe con duración,
pico, RMS y SHA-256 de cada archivo en Tools/IronRequiem/Reports/Map01/Hangar/synth_hangar_intro.txt.

Uso (desde la raíz del proyecto o desde cualquier sitio):
    python Tools/IronRequiem/Audio/synth_hangar_intro.py            # todos
    python Tools/IronRequiem/Audio/synth_hangar_intro.py Boot Heart # solo los que contienen esas palabras

Reglas de calidad aplicadas a todos los sonidos:
  * Todo el procesado (filtros, reverberación) se hace sobre búferes largos ANTES de cerrar el bucle, para que el
    estado de los filtros no deje costura. Los bucles se cierran plegando las colas de los eventos (convolución
    periódica exacta) y con fundido de potencia constante en las capas de ruido continuo. Las capas tonales usan
    frecuencias con un número entero de ciclos en la longitud del bucle.
  * Sin clics: los one-shot empiezan en cero (ataque >= 1 ms) y terminan con fundido; se elimina la continua (DC).
  * Sin clipping: normalización por pico (one-shots) o por RMS con techo de pico (ambientes); dither TPDF de 1 LSB.
No usa servicios externos, descargas ni modelos generativos.
"""
import array
import hashlib
import math
import os
import random
import sys
import time
import wave

SR = 48000
TAU = 2.0 * math.pi
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "IronRequiem", "Audio", "SFX", "HangarIntro_v2")
REPORT = os.path.join(ROOT, "Tools", "IronRequiem", "Reports", "Map01", "Hangar", "synth_hangar_intro.txt")
MASTER_SEED = 0x1A_2026_11


# --------------------------------------------------------------------------------------------- básicos
def n_of(seconds):
    return int(round(seconds * SR))


def zeros(n):
    return [0.0] * n


def rng(tag):
    """RNG independiente y estable por sonido/capa (no depende del orden de generación)."""
    h = int(hashlib.sha256(("%d:%s" % (MASTER_SEED, tag)).encode()).hexdigest()[:12], 16)
    return random.Random(h)


def white(n, r):
    g = r.random
    return [2.0 * g() - 1.0 for _ in range(n)]


def pink(n, r):
    """Ruido rosa (filtro de Paul Kellet, versión económica)."""
    g = r.random
    b0 = b1 = b2 = 0.0
    out = [0.0] * n
    for i in range(n):
        w = 2.0 * g() - 1.0
        b0 = 0.99765 * b0 + w * 0.0990460
        b1 = 0.96300 * b1 + w * 0.2965164
        b2 = 0.57000 * b2 + w * 1.0526913
        out[i] = (b0 + b1 + b2 + w * 0.1848) * 0.2
    return out


def brown(n, r):
    """Ruido marrón con fuga (sin deriva de continua)."""
    g = r.random
    x = 0.0
    out = [0.0] * n
    for i in range(n):
        x = 0.9985 * x + 0.03 * (2.0 * g() - 1.0)
        out[i] = x
    return out


def add_into(dst, src, at=0, gain=1.0):
    n = min(len(src), len(dst) - at)
    for i in range(max(0, -at), n):
        dst[at + i] += gain * src[i]
    return dst


def mix(*layers):
    n = max(len(l) for l in layers)
    out = [0.0] * n
    for l in layers:
        for i, v in enumerate(l):
            out[i] += v
    return out


def scale(x, g):
    return [v * g for v in x]


def mul(x, env):
    return [a * b for a, b in zip(x, env)]


def rms(x):
    return math.sqrt(sum(v * v for v in x) / max(1, len(x)))


def peak(x):
    return max(abs(v) for v in x) if x else 0.0


def to_rms(x, target):
    r = rms(x)
    return scale(x, target / r) if r > 1e-12 else x


def db(v):
    return 20.0 * math.log10(max(v, 1e-12))


def undb(d):
    return 10.0 ** (d / 20.0)


def smooth(u):
    u = 0.0 if u < 0 else 1.0 if u > 1 else u
    return u * u * (3.0 - 2.0 * u)


def soft_clip(x, drive=1.5):
    k = math.tanh(drive)
    return [math.tanh(drive * v) / k for v in x]


# --------------------------------------------------------------------------------------------- filtros
def _coefs(kind, f, q):
    f = min(max(f, 10.0), SR * 0.45)
    w0 = TAU * f / SR
    c, s = math.cos(w0), math.sin(w0)
    al = s / (2.0 * q)
    if kind == "lp":
        b0, b1, b2 = (1 - c) / 2, 1 - c, (1 - c) / 2
    elif kind == "hp":
        b0, b1, b2 = (1 + c) / 2, -(1 + c), (1 + c) / 2
    else:  # bp, ganancia 0 dB en el pico
        b0, b1, b2 = al, 0.0, -al
    a0, a1, a2 = 1 + al, -2 * c, 1 - al
    return b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0


def biquad(x, kind, f, q=0.707):
    b0, b1, b2, a1, a2 = _coefs(kind, f, q)
    z1 = z2 = 0.0
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        y = b0 * v + z1
        z1 = b1 * v - a1 * y + z2
        z2 = b2 * v - a2 * y
        out[i] = y
    return out


def biquad_sweep(x, kind, freq_of_t, q=0.707, block=64):
    """Filtro con frecuencia variable (coeficientes recalculados cada `block` muestras, estado continuo)."""
    z1 = z2 = 0.0
    out = [0.0] * len(x)
    n = len(x)
    for s0 in range(0, n, block):
        b0, b1, b2, a1, a2 = _coefs(kind, freq_of_t(s0 / SR), q)
        for i in range(s0, min(n, s0 + block)):
            v = x[i]
            y = b0 * v + z1
            z1 = b1 * v - a1 * y + z2
            z2 = b2 * v - a2 * y
            out[i] = y
    return out


def lp1(x, fc):
    a = math.exp(-TAU * fc / SR)
    y = 0.0
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        y = (1 - a) * v + a * y
        out[i] = y
    return out


def dc_block(x, fc=18.0):
    a = math.exp(-TAU * fc / SR)
    px = py = 0.0
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        py = v - px + a * py
        px = v
        out[i] = py
    return out


def reverb(x, wet=0.3, room=0.84, damp=0.3, spread=0, predelay=0.012):
    """Reverberación tipo Freeverb (4 peines amortiguados + 2 pasa-todo). Devuelve seco + húmedo."""
    k = SR / 44100.0
    combs = [int((d + spread) * k) for d in (1116, 1188, 1277, 1356)]
    alls = [int((d + spread) * k) for d in (556, 441)]
    n = len(x)
    pd = n_of(predelay)
    src = [0.0] * pd + x[: n - pd] if pd < n else [0.0] * n
    acc = [0.0] * n
    for d in combs:
        buf = [0.0] * d
        idx = 0
        store = 0.0
        for i in range(n):
            o = buf[idx]
            store = o * (1 - damp) + store * damp
            buf[idx] = src[i] * 0.015 + store * room
            acc[i] += o
            idx += 1
            if idx == d:
                idx = 0
    for d in alls:
        buf = [0.0] * d
        idx = 0
        for i in range(n):
            b = buf[idx]
            v = acc[i]
            acc[i] = -v + b
            buf[idx] = v + b * 0.5
            idx += 1
            if idx == d:
                idx = 0
    return [a + wet * b for a, b in zip(x, acc)]


# --------------------------------------------------------------------------------------------- generadores
def decaying_sine(n, f, tau, amp=1.0, phase=0.0, attack=0.0015):
    """Seno amortiguado por recurrencia (rápido); ataque lineal corto para evitar clics."""
    out = [0.0] * n
    if f >= SR * 0.45:
        return out
    r = math.exp(-1.0 / (tau * SR))
    w = TAU * f / SR
    c = 2.0 * r * math.cos(w)
    r2 = r * r
    y1 = amp * math.sin(phase - w) / r
    y2 = amp * math.sin(phase - 2 * w) / r2
    na = max(1, n_of(attack))
    for i in range(n):
        y = c * y1 - r2 * y2
        out[i] = y * (i / na if i < na else 1.0)
        y2, y1 = y1, y
        if i > na and abs(y) < 1e-7 and abs(y2) < 1e-7:
            break
    return out


def modal(n, freqs, amps, taus, attack=0.001):
    out = [0.0] * n
    for f, a, t in zip(freqs, amps, taus):
        add_into(out, decaying_sine(n, f, t, a, 0.0, attack))
    return out


def glide_sine(n, f_of_t, amp_of_t, harmonics=((1, 1.0),), phase=0.0):
    out = [0.0] * n
    ph = phase
    for i in range(n):
        t = i / SR
        a = amp_of_t(t)
        if a != 0.0:
            s = 0.0
            for h, ha in harmonics:
                s += ha * math.sin(h * ph)
            out[i] = a * s
        ph += TAU * f_of_t(t) / SR
    return out


def thump(n, f_hi, f_lo, f_tau, tau, amp=1.0, attack=0.002):
    """Golpe grave: seno con caída de tono exponencial."""
    return glide_sine(n, lambda t: f_lo + (f_hi - f_lo) * math.exp(-t / f_tau),
                      lambda t: amp * (t / attack if t < attack else math.exp(-(t - attack) / tau)))


def burst(n, r, tau, amp=1.0, kind="hp", fc=1500.0, attack=0.0005):
    x = white(n, r)
    x = biquad(x, kind, fc, 0.7)
    na = max(1, n_of(attack))
    return [amp * v * (i / na if i < na else math.exp(-(i - na) / (tau * SR))) for i, v in enumerate(x)]


def env_ar(n, attack, release_tau, hold=0.0):
    na, nh = max(1, n_of(attack)), n_of(hold)
    return [smooth(i / na) if i < na else 1.0 if i < na + nh else math.exp(-(i - na - nh) / (release_tau * SR)) for i in range(n)]


def fade_edges(x, fin=0.001, fout=0.01):
    n = len(x)
    a, b = max(1, n_of(fin)), max(1, n_of(fout))
    y = list(x)
    for i in range(min(a, n)):
        y[i] *= i / a
    for i in range(min(b, n)):
        y[n - 1 - i] *= i / b
    return y


# --------------------------------------------------------------------------------------------- bucles
def fold(x, L):
    """Convierte un búfer largo de eventos en periódico de longitud L (las colas que pasan de L vuelven al inicio)."""
    out = list(x[:L]) + [0.0] * max(0, L - len(x))
    for i in range(L, len(x)):
        out[i % L] += x[i]
    return out


def xfade_loop(x, L, X):
    """Bucle de L muestras a partir de un búfer de L+X: fundido de potencia constante del final sobre el inicio."""
    assert len(x) >= L + X
    out = list(x[:L])
    for i in range(X):
        u = i / X
        out[i] = x[i] * math.sin(0.5 * math.pi * u) + x[L + i] * math.cos(0.5 * math.pi * u)
    return out


# --------------------------------------------------------------------------------------------- salida
def finish_peak(x, peak_db, fin=0.001, fout=0.02):
    x = dc_block(x)
    x = fade_edges(x, fin, fout)
    p = peak(x)
    return scale(x, undb(peak_db) / p) if p > 0 else x


def finish_loop(x, rms_db, ceiling_db):
    """Ambientes: RMS objetivo y techo de pico. Sin fundidos (es un bucle). El DC se quita antes de cerrar el bucle."""
    g = undb(rms_db) / max(rms(x), 1e-12)
    if peak(x) * g > undb(ceiling_db):
        g = undb(ceiling_db) / peak(x)
    return scale(x, g)


def write_wav(name, channels, tag):
    """channels: lista de listas (1 = mono, 2 = estéreo). Cuantiza a 16 bits con dither TPDF reproducible."""
    os.makedirs(OUT_DIR, exist_ok=True)
    n = len(channels[0])
    for c in channels:
        if len(c) != n:
            raise ValueError(name + ": canales de distinta longitud")
        for v in c:
            if v != v or abs(v) > 1.0:
                raise ValueError(name + ": muestra fuera de rango o NaN (%r)" % v)
    r = rng("dither:" + tag)
    data = array.array("h", [0]) * (n * len(channels))
    k = 0
    for i in range(n):
        for c in channels:
            d = (r.random() - r.random())  # TPDF, +-1 LSB
            s = int(round(c[i] * 32766.0 + d))
            data[k] = -32767 if s < -32767 else 32767 if s > 32767 else s
            k += 1
    if sys.byteorder == "big":
        data.byteswap()
    path = os.path.join(OUT_DIR, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(len(channels))
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    with open(path, "rb") as f:
        sha = hashlib.sha256(f.read()).hexdigest()
    allv = [v for c in channels for v in c]
    return {"name": name, "ch": len(channels), "dur": n / SR, "peak": db(peak(allv)), "rms": db(rms(allv)), "sha": sha[:16]}


def loop_check(channels):
    """Salto en la costura del bucle comparado con el salto típico entre muestras (debe ser del mismo orden)."""
    worst = 0.0
    for c in channels:
        typical = sum(abs(c[i + 1] - c[i]) for i in range(0, len(c) - 1, 7)) / (len(c) // 7)
        worst = max(worst, abs(c[0] - c[-1]) / max(typical, 1e-9))
    return worst


# ============================================================================================= SONIDOS
def amb_hangar_tone():
    """Tono de sala del hangar (2D, estéreo, 20 s): zumbido, retumbo, viento metálico, aire y golpes lejanos."""
    L, X = n_of(20.0), n_of(2.0)
    chans = []
    # golpes y crujidos lejanos compartidos (mismo instante en ambos canales, paneo distinto)
    r_ev = rng("tone:events")
    events = []
    t = 1.3
    while t < 20.0:
        events.append((t, r_ev.uniform(0.45, 1.0), r_ev.uniform(-0.8, 0.8), r_ev.random() < 0.3))
        t += r_ev.uniform(2.4, 4.6)
    for ch in (0, 1):
        r = rng("tone:ch%d" % ch)
        # 1) zumbido eléctrico/estructural: ciclos enteros en 20 s (47 Hz y 47.15 Hz -> batido lento entre canales)
        f0 = 47.0 if ch == 0 else 47.15
        hum = [0.0] * L
        parts = ((1, 1.0), (2, 0.55), (3, 0.30), (4, 0.18), (5, 0.08))
        for i in range(L):
            tt = i / SR
            am = 0.78 + 0.22 * math.sin(TAU * 0.05 * tt + ch * 1.1) * math.sin(TAU * 0.1 * tt + 0.4)
            s = 0.0
            for h, a in parts:
                s += a * math.sin(TAU * f0 * h * tt + h * 0.7)
            hum[i] = am * s
        hum = to_rms(hum, 0.040)
        # 2) retumbo de la sala (ruido marrón, paso bajo)
        rum = biquad(brown(L + X, r), "lp", 140.0, 0.7)
        rum = biquad(rum, "hp", 22.0, 0.7)
        rum = to_rms(rum, 0.045)
        # 3) viento metálico: ruido rosa por tres resonancias que deambulan y ráfagas lentas
        pk = pink(L + X, r)
        wind = [0.0] * (L + X)
        for base, amp, rate, q in ((420.0, 0.5, 0.071, 7.0), (910.0, 0.35, 0.053, 9.0), (1730.0, 0.2, 0.089, 11.0)):
            ph = r.uniform(0, TAU)
            band = biquad_sweep(pk, "bp", lambda tt, b=base, rr=rate, p=ph: b * (1.0 + 0.09 * math.sin(TAU * rr * tt + p)), q, 128)
            add_into(wind, band, 0, amp)
        g1, g2 = r.uniform(0, TAU), r.uniform(0, TAU)
        wind = [v * (0.35 + 0.65 * (0.5 + 0.5 * math.sin(TAU * 0.045 * (i / SR) + g1)) * (0.6 + 0.4 * math.sin(TAU * 0.13 * (i / SR) + g2)))
                for i, v in enumerate(wind)]
        wind = to_rms(wind, 0.030)
        # 4) aire (presencia muy suave en agudos)
        air = biquad(biquad(pink(L + X, r), "hp", 2200.0, 0.7), "lp", 7000.0, 0.7)
        air = to_rms(air, 0.0045)
        # 5) maquinaria lejana: golpes sordos y crujidos, oscuros y reverberados
        evl = [0.0] * (L + n_of(4.0))
        for (et, amp, pan, creak) in events:
            g = amp * (0.5 - 0.5 * pan if ch == 0 else 0.5 + 0.5 * pan) * 1.4
            at = n_of(et)
            add_into(evl, thump(n_of(1.2), 62.0, 38.0, 0.05, 0.28, 1.0), at, g)
            add_into(evl, modal(n_of(1.6), (212.0, 489.0, 931.0, 1377.0), (0.5, 0.35, 0.2, 0.1), (0.7, 0.45, 0.3, 0.2)), at + n_of(0.01), g * 0.6)
            if creak:
                add_into(evl, groan(n_of(1.4), rng("tone:creak%.2f" % et), 0.9), at + n_of(0.35), g * 0.5)
        evl = biquad(evl, "lp", 650.0, 0.7)
        evl = reverb(evl, wet=1.6, room=0.88, damp=0.45, spread=23 * ch, predelay=0.04)
        evl = to_rms(fold(evl, L), 0.022)
        noise = xfade_loop(mix(rum, wind, air), L, X)
        chans.append([a + b + c for a, b, c in zip(hum, noise, evl)])
    # nivel conjunto: RMS -26 dBFS, techo -6 dBFS
    g = undb(-26.0) / max(rms(chans[0] + chans[1]), 1e-12)
    pk_all = max(peak(chans[0]), peak(chans[1]))
    if pk_all * g > undb(-6.0):
        g = undb(-6.0) / pk_all
    return [scale(c, g) for c in chans]


def groan(n, r, amp=1.0):
    """Gemido de metal bajo tensión: tren de impulsos de fricción (stick-slip) por resonancias estrechas."""
    exc = [0.0] * n
    t = 0.0
    while t < n / SR:
        u = t / (n / SR)
        f = 34.0 + 24.0 * math.sin(math.pi * u) + 4.0 * math.sin(TAU * 1.7 * u)
        i = int(t * SR)
        if i < n:
            exc[i] += r.uniform(0.6, 1.0)
        t += (1.0 / f) * r.uniform(0.88, 1.12)
    out = [0.0] * n
    for fc, a in ((380.0, 1.0), (1040.0, 0.7), (1720.0, 0.45), (2650.0, 0.25)):
        add_into(out, biquad(exc, "bp", fc * r.uniform(0.97, 1.03), 15.0), 0, a)
    env = [amp * smooth(min(1.0, (i / n) / 0.3)) * smooth(min(1.0, (1.0 - i / n) / 0.35)) for i in range(n)]
    return mul(out, env)


def amb_drip():
    """Goteo (3D, mono, 11 s): gotas de agua en charcos y en chapa, con la sala del hangar."""
    L = n_of(11.0)
    r = rng("drip")
    buf = [0.0] * (L + n_of(3.0))
    t = 0.4
    while t < 11.0:
        at = n_of(t)
        a = r.uniform(0.35, 1.0)
        if r.random() < 0.65:  # agua: «plip» con subida de tono
            f0 = r.uniform(700.0, 1300.0)
            nn = n_of(0.12)
            drop = glide_sine(nn, lambda tt, f=f0: f * (1.0 + 0.9 * (1.0 - math.exp(-tt / 0.008))),
                              lambda tt: (tt / 0.001 if tt < 0.001 else math.exp(-(tt - 0.001) / 0.03)))
            add_into(buf, drop, at, a * 0.8)
            add_into(buf, burst(n_of(0.01), r, 0.0012, 0.25, "hp", 3000.0), at, a)
        else:  # gota sobre metal
            f = r.uniform(1800.0, 2600.0)
            add_into(buf, modal(n_of(0.5), (f, 2.76 * f, 5.40 * f), (1.0, 0.5, 0.22), (0.12, 0.07, 0.035)), at, a * 0.55)
        t += r.uniform(0.35, 1.6) if r.random() < 0.8 else r.uniform(0.08, 0.16)  # a veces una gota doble
    buf = reverb(buf, wet=0.9, room=0.86, damp=0.35, predelay=0.02)
    buf = biquad(buf, "hp", 120.0, 0.7)
    out = fold(dc_block(buf), L)
    return [finish_loop(out, -24.0, -3.0)]


def amb_machine_far():
    """Maquinaria lejana (3D, mono, 12 s = 8 golpes de prensa de 1.5 s): prensa, cadena, motor. Oscuro y reverberado."""
    L, X = n_of(12.0), n_of(1.5)
    r = rng("machine")
    ev = [0.0] * (L + n_of(4.0))
    for k in range(8):
        at = n_of(k * 1.5 + 0.05)
        a = 1.0 if k % 4 != 3 else 0.82
        add_into(ev, thump(n_of(1.0), 58.0, 37.0, 0.04, 0.33, 1.0), at, a)
        add_into(ev, burst(n_of(0.3), r, 0.07, 0.5, "lp", 380.0), at, a)
        add_into(ev, modal(n_of(0.8), (330.0, 742.0, 1310.0), (1.0, 0.6, 0.3), (0.2, 0.14, 0.09)), at + n_of(0.75), 0.32 * a)
        tt = 0.92
        while tt < 1.32:  # cadena
            add_into(ev, biquad(burst(n_of(0.04), r, 0.006, 1.0, "hp", 1500.0), "bp", r.uniform(2000.0, 3200.0), 3.0), at + n_of(tt), r.uniform(0.15, 0.35))
            tt += r.uniform(0.018, 0.04)
    ev = biquad(ev, "lp", 1100.0, 0.7)
    ev = reverb(ev, wet=1.2, room=0.87, damp=0.5, predelay=0.03)
    ev = to_rms(fold(ev, L), 0.06)
    # motor: 92 Hz con armónicos (ciclos enteros en 12 s), con un leve bombeo al ritmo de la prensa (1/1.5 Hz)
    motor = [0.0] * L
    for i in range(L):
        tt = i / SR
        pump = 0.82 + 0.18 * math.cos(TAU * tt / 1.5)
        motor[i] = pump * (math.sin(TAU * 92.0 * tt) + 0.5 * math.sin(TAU * 184.0 * tt + 0.3) + 0.25 * math.sin(TAU * 276.0 * tt + 1.1))
    motor = to_rms(biquad(motor, "lp", 900.0, 0.7), 0.028)
    hiss = to_rms(biquad(brown(L + X, r), "lp", 260.0, 0.7), 0.03)
    out = [a + b + c for a, b, c in zip(ev, motor, xfade_loop(hiss, L, X))]
    return [finish_loop(out, -21.0, -3.0)]


def amb_elec_hum():
    """Zumbido eléctrico de la lámpara (3D, mono, 6 s): 100 Hz rico en impares, aleteo y chisporroteo."""
    L, X = n_of(6.0), n_of(0.8)
    r = rng("hum")
    period = SR // 100  # 480 muestras exactas
    table = [0.0] * period
    for h in range(1, 41):
        a = (1.0 / h ** 1.05) * (1.0 if h % 2 else 0.45)
        ph = r.uniform(0, TAU)
        for i in range(period):
            table[i] += a * math.sin(TAU * h * i / period + ph)
    buzz = [table[i % period] for i in range(L)]
    buzz = [v * (0.82 + 0.1 * math.sin(TAU * 0.5 * (i / SR)) + 0.08 * math.sin(TAU * 7.5 * (i / SR))) for i, v in enumerate(buzz)]
    buzz = to_rms(biquad(biquad(buzz, "lp", 3800.0, 0.7), "hp", 70.0, 0.7), 0.05)
    crack = [0.0] * (L + n_of(0.5))
    t = 0.3
    while t < 6.0:
        at = n_of(t)
        for _ in range(r.randint(1, 4)):
            add_into(crack, burst(n_of(0.03), r, r.uniform(0.0015, 0.004), r.uniform(0.3, 1.0), "hp", 2500.0), at + n_of(r.uniform(0, 0.03)))
        t += r.uniform(0.6, 1.9)
    crack = to_rms(fold(biquad(crack, "lp", 9000.0, 0.7), L), 0.012)
    hiss = to_rms(biquad(white(L + X, r), "bp", 5200.0, 0.8), 0.006)
    out = [a + b + c for a, b, c in zip(buzz, crack, xfade_loop(hiss, L, X))]
    return [finish_loop(out, -22.0, -4.0)]


def robot_boot():
    """Arranque de sistemas (2D, 3.4 s): relé, tono ascendente con tartamudeo del visor, tres pitidos, «listo», datos."""
    n = n_of(3.4)
    r = rng("boot")
    out = [0.0] * n
    add_into(out, burst(n_of(0.05), r, 0.004, 0.6, "hp", 1500.0), 0)
    add_into(out, thump(n_of(0.4), 110.0, 70.0, 0.02, 0.06, 0.5), 0)

    def f(t):
        u = min(1.0, max(0.0, (t - 0.12) / 1.88))
        return 70.0 * 6.0 ** (u ** 1.4)

    def a(t):
        if t < 0.12:
            return 0.0
        up = smooth((t - 0.12) / 1.5)
        down = 1.0 - smooth((t - 1.95) / 0.35)
        stutter = 1.0
        if 0.35 < t < 0.9:  # el visor se enciende a trompicones
            g = 0.5 + 0.5 * math.sin(TAU * 12.0 * t)
            stutter = 0.35 + 0.65 * (g * g)
        return 0.5 * up * down * stutter

    tone = glide_sine(n, f, a, ((1, 1.0), (2, 0.35), (3, 0.15)))
    add_into(out, tone)
    noise = biquad_sweep(pink(n, r), "bp", lambda t: 6.0 * f(t), 3.0)
    add_into(out, [v * a(i / SR) * 0.6 for i, v in enumerate(noise)])
    for t0, fr in ((1.25, 1318.5), (1.45, 1568.0), (1.65, 1975.5)):
        nn = n_of(0.12)
        add_into(out, glide_sine(nn, lambda t, fr=fr: fr, lambda t: 0.35 * (t / 0.003 if t < 0.003 else math.exp(-(t - 0.003) / 0.035)), ((1, 1.0), (2, 0.2))), n_of(t0))
    add_into(out, glide_sine(n_of(0.5), lambda t: 880.0, lambda t: 0.3 * (t / 0.005 if t < 0.005 else math.exp(-(t - 0.005) / 0.09)), ((1, 1.0), (1.5, 0.8))), n_of(2.05))
    add_into(out, thump(n_of(0.4), 90.0, 58.0, 0.015, 0.09, 0.45), n_of(2.05))
    scale_notes = (2093.0, 2349.3, 2637.0, 3136.0, 3520.0)
    t = 2.3
    while t < 3.1:
        d = r.uniform(0.012, 0.025)
        fr = r.choice(scale_notes)
        add_into(out, glide_sine(n_of(d + 0.01), lambda tt, fr=fr: fr, lambda tt, d=d: r_env(tt, d, 0.002) * 0.12), n_of(t))
        t += r.uniform(0.035, 0.09)
    out = reverb(out, wet=0.18, room=0.8, damp=0.35)
    return [finish_peak(out, -1.0, 0.001, 0.08)]


def r_env(t, dur, edge):
    if t < edge:
        return t / edge
    if t > dur:
        return max(0.0, 1.0 - (t - dur) / edge)
    return 1.0


def robot_heart():
    """Latido del reactor (2D, mono, bucle de 1.5 s = 40 lpm): «lub-dub» sub-grave con un poco de cuerpo."""
    L = n_of(1.5)
    r = rng("heart")
    buf = [0.0] * (L + n_of(1.0))
    for t0, a in ((0.06, 1.0), (0.34, 0.6)):
        at = n_of(t0)
        add_into(buf, thump(n_of(0.9), 78.0, 42.0, 0.03, 0.16, 1.0, 0.004), at, a)
        add_into(buf, burst(n_of(0.2), r, 0.05, 0.15, "lp", 180.0, 0.003), at, a)
    buf = soft_clip(buf, 1.6)  # armónicos (84/126 Hz) para que se oiga también en altavoces pequeños
    out = fold(dc_block(buf), L)
    p = peak(out)
    return [scale(out, undb(-3.0) / p)]


def robot_blip():
    """Pitido de visor (2D, 0.25 s): dos notas cortas ascendentes."""
    n = n_of(0.25)
    out = [0.0] * n
    for t0, fr in ((0.0, 1760.0), (0.07, 2349.3)):
        add_into(out, glide_sine(n_of(0.07), lambda t, fr=fr: fr, lambda t: r_env(t, 0.045, 0.002) * 0.5, ((1, 1.0), (3, 0.12))), n_of(t0) + 48)
    out = reverb(out, wet=0.12, room=0.7, damp=0.4)
    return [finish_peak(out, -3.0, 0.001, 0.03)]


def robot_scan():
    """Escaneo de la mano (2D, 1.6 s): barrido filtrado con trémolo de escáner, tono fino y charla de datos."""
    n = n_of(1.6)
    r = rng("scan")

    def c(t):
        return 600.0 * 7.0 ** smooth(t / 0.9) if t < 0.9 else 4200.0 - 2700.0 * smooth((t - 0.9) / 0.7)

    sw = biquad_sweep(white(n, r), "bp", c, 5.0)
    env = [smooth(i / n_of(0.08)) * (1.0 - smooth((i - n_of(1.3)) / n_of(0.3))) for i in range(n)]
    out = [v * e * (0.55 + 0.45 * math.sin(TAU * 26.0 * i / SR)) for i, (v, e) in enumerate(zip(sw, env))]
    out = to_rms(out, 0.12)
    add_into(out, glide_sine(n, lambda t: 2200.0 + 400.0 * t / 1.6, lambda t: 0.04 * env[min(n - 1, int(t * SR))]))
    t = 0.15
    while t < 1.35:
        d = r.uniform(0.008, 0.02)
        fr = r.choice((1975.5, 2349.3, 2637.0, 3136.0))
        add_into(out, glide_sine(n_of(d + 0.01), lambda tt, fr=fr: fr, lambda tt, d=d: r_env(tt, d, 0.0015) * 0.09), n_of(t))
        t += r.uniform(0.05, 0.12)
    return [finish_peak(out, -3.0, 0.002, 0.05)]


def robot_step_weight():
    """Peso de la pisada (3D, 0.7 s): golpe sub, resonancia del suelo metálico y polvo grave."""
    n = n_of(0.7)
    r = rng("step")
    out = thump(n, 75.0, 45.0, 0.025, 0.14, 1.0)
    add_into(out, modal(n, (140.0, 317.0, 602.0), (0.35, 0.22, 0.12), (0.3, 0.18, 0.1)))
    add_into(out, burst(n_of(0.2), r, 0.03, 0.3, "lp", 600.0))
    out = soft_clip(out, 1.3)
    return [finish_peak(out, -1.0, 0.001, 0.05)]


def capsule_steam():
    """Vapor de la cápsula (3D, 3.6 s): golpe de válvula, chorro presurizado que se apaga con tartamudeo."""
    n = n_of(3.6)
    r = rng("steam")
    out = thump(n_of(0.6), 95.0, 60.0, 0.02, 0.12, 0.5)
    out += [0.0] * (n - len(out))
    hiss = biquad(white(n, r), "hp", 800.0, 0.7)
    hiss = biquad_sweep(hiss, "bp", lambda t: 2400.0 + 2800.0 * math.exp(-t / 0.9), 0.9)
    flutter = lp1(white(n, rng("steam:flutter")), 12.0)
    fp = peak(flutter) or 1.0
    env = []
    for i in range(n):
        t = i / SR
        e = smooth(t / 0.015) * (0.45 + 0.55 * math.exp(-t / 0.25)) * (1.0 - smooth((t - 0.8) / 2.8))
        e *= 1.0 - 0.45 * smooth(t / 2.5) * (0.5 + 0.5 * flutter[i] / fp)
        env.append(e)
    hiss = to_rms(mul(hiss, env), 0.12)
    add_into(out, hiss)
    add_into(out, glide_sine(n_of(0.3), lambda t: 3100.0 - 300.0 * t, lambda t: 0.03 * r_env(t, 0.2, 0.01)))
    return [finish_peak(out, -1.0, 0.001, 0.3)]


def capsule_clamp():
    """Anclaje que se suelta (3D, 1.1 s): chasquido, golpe metálico pesado, pestillo deslizante y asiento."""
    n = n_of(1.1)
    r = rng("clamp")
    out = burst(n_of(0.05), r, 0.003, 0.6, "hp", 2000.0)
    out += [0.0] * (n - len(out))
    ratios = (1.0, 2.32, 4.25, 6.63, 9.38, 13.1)
    add_into(out, modal(n, [160.0 * k for k in ratios], (1.0, 0.7, 0.5, 0.35, 0.2, 0.12), (0.35, 0.22, 0.15, 0.1, 0.07, 0.05)), n_of(0.005))
    add_into(out, thump(n_of(0.6), 105.0, 68.0, 0.015, 0.12, 0.8), n_of(0.004))
    slide = biquad_sweep(white(n_of(0.16), r), "bp", lambda t: 2400.0 - 1500.0 * t / 0.16, 4.0)
    slide = [v * math.sin(math.pi * i / len(slide)) for i, v in enumerate(slide)]
    add_into(out, to_rms(slide, 0.08), n_of(0.16))
    add_into(out, modal(n_of(0.6), [230.0 * k for k in ratios[:4]], (0.4, 0.3, 0.2, 0.1), (0.18, 0.12, 0.08, 0.05)), n_of(0.42))
    out = soft_clip(reverb(out, wet=0.2, room=0.78, damp=0.3), 1.2)
    return [finish_peak(out, -1.0, 0.0008, 0.08)]


def capsule_creak():
    """Crujido de la cuna al inclinarse (3D, 2.4 s)."""
    n = n_of(2.4)
    out = groan(n, rng("creak"), 1.0)
    out = reverb(out, wet=0.3, room=0.82, damp=0.4)
    return [finish_peak(out, -2.0, 0.005, 0.2)]


def impact_sub_boom():
    """Sub-impacto (2D, 2.4 s): caída de 85 a 30 Hz, transitorio y cola oscura."""
    n = n_of(2.4)
    r = rng("boom")
    out = thump(n, 85.0, 30.0, 0.18, 0.6, 1.0, 0.0025)
    out = soft_clip(out, 2.0)
    add_into(out, burst(n_of(0.2), r, 0.02, 0.5, "lp", 2500.0))
    add_into(out, burst(n_of(1.0), r, 0.25, 0.25, "lp", 300.0, 0.002))
    wet = biquad(reverb(out, wet=1.0, room=0.86, damp=0.6), "lp", 900.0, 0.7)
    out = [a + 0.3 * (b - a) for a, b in zip(out, wet)]
    return [finish_peak(out, -1.0, 0.0008, 0.4)]


def impact_pre_suck():
    """Succión previa al golpe (2D, 0.62 s): cola de impacto invertida que crece y se corta justo antes del golpe."""
    n = n_of(0.62)
    r = rng("suck")
    fwd = burst(n, r, 0.18, 1.0, "lp", 5000.0, 0.001)
    fwd = reverb(fwd, wet=0.6, room=0.8, damp=0.3)
    rev = fwd[::-1]
    sub = glide_sine(n, lambda t: 28.0 + 27.0 * (t / 0.62), lambda t: 0.35 * (t / 0.62) ** 2)
    out = [a + b for a, b in zip(to_rms(rev, 0.12), sub)]
    return [finish_peak(out, -3.0, 0.01, 0.004)]


def impact_glass_debris():
    """Lluvia de cristales (3D, 2.6 s): trozos grandes al principio y esquirlas que rebotan y se asientan."""
    n = n_of(2.6)
    r = rng("debris")
    out = [0.0] * n
    for _ in range(6):
        f = r.uniform(1200.0, 2600.0)
        add_into(out, modal(n_of(0.5), (f, 1.62 * f, 2.71 * f), (1.0, 0.6, 0.35), (r.uniform(0.05, 0.12), 0.05, 0.03)), n_of(r.uniform(0.0, 0.35)), r.uniform(0.6, 1.0))
    for _ in range(85):
        t = min(2.3, r.expovariate(1.0 / 0.45))
        a = r.uniform(0.1, 0.5) * math.exp(-t / 1.2)
        f1, f2 = r.uniform(3000.0, 9000.0), r.uniform(3000.0, 9000.0)
        g = modal(n_of(0.08), (f1, f2), (1.0, 0.6), (r.uniform(0.006, 0.025), r.uniform(0.004, 0.015)), 0.0004)
        add_into(out, g, n_of(t), a)
        if r.random() < 0.4:
            add_into(out, burst(n_of(0.01), r, 0.0008, a * 0.6, "hp", 4000.0), n_of(t))
    out = reverb(out, wet=0.25, room=0.75, damp=0.3)
    out = biquad(out, "hp", 400.0, 0.7)
    return [finish_peak(out, -2.0, 0.0005, 0.2)]


def reveal_sting():
    """Acento de revelación de los Carroñeros (2D, estéreo, 5 s; el golpe cae a 1.5 s del inicio)."""
    n = n_of(5.0)
    hit = 1.5
    chans = []
    for ch in (0, 1):
        r = rng("sting:%d" % ch)
        det = 1.0 + (0.0 if ch == 0 else 0.0035)

        def env(t):
            return (t / hit) ** 2 if t < hit else math.exp(-(t - hit) / 1.6)

        drone = [0.0] * n
        for f, a in ((41.2, 1.0), (43.65, 0.8), (61.74, 0.5), (82.4, 0.35)):
            add_into(drone, glide_sine(n, lambda t, f=f: f * det, lambda t, a=a: a * env(t)))
        drone = soft_clip(to_rms(drone, 0.2), 1.8)
        bowed = [0.0] * n
        exc = pink(n, r)
        for f, a in ((311.0, 1.0), (466.0, 0.7), (698.0, 0.5), (1046.0, 0.32), (1480.0, 0.2)):
            add_into(bowed, biquad(exc, "bp", f * det, 30.0), 0, a)
        bowed = [v * (smooth(t / hit) if t < hit else math.exp(-(t - hit) / 2.5)) for v, t in ((v, i / SR) for i, v in enumerate(bowed))]
        bowed = to_rms(bowed, 0.05)
        bell = modal(n - n_of(hit), [146.8 * k * det for k in (0.5, 1.0, 1.183, 1.506, 2.0, 2.514, 2.662, 3.011, 4.166)],
                     (0.6, 1.0, 0.6, 0.5, 0.45, 0.3, 0.25, 0.2, 0.12), (3.2, 2.6, 2.2, 1.8, 1.5, 1.1, 1.0, 0.8, 0.5), 0.002)
        out = [a + b for a, b in zip(drone, bowed)]
        add_into(out, scale(bell, 0.22), n_of(hit))
        add_into(out, thump(n_of(2.0), 70.0, 32.0, 0.12, 0.5, 0.7, 0.003), n_of(hit))
        out = reverb(out, wet=0.35, room=0.87, damp=0.4, spread=23 * ch)
        chans.append(dc_block(out))
    pk_all = max(peak(c) for c in chans)
    return [fade_edges(scale(c, undb(-1.0) / pk_all), 0.01, 0.5) for c in chans]


def trans_whoosh():
    """Entrada en la cabeza (2D, estéreo, 3 s): ráfaga que acelera, riser tonal y «sellado» del casco a 2.78 s."""
    n = n_of(3.0)
    seal = 2.78
    ns = n_of(seal)
    chans = []
    for ch in (0, 1):
        r = rng("whoosh:%d" % ch)
        w = 0.97 if ch == 0 else 1.03

        def u(t):
            return min(1.0, t / seal)

        rush = biquad_sweep(white(n, r), "bp", lambda t: w * 180.0 * 45.0 ** (u(t) ** 2.0), 1.4)
        rush = [v * (0.02 + 0.98 * u(i / SR) ** 3) for i, v in enumerate(rush)]
        rush = to_rms(rush, 0.18)
        low = biquad(brown(n, r), "lp", 300.0, 0.7)
        low = to_rms([v * u(i / SR) ** 2 for i, v in enumerate(low)], 0.06)
        riser = glide_sine(n, lambda t: 90.0 * 8.0 ** (u(t) ** 2.5) * w, lambda t: 0.25 * u(t) ** 2.5, ((1, 1.0), (2, 0.4)))
        out = [a + b + c for a, b, c in zip(rush, low, riser)]
        cut = n_of(0.012)
        for i in range(ns - cut, n):  # se corta en seco (12 ms) al entrar
            out[i] *= max(0.0, (ns - i) / cut)
        add_into(out, thump(n_of(0.22), 90.0, 55.0, 0.015, 0.09, 0.9), ns)
        add_into(out, burst(n_of(0.02), r, 0.002, 0.5, "hp", 3000.0), ns)
        add_into(out, modal(n_of(0.15), (2400.0, 3900.0), (0.3, 0.2), (0.03, 0.02)), ns)
        out = reverb(out, wet=0.12, room=0.7, damp=0.4, spread=23 * ch)
        chans.append(dc_block(out))
    pk_all = max(peak(c) for c in chans)
    return [fade_edges(scale(c, undb(-1.0) / pk_all), 0.002, 0.04) for c in chans]


def trans_hud_online():
    """Visor en línea (2D, 0.9 s): clic y arpegio suave al pasar a primera persona."""
    n = n_of(0.9)
    r = rng("hud")
    out = burst(n_of(0.02), r, 0.002, 0.4, "hp", 3000.0)
    out += [0.0] * (n - len(out))
    for t0, fr in ((0.03, 659.3), (0.11, 987.8), (0.19, 1318.5)):
        add_into(out, glide_sine(n_of(0.5), lambda t, fr=fr: fr, lambda t: 0.3 * (t / 0.004 if t < 0.004 else math.exp(-(t - 0.004) / 0.09))), n_of(t0))
    add_into(out, glide_sine(n_of(0.6), lambda t: 329.6, lambda t: 0.15 * math.sin(math.pi * min(1.0, t / 0.6))), n_of(0.03))
    out = reverb(out, wet=0.2, room=0.78, damp=0.35)
    return [finish_peak(out, -3.0, 0.001, 0.1)]


SOUNDS = [
    ("IR2_Amb_HangarTone_Loop.wav", amb_hangar_tone, True),
    ("IR2_Amb_Drip_Loop.wav", amb_drip, True),
    ("IR2_Amb_MachineFar_Loop.wav", amb_machine_far, True),
    ("IR2_Amb_ElecHum_Loop.wav", amb_elec_hum, True),
    ("IR2_Robot_Boot.wav", robot_boot, False),
    ("IR2_Robot_Heart_Loop.wav", robot_heart, True),
    ("IR2_Robot_Blip.wav", robot_blip, False),
    ("IR2_Robot_Scan.wav", robot_scan, False),
    ("IR2_Robot_StepWeight.wav", robot_step_weight, False),
    ("IR2_Capsule_Steam.wav", capsule_steam, False),
    ("IR2_Capsule_Clamp.wav", capsule_clamp, False),
    ("IR2_Capsule_Creak.wav", capsule_creak, False),
    ("IR2_Impact_SubBoom.wav", impact_sub_boom, False),
    ("IR2_Impact_PreSuck.wav", impact_pre_suck, False),
    ("IR2_Impact_GlassDebris.wav", impact_glass_debris, False),
    ("IR2_Reveal_Sting.wav", reveal_sting, False),
    ("IR2_Trans_Whoosh.wav", trans_whoosh, False),
    ("IR2_Trans_HudOnline.wav", trans_hud_online, False),
]


def main(argv):
    filters = [a.lower() for a in argv]
    rows = []
    t_all = time.time()
    for name, fn, loop in SOUNDS:
        if filters and not any(f in name.lower() for f in filters):
            continue
        t0 = time.time()
        chans = fn()
        info = write_wav(name, chans, name)
        info["loop"] = ("costura %.2fx" % loop_check(chans)) if loop else "one-shot"
        info["secs"] = time.time() - t0
        rows.append(info)
        print("%-30s %s %5.2f s  pico %6.1f dBFS  RMS %6.1f dBFS  %-16s %s  (%.1f s)" % (
            name, "est" if info["ch"] == 2 else "mono", info["dur"], info["peak"], info["rms"], info["loop"], info["sha"], info["secs"]))
    lines = ["IRON REQUIEM · synth_hangar_intro.py · %s" % time.strftime("%Y-%m-%d %H:%M:%S"),
             "salida %s · %d Hz · PCM 16 bits · semilla 0x%X · python %s" % (os.path.relpath(OUT_DIR, ROOT), SR, MASTER_SEED, sys.version.split()[0]),
             "costura = salto entre la última y la primera muestra / salto medio entre muestras (≈1 o menos: sin clic)", ""]
    for i in rows:
        lines.append("%-30s %s %5.2f s  pico %6.1f dBFS  RMS %6.1f dBFS  %-16s sha256 %s" % (
            i["name"], "est " if i["ch"] == 2 else "mono", i["dur"], i["peak"], i["rms"], i["loop"], i["sha"]))
    lines.append("")
    lines.append("total %.1f s" % (time.time() - t_all))
    if not filters:
        os.makedirs(os.path.dirname(REPORT), exist_ok=True)
        with open(REPORT, "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        print("informe ->", os.path.relpath(REPORT, ROOT))


if __name__ == "__main__":
    main(sys.argv[1:])
