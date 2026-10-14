#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy>=1.26", "soundfile>=0.12"]
# ///
"""Generează sunetele jocului prin ElevenLabs și le pregătește pentru Unity.

Rulare (din rădăcina repo-ului):
    uv run tools/audio/generate.py                  # generează ce lipsește, procesează, QA, manifest
    uv run tools/audio/generate.py --dry-run        # planul și costul estimat, fără apeluri care costă
    uv run tools/audio/generate.py --only punch,heal
    uv run tools/audio/generate.py --force punch    # regenerează (costă credite; brutul vechi merge în raw/rejected/)
    uv run tools/audio/generate.py --reprocess      # refă post-procesarea din raw/, fără API
    uv run tools/audio/generate.py --qa             # doar QA + manifest pe ce există

Idempotent: un sunet care are deja brutul (raw/<id>.mp3) sau .ogg-ul final nu se mai generează,
deci o clonă fără raw/ nu cheltuie credite din nou.
Cheia API se citește din mediu sau din ~/.secrets/elevenlabs.env și nu se afișează niciodată.
Lista de sunete: tools/audio/sounds.json. Rezultat: Assets/_Game/Resources/Audio/{Sfx,Music}/<id>.ogg
și Assets/_Game/Resources/Audio/audio_manifest.json.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / "tools" / "audio"
SPEC_PATH = TOOLS / "sounds.json"
RAW_DIR = TOOLS / "raw"
LEDGER_PATH = RAW_DIR / "ledger.json"
QA_PATH = TOOLS / "qa_report.json"
OUT_DIR = ROOT / "Assets" / "_Game" / "Resources" / "Audio"
MANIFEST_PATH = OUT_DIR / "audio_manifest.json"

API = "https://api.elevenlabs.io"
SECRETS_FILE = Path.home() / ".secrets" / "elevenlabs.env"
SR = 44100
# Prețuri pentru estimare și buget, măsurate pe cont la 2026-10-14 (documentația spune 40/s la SFX,
# contul a fost taxat ~11-12/s; muzica ~14/s). Rotunjim în sus ca plafonul să rămână prudent.
SFX_CREDITS_PER_SEC = 14
MUSIC_CREDITS_PER_SEC = 15
# Plafon de vârf după procesare: -2 dBFS lasă loc pentru depășirile codecului Vorbis (true peak ≤ -1 dBTP).
LIMIT_LIN = 10 ** (-2.0 / 20)
# Cât are voie limitatorul să taie din vârf; peste asta sunetul s-ar turti (la bucle, picăturile de apă).
SFX_MAX_LIMITING_DB = 6.0
LOOP_MAX_LIMITING_DB = 4.0
OGG_QUALITY = {"sfx": 0.4, "loop": 0.5, "music": 0.5}  # compression_level libsndfile: 0 = calitate maximă


# ---------------------------------------------------------------- utilitare

def db(v: float) -> float:
    return 10 ** (v / 20)


def to_db(a: float) -> float:
    return 20 * math.log10(max(a, 1e-12))


def log(msg: str) -> None:
    print(msg, flush=True)


def load_spec() -> tuple[dict, list[dict]]:
    data = json.loads(SPEC_PATH.read_text())
    defaults = data.get("defaults", {})
    items = []
    for raw in data["sounds"]:
        item = dict(defaults.get(raw["type"], {}))
        item.update(raw)
        items.append(item)
    ids = [i["id"] for i in items]
    dupes = {i for i in ids if ids.count(i) > 1}
    if dupes:
        sys.exit(f"ID-uri duplicate în sounds.json: {sorted(dupes)}")
    return data, items


def out_path(item: dict) -> Path:
    # Ambianțele stau lângă muzică: sunt paturi lungi care se redau în buclă, nu efecte scurte.
    folder = "Sfx" if item["type"] == "sfx" else "Music"
    return OUT_DIR / folder / f"{item['id']}.ogg"


def raw_path(item: dict) -> Path:
    return RAW_DIR / f"{item['id']}.mp3"


def estimate_cost(item: dict) -> int:
    rate = MUSIC_CREDITS_PER_SEC if item["type"] == "music" else SFX_CREDITS_PER_SEC
    return math.ceil(rate * float(item["duration"]))


# ---------------------------------------------------------------- API ElevenLabs

class ApiError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(f"HTTP {status}: {message}")
        self.status = status
        self.message = message


def load_api_key() -> str:
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not key and SECRETS_FILE.exists():
        for line in SECRETS_FILE.read_text().splitlines():
            line = line.strip()
            if line.startswith("export "):
                line = line[len("export "):]
            if line.startswith("ELEVENLABS_API_KEY="):
                key = line.split("=", 1)[1].strip().strip('"').strip("'")
    if not key:
        sys.exit("Lipsește ELEVENLABS_API_KEY (mediu sau ~/.secrets/elevenlabs.env).")
    return key


def api_call(key: str, method: str, path: str, body: dict | None = None,
             query: dict | None = None, timeout: float = 120) -> bytes:
    url = API + path + ("?" + urllib.parse.urlencode(query) if query else "")
    data = json.dumps(body).encode() if body is not None else None
    headers = {"xi-api-key": key, "Accept": "*/*"}
    if data is not None:
        headers["Content-Type"] = "application/json"
    delays = [5, 15, 40]
    for attempt in range(len(delays) + 1):
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                return resp.read()
        except urllib.error.HTTPError as e:
            text = e.read().decode("utf-8", "replace")
            try:
                detail = json.loads(text).get("detail", text)
            except json.JSONDecodeError:
                detail = text
            # 429 / 5xx sunt trecătoare: reîncercăm cu pauze crescătoare.
            if e.code in (429, 500, 502, 503, 504) and attempt < len(delays):
                log(f"    HTTP {e.code}, reîncerc în {delays[attempt]}s")
                time.sleep(delays[attempt])
                continue
            raise ApiError(e.code, json.dumps(detail)[:500]) from None
        except (urllib.error.URLError, TimeoutError) as e:
            if attempt < len(delays):
                log(f"    eroare de rețea ({e}), reîncerc în {delays[attempt]}s")
                time.sleep(delays[attempt])
                continue
            raise
    raise RuntimeError("unreachable")


def remaining_credits(key: str) -> tuple[int, int, int]:
    sub = json.loads(api_call(key, "GET", "/v1/user/subscription"))
    used, limit = int(sub["character_count"]), int(sub["character_limit"])
    return limit - used, used, limit


def request_sfx(key: str, item: dict, prompt: str | None = None, loop: bool | None = None,
                duration: float | None = None) -> bytes:
    body = {
        "text": prompt or item["prompt"],
        "duration_seconds": float(duration or item["duration"]),
        "prompt_influence": float(item.get("prompt_influence", 0.4)),
        "model_id": "eleven_text_to_sound_v2",
    }
    if loop is None:
        loop = item["type"] == "loop"
    if loop:
        body["loop"] = True
    return api_call(key, "POST", "/v1/sound-generation", body,
                    {"output_format": "mp3_44100_192"}, timeout=180)


def request_music(key: str, item: dict) -> bytes:
    body = {
        "prompt": item["prompt"],
        "music_length_ms": int(float(item["duration"]) * 1000),
        "model_id": item.get("model_id", "music_v2_5"),
        "force_instrumental": bool(item.get("force_instrumental", False)),
    }
    return api_call(key, "POST", "/v1/music", body,
                    {"output_format": "mp3_44100_192"}, timeout=900)


# ---------------------------------------------------------------- bugetul (plafon pe toată sarcina, nu pe rulare)

def load_ledger() -> dict | None:
    if LEDGER_PATH.exists():
        return json.loads(LEDGER_PATH.read_text())
    return None


def save_ledger(ledger: dict) -> None:
    RAW_DIR.mkdir(parents=True, exist_ok=True)
    LEDGER_PATH.write_text(json.dumps(ledger, indent=2) + "\n")


# ---------------------------------------------------------------- audio: citire, scriere, măsurători

def ffmpeg_bin() -> str:
    exe = shutil.which("ffmpeg")
    if not exe:
        sys.exit("ffmpeg nu e instalat.")
    return exe


def channels_of(path: Path) -> int:
    out = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries",
                          "stream=channels", "-of", "csv=p=0", str(path)],
                         capture_output=True, text=True, check=True).stdout.strip()
    return int(out.split(",")[0])


def decode(path: Path) -> np.ndarray:
    """Fișier audio → float32 [eșantioane, canale] la 44.1 kHz."""
    ch = channels_of(path)
    raw = subprocess.run([ffmpeg_bin(), "-v", "error", "-i", str(path), "-f", "f32le",
                          "-acodec", "pcm_f32le", "-ar", str(SR), "-ac", str(ch), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype="<f4").reshape(-1, ch).copy()


def ffmpeg_filter(x: np.ndarray, af: str) -> np.ndarray:
    ch = x.shape[1]
    with tempfile.TemporaryDirectory() as tmp:
        src = Path(tmp) / "in.wav"
        sf.write(src, x, SR, subtype="FLOAT")
        raw = subprocess.run([ffmpeg_bin(), "-v", "error", "-i", str(src), "-af", af, "-f", "f32le",
                              "-acodec", "pcm_f32le", "-ar", str(SR), "-ac", str(ch), "-"],
                             capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype="<f4").reshape(-1, ch).copy()


def ebur128(x: np.ndarray) -> tuple[float, float]:
    """(loudness integrată LUFS, true peak dBTP). Sub 0.4 s ebur128 n-are niciun bloc, așa că
    sunetele foarte scurte se completează cu liniște până la 0.4 s."""
    min_len = int(0.4 * SR) + 1
    if len(x) < min_len:
        x = np.concatenate([x, np.zeros((min_len - len(x), x.shape[1]), dtype=x.dtype)])
    with tempfile.TemporaryDirectory() as tmp:
        src = Path(tmp) / "in.wav"
        sf.write(src, x, SR, subtype="FLOAT")
        err = subprocess.run([ffmpeg_bin(), "-nostats", "-hide_banner", "-i", str(src), "-af",
                              "ebur128=peak=true:framelog=quiet", "-f", "null", "-"],
                             capture_output=True, text=True).stderr
    i_vals = re.findall(r"I:\s+(-?[\d.]+|-inf) LUFS", err)
    p_vals = re.findall(r"Peak:\s+(-?[\d.]+|-inf) dBFS", err)
    lufs = float(i_vals[-1]) if i_vals and i_vals[-1] != "-inf" else -120.0
    tp = float(p_vals[-1]) if p_vals and p_vals[-1] != "-inf" else -120.0
    return lufs, tp


def limit(x: np.ndarray, gain_db: float) -> np.ndarray:
    # level=0: fără auto-nivel (altfel alimiter ar împinge totul spre 0 dB); latency=1 compensează lookahead-ul.
    return ffmpeg_filter(x, f"volume={gain_db:.3f}dB,"
                            f"alimiter=limit={LIMIT_LIN:.4f}:attack=1:release=40:level=0:latency=1")


def write_ogg(x: np.ndarray, path: Path, kind: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".tmp.ogg")
    x = np.ascontiguousarray(np.clip(x, -1.0, 1.0), dtype=np.float32)
    # libsndfile cade (segfault) la scrierea Vorbis dintr-o bucată mare; scriem în blocuri mici.
    with sf.SoundFile(tmp, "w", SR, x.shape[1], format="OGG", subtype="VORBIS",
                      compression_level=OGG_QUALITY[kind]) as f:
        for i in range(0, len(x), 16384):
            f.write(x[i:i + 16384])
    tmp.replace(path)


def block_peaks(x: np.ndarray, block: int) -> np.ndarray:
    mono = np.abs(x).max(axis=1)
    n = len(mono) // block
    if n == 0:
        return np.array([mono.max() if len(mono) else 0.0])
    return mono[: n * block].reshape(n, block).max(axis=1)


def clip_runs(x: np.ndarray) -> int:
    """Câte porțiuni de ≥3 eșantioane consecutive stau lipite de 0 dBFS (semn de clipping în brut)."""
    hot = (np.abs(x) >= 0.995).any(axis=1).astype(np.int8)
    if not hot.any():
        return 0
    edges = np.diff(np.concatenate([[0], hot, [0]]))
    starts, ends = np.nonzero(edges == 1)[0], np.nonzero(edges == -1)[0]
    return int(((ends - starts) >= 3).sum())


# ---------------------------------------------------------------- procesare SFX

# ElevenLabs lasă un mic „clic” în ultimele ~10 ms ale fiecărui fișier (l-am găsit în toate brutele);
# îl tăiem înainte de orice analiză, altfel coada pare să țină până la capăt și strică buclele.
END_ARTIFACT_S = 0.015


def fade(x: np.ndarray, fade_in: float, fade_out: float) -> np.ndarray:
    y = x.copy()
    n_in, n_out = min(int(fade_in * SR), len(y)), min(int(fade_out * SR), len(y))
    if n_in > 0:
        y[:n_in] *= np.linspace(0, 1, n_in, dtype=np.float32)[:, None]
    if n_out > 0:
        y[-n_out:] *= np.linspace(1, 0, n_out, dtype=np.float32)[:, None]
    return y


def gain_for(y: np.ndarray, target: float, max_limiting_db: float, info: dict) -> float:
    """Câștigul spre ținta LUFS, plafonat ca limitatorul să nu taie mai mult de max_limiting_db din vârf.
    Sunetele foarte vârfoase (picături, lovituri scurte) ies astfel puțin sub țintă, dar nu turtite."""
    lufs, _ = ebur128(y)
    gain = target - lufs
    max_gain = (to_db(LIMIT_LIN) - to_db(float(np.abs(y).max()))) + max_limiting_db
    if gain > max_gain:
        info["gain_capped_db"] = round(gain - max_gain, 1)
        gain = max_gain
    info["gain_db"] = round(gain, 2)
    return gain


def process_sfx(x: np.ndarray, item: dict) -> tuple[np.ndarray, dict]:
    info: dict = {}
    x = x[: max(1, len(x) - int(END_ARTIFACT_S * SR))]
    mono = np.abs(x).max(axis=1)
    peak = float(mono.max()) if len(mono) else 0.0
    if peak < db(-60):
        return x, {"note": "brut aproape mut"}
    # Pragurile sunt relative la vârf: un sunet slab nu pierde atacul, unul tare nu păstrează zgomotul de fond.
    start_thr = max(peak * db(-40), db(-60))
    start = int(np.argmax(mono > start_thr))
    start = max(0, start - int(0.004 * SR))
    end_thr = max(peak * db(-45), db(-66))
    above = np.nonzero(mono > end_thr)[0]
    end = min(len(x), int(above[-1]) + int(0.02 * SR))
    y = x[start:end]
    info["trimmed_lead_s"] = round(start / SR, 3)
    max_len = float(item.get("max_length", item["duration"]))
    fade_out = 0.015
    if len(y) > max_len * SR:
        y = y[: int(max_len * SR)]
        info["truncated_to_s"] = max_len
    # Dacă sunetul e încă tare la final (modelul l-a tăiat la durata cerută), un fade scurt ar lăsa
    # o retezătură; îl închidem cu un fade proporțional cu lungimea.
    tail = np.abs(y[-int(0.05 * SR):]).max() if len(y) > int(0.05 * SR) else 0.0
    if tail > float(np.abs(y).max()) * db(-25):
        fade_out = min(0.3, 0.2 * len(y) / SR)
        info["long_fade_s"] = round(fade_out, 2)
    y = fade(y, 0.002, fade_out)
    gain = gain_for(y, float(item.get("target_lufs", -16)), SFX_MAX_LIMITING_DB, info)
    return limit(y, gain), info


# ---------------------------------------------------------------- procesare muzică și bucle

def frame_features(mono: np.ndarray, hop: int = 512, n_fft: int = 2048, bands: int = 24) -> np.ndarray:
    """Pe fiecare cadru: energia pe 24 de benzi logaritmice (timbru + armonie grosieră) și fluxul
    spectral (atacuri, adică ritmul). Normalizate, ca produsul scalar să fie o similaritate cosinus."""
    n = 1 + (len(mono) - n_fft) // hop
    win = np.hanning(n_fft).astype(np.float32)
    freqs = np.fft.rfftfreq(n_fft, 1 / SR)
    edges = np.geomspace(40, 16000, bands + 1)
    band_idx = np.clip(np.digitize(freqs, edges) - 1, -1, bands)
    energies = np.zeros((n, bands), dtype=np.float32)
    for c0 in range(0, n, 1024):  # pe bucăți, ca să nu ținem tot STFT-ul în memorie
        c1 = min(n, c0 + 1024)
        idx = np.arange(n_fft)[None, :] + hop * np.arange(c0, c1)[:, None]
        spec = np.abs(np.fft.rfft(mono[idx] * win, axis=1)) ** 2
        for b in range(bands):
            energies[c0:c1, b] = spec[:, band_idx == b].sum(axis=1)
    logb = np.log10(energies + 1e-10)
    flux = np.maximum(0, np.diff(logb, axis=0, prepend=logb[:1])).sum(axis=1, keepdims=True)
    feats = np.concatenate([logb, 2.0 * flux], axis=1)  # ritmul cântărește dublu: tobele decalate se aud primele
    feats = (feats - feats.mean(axis=0)) / (feats.std(axis=0) + 1e-6)
    return feats / (np.linalg.norm(feats, axis=1, keepdims=True) + 1e-9)


def find_loop_region(x: np.ndarray, window_s: float = 4.0, head_s: float = 0.3,
                     tail_s: float = 15.0) -> tuple[int, int, float]:
    """Caută perechea (A, B) pentru care ce urmează după B seamănă cel mai bine cu ce urmează după A,
    pe window_s secunde (timbru, armonie, atacuri). Bucla e x[A..B]: la crossfade tobele din coadă
    cad peste tobele din cap, nu între ele. A poate sări peste un intro, B peste un final diferit;
    fiecare secundă aruncată costă puțin din scor, ca să nu pierdem muzică degeaba."""
    hop = 512
    mono = x.mean(axis=1).astype(np.float32)
    f = frame_features(mono, hop)
    n, w = len(f), int(window_s * SR / hop)
    a_max = int(head_s * n)
    b_min = max(a_max + w, n - w - int(tail_s * SR / hop))
    head, tail = f[: a_max + w], f[b_min:]
    s = head @ tail.T
    # Sume pe diagonale (a+t, b+t), t < w, prin sume cumulative pe diagonală.
    d = s.copy()
    for i in range(1, d.shape[0]):
        d[i, 1:] += d[i - 1, :-1]
    rows, cols = head.shape[0] - w + 1, tail.shape[0] - w + 1
    end = d[w - 1: w - 1 + rows, w - 1: w - 1 + cols]
    before = np.zeros_like(end)
    before[1:, 1:] = d[: rows - 1, : cols - 1]
    score = (end - before) / w
    a_idx = np.arange(rows)[:, None]
    b_idx = (b_min + np.arange(cols))[None, :]
    lost_s = (a_idx + (n - w - b_idx)) * hop / SR
    total = score - 0.008 * lost_s
    a, b = np.unravel_index(int(np.argmax(total)), total.shape)
    a_s, b_s = int(a) * hop, int(b_min + b) * hop
    # Rafinare la eșantion: aliniem forma de undă, ca crossfade-ul să nu anuleze faze.
    seg = int(0.05 * SR)
    ref = mono[a_s:a_s + seg]
    best_d, best_c = 0, -1e18
    for dd in range(-hop, hop + 1, 4):
        q = b_s + dd
        if q < 0 or q + seg > len(mono):
            continue
        c = float(np.dot(mono[q:q + seg], ref))
        if c > best_c:
            best_c, best_d = c, dd
    return a_s, b_s + best_d, float(score[a, b])


def crossfade_loop(x: np.ndarray, a: int, b: int, c: int) -> np.ndarray:
    """Bucla x[a..b]: x[a+c:b] urmat de coada x[b:b+c] topită (equal-power) în x[a:a+c].
    Ultimul eșantion e practic x[a+c-1], deci la reluare continuă exact cu x[a+c]: fără cusătură."""
    t = np.linspace(0, 1, c, dtype=np.float32)[:, None]
    fade_in, fade_out = np.sin(t * np.pi / 2), np.cos(t * np.pi / 2)
    mixed = x[b:b + c] * fade_out + x[a:a + c] * fade_in
    return np.concatenate([x[a + c:b], mixed])


def rms_db_series(x: np.ndarray, win_s: float = 0.4, hop_s: float = 0.1) -> np.ndarray:
    mono = x.mean(axis=1)
    win, hop = int(win_s * SR), int(hop_s * SR)
    if len(mono) < win:
        return np.array([to_db(float(np.sqrt(np.mean(mono ** 2))))])
    idx = range(0, len(mono) - win + 1, hop)
    return np.array([to_db(float(np.sqrt(np.mean(mono[i:i + win] ** 2)))) for i in idx])


def process_loop(x: np.ndarray, item: dict) -> tuple[np.ndarray, dict]:
    info: dict = {}
    c = int(float(item.get("crossfade", 2.0)) * SR)
    x = x[: len(x) - int(END_ARTIFACT_S * SR)]
    mono = np.abs(x).max(axis=1)
    peak = float(mono.max())
    if peak < db(-60):
        return x, {"note": "brut aproape mut"}
    if item["type"] == "music":
        # Liniștea de la capete ar deveni o gaură în buclă.
        thr = max(peak * db(-50), db(-66))
        above = np.nonzero(mono > thr)[0]
        x = x[int(above[0]): int(above[-1]) + 1]
        # Muzica generată se termină des cu un fade-out; tăiem acolo unde nivelul scade clar sub mediana piesei.
        series = rms_db_series(x)
        loud = np.nonzero(series >= float(np.median(series)) - 9)[0]
        cut = min(len(x), int((loud[-1] * 0.1 + 0.4) * SR))
        if cut >= 0.75 * len(x):
            info["fadeout_cut_s"] = round((len(x) - cut) / SR, 2)
            x = x[:cut]
        a, b, score = find_loop_region(x, window_s=max(4.0, c / SR))
        info.update(loop_match=round(score, 3), loop_start_s=round(a / SR, 2), loop_end_s=round(b / SR, 2))
    else:
        # Ambianțele vin cu loop=true din API: perioada lor naturală e lungimea întreagă, deci nu tăiem nimic.
        a, b = 0, len(x) - c
    y = crossfade_loop(x, a, b, c)
    info["loop_len_s"] = round(len(y) / SR, 2)
    gain = gain_for(y, float(item.get("target_lufs", -20)), LOOP_MAX_LIMITING_DB, info)
    return limit(y, gain), info


# ---------------------------------------------------------------- QA („ascultare” prin măsurători)

def spectral_centroid(x: np.ndarray) -> float:
    mono = x.mean(axis=1)
    if len(mono) < 1024:
        mono = np.pad(mono, (0, 1024 - len(mono)))
    n = 2048
    frames = [mono[i:i + n] for i in range(0, max(1, len(mono) - n + 1), n // 2)]
    frames = [np.pad(f, (0, n - len(f))) for f in frames]
    spec = np.abs(np.fft.rfft(np.array(frames) * np.hanning(n), axis=1)).mean(axis=0)
    freqs = np.fft.rfftfreq(n, 1 / SR)
    return float((spec * freqs).sum() / (spec.sum() + 1e-12))


def qa_item(item: dict) -> dict:
    path = out_path(item)
    x = decode(path)
    dur = len(x) / SR
    lufs, tp = ebur128(x)
    peak = float(np.abs(x).max()) if len(x) else 0.0
    blocks = block_peaks(x, int(0.01 * SR))
    pk_block = int(np.argmax(blocks))
    active = np.nonzero(blocks > peak * db(-20))[0]
    lead = np.nonzero(np.abs(x).max(axis=1) > db(-40))[0]
    rec = {
        "id": item["id"], "type": item["type"], "requested_s": float(item["duration"]),
        "duration_s": round(dur, 3), "lufs": round(lufs, 1), "true_peak_dbtp": round(tp, 1),
        "sample_peak_dbfs": round(to_db(peak), 1),
        "lead_silence_s": round(int(lead[0]) / SR, 3) if len(lead) else None,
        "time_to_peak_s": round(pk_block * 0.01, 2),
        "active_s": round((active[-1] - active[0] + 1) * 0.01, 2) if len(active) else 0.0,
        "centroid_hz": round(spectral_centroid(x)),
        "channels": int(x.shape[1]),
    }
    rp = raw_path(item)
    if rp.exists():
        raw = decode(rp)
        rec["raw_clip_runs"] = clip_runs(raw)
        # Raport vârf / zgomot de fond (percentila 10 a RMS-ului pe 10 ms): cât „curat” e brutul.
        b = int(0.01 * SR)
        m = len(raw) // b
        if m > 0:
            rms = np.sqrt((raw[: m * b].mean(axis=1).reshape(m, b) ** 2).mean(axis=1) + 1e-12)
            rec["raw_snr_db"] = round(to_db(float(np.abs(raw).max())) - float(np.percentile(20 * np.log10(rms), 10)), 1)
    meta_p = rp.with_suffix(".json")
    if meta_p.exists():
        meta = json.loads(meta_p.read_text())
        rec["credits_est"] = meta.get("credits_est", meta.get("credits"))
        rec.update({k: v for k, v in meta.get("process", {}).items()})
        if meta.get("fallback"):
            rec["fallback"] = True
    target = float(item.get("target_lufs", -16))
    flags = []
    if lufs < -45 or peak < db(-30):
        flags.append("SILENT")
    # Câteva vârfuri lipite (sub 10) într-o explozie nu se aud; peste atât brutul e clar saturat.
    if rec.get("raw_clip_runs", 0) > 10:
        flags.append("CLIPPED_RAW")
    # Brut foarte încet și zgomotos: după +25 dB câștig s-ar auzi zgomotul de fond.
    if rec.get("gain_db", 0) > 25 and rec.get("raw_snr_db", 99) < 45:
        flags.append("RAW_TOO_QUIET")
    if peak > db(-0.3):
        flags.append("CLIPPED_OUT")
    if item["type"] == "sfx":
        if dur > float(item.get("max_length", item["duration"])) + 0.3:
            flags.append("TOO_LONG")
        if dur < 0.06:
            flags.append("TOO_SHORT")
        if rec["lead_silence_s"] is not None and rec["lead_silence_s"] > 0.05:
            flags.append("LATE_ONSET")
    else:
        if dur < 0.5 * float(item["duration"]):
            flags.append("TOO_SHORT")
        if dur > 1.2 * float(item["duration"]) + 1:
            flags.append("TOO_LONG")
    # Sub țintă e o problemă doar dacă mai era loc de vârf (sunetele vârfoase stau intenționat sub țintă).
    if lufs > target + 3 or (lufs < target - 3 and peak < db(-6)):
        flags.append("LOUDNESS_OFF")
    rec["flags"] = flags
    return rec


# ---------------------------------------------------------------- pașii principali

def generate_missing(items: list[dict], key: str | None, args) -> list[str]:
    pending = [i for i in items if not raw_path(i).exists() and not out_path(i).exists()]
    if not pending:
        log("Nimic de generat: toate sunetele au brut sau .ogg.")
        return []
    est = sum(estimate_cost(i) for i in pending)
    log(f"De generat: {len(pending)} sunete, cost estimat ≈ {est} credite.")
    for i in pending:
        log(f"  {i['id']:<22} {i['type']:<6} {float(i['duration']):>6.1f}s  ≈{estimate_cost(i)}")
    if args.dry_run:
        return []
    assert key
    remaining, _, _ = remaining_credits(key)
    ledger = load_ledger()
    if ledger is None:
        # Plafonul se fixează o singură dată, la prima rulare: 35% din ce rămânea atunci.
        ledger = {"baseline_remaining": remaining, "cap": int(remaining * 0.35), "spent": 0,
                  "created": dt.datetime.now().isoformat(timespec="seconds")}
        save_ledger(ledger)
    cap = args.budget if args.budget is not None else ledger["cap"] - ledger["spent"]
    log(f"Credite rămase în cont: {remaining}. Buget disponibil pentru sarcină: {cap}.")
    if est > cap:
        sys.exit(f"Costul estimat ({est}) depășește bugetul ({cap}). Restrânge cu --only sau ridică --budget.")
    RAW_DIR.mkdir(parents=True, exist_ok=True)
    done, spent_run = [], 0
    for item in pending:
        if spent_run + estimate_cost(item) > cap:
            log(f"  STOP buget: {item['id']} ar depăși plafonul.")
            break
        fallback = False
        t0 = time.time()
        try:
            if item["type"] == "music":
                try:
                    data = request_music(key, item)
                except ApiError as e:
                    # Planul fără Music API: pat ambiental în buclă din endpoint-ul de efecte.
                    if e.status in (401, 402, 403) and item.get("fallback_prompt") and not args.no_music_fallback:
                        log(f"    Music API indisponibil ({e}); fac pat ambiental prin SFX.")
                        data = request_sfx(key, item, prompt=item["fallback_prompt"], loop=True, duration=30)
                        fallback = True
                    else:
                        raise
            else:
                data = request_sfx(key, item)
        except ApiError as e:
            log(f"  EROARE {item['id']}: {e}")
            continue
        # Facturarea ElevenLabs apare în abonament cu întârziere de minute, deci diferența înainte/după
        # un apel iese 0. Ledger-ul ține estimarea (prudentă); totalul real se vede în abonament la final.
        cost = estimate_cost(item) if not fallback else math.ceil(SFX_CREDITS_PER_SEC * 30)
        spent_run += cost
        ledger["spent"] += cost
        save_ledger(ledger)
        tmp = raw_path(item).with_suffix(".part")
        tmp.write_bytes(data)
        tmp.replace(raw_path(item))
        meta = {"id": item["id"], "type": item["type"], "prompt": item["fallback_prompt"] if fallback else item["prompt"],
                "duration": item["duration"], "prompt_influence": item.get("prompt_influence"),
                "model": item.get("model_id", "music_v2_5") if item["type"] == "music" and not fallback else "eleven_text_to_sound_v2",
                "fallback": fallback, "credits_est": cost,
                "generated_at": dt.datetime.now().isoformat(timespec="seconds")}
        raw_path(item).with_suffix(".json").write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n")
        log(f"  ok {item['id']:<22} {len(data) / 1024:7.1f} KB  {cost:>5} credite  {time.time() - t0:5.1f}s")
        done.append(item["id"])
    log(f"Cheltuit în rulare (estimat): {spent_run} credite; pe toată sarcina: {ledger['spent']} / {ledger['cap']}.")
    log(f"Abonamentul arată acum {remaining_credits(key)[0]} credite rămase (se actualizează cu întârziere).")
    return done


def process_items(items: list[dict], args, fresh: set[str]) -> None:
    for item in items:
        rp, op = raw_path(item), out_path(item)
        if not rp.exists():
            continue
        if op.exists() and not args.reprocess and item["id"] not in fresh:
            continue
        x = decode(rp)
        if item["type"] == "sfx":
            y, info = process_sfx(x, item)
        else:
            y, info = process_loop(x, item)
        write_ogg(y, op, item["type"])
        meta_p = rp.with_suffix(".json")
        meta = json.loads(meta_p.read_text()) if meta_p.exists() else {"id": item["id"]}
        meta["process"] = info
        meta_p.write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n")
        log(f"  procesat {item['id']:<22} {len(y) / SR:6.2f}s  {info}")


def write_manifest(items: list[dict], qa: dict[str, dict]) -> None:
    sounds = []
    for item in items:
        op = out_path(item)
        if not op.exists():
            continue
        rel = op.relative_to(OUT_DIR.parent).with_suffix("")  # relativ la Resources, fără extensie
        sounds.append({
            "id": item["id"],
            "path": rel.as_posix(),
            "type": item["type"],
            "category": item.get("category", item["type"]),
            "group": item.get("group", item["id"]),
            "volume": float(item.get("volume", 1.0)),
            "loop": item["type"] in ("loop", "music"),
            "pitchVariance": float(item.get("pitch_variance", 0.0)),
            "duration": qa.get(item["id"], {}).get("duration_s", 0.0),
        })
    manifest = {"version": 1, "generator": "tools/audio/generate.py", "sounds": sounds}
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n")
    log(f"Manifest: {MANIFEST_PATH.relative_to(ROOT)} ({len(sounds)} sunete).")


def run_qa(items: list[dict]) -> dict[str, dict]:
    results = {}
    log(f"\n{'id':<22}{'dur':>7}{'LUFS':>7}{'TP':>6}{'lead':>6}{'t_pk':>6}{'act':>6}{'centr':>7}  flags")
    for item in items:
        if not out_path(item).exists():
            continue
        rec = qa_item(item)
        results[item["id"]] = rec
        log(f"{rec['id']:<22}{rec['duration_s']:>7.2f}{rec['lufs']:>7.1f}{rec['true_peak_dbtp']:>6.1f}"
            f"{(rec['lead_silence_s'] or 0):>6.2f}{rec['time_to_peak_s']:>6.2f}{rec['active_s']:>6.2f}"
            f"{rec['centroid_hz']:>7}  {' '.join(rec['flags'])}")
    QA_PATH.write_text(json.dumps(results, indent=2) + "\n")
    flagged = {k: v["flags"] for k, v in results.items() if v["flags"]}
    log(f"QA: {len(results)} fișiere, {len(flagged)} cu probleme: {flagged}")
    return results


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", help="listă de id-uri separate prin virgulă")
    ap.add_argument("--force", help="id-uri de regenerat (brutul vechi se mută în raw/rejected/)")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--reprocess", action="store_true", help="refă procesarea din raw/ pentru selecție")
    ap.add_argument("--qa", action="store_true", help="doar QA + manifest")
    ap.add_argument("--budget", type=int, help="plafon de credite pentru rularea asta (implicit: restul din ledger)")
    ap.add_argument("--no-music-fallback", action="store_true")
    args = ap.parse_args()

    _, items = load_spec()
    all_items = items
    if args.only or args.force:
        wanted = set(filter(None, ((args.only or "") + "," + (args.force or "")).split(",")))
        unknown = wanted - {i["id"] for i in items}
        if unknown:
            sys.exit(f"ID-uri necunoscute: {sorted(unknown)}")
        items = [i for i in items if i["id"] in wanted]

    if args.force and not args.dry_run:
        stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
        for item in items:
            if item["id"] not in args.force.split(","):
                continue
            for p in (raw_path(item), raw_path(item).with_suffix(".json")):
                if p.exists():
                    dest = RAW_DIR / "rejected" / f"{item['id']}-{stamp}{p.suffix}"
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    p.replace(dest)
            if out_path(item).exists():
                out_path(item).unlink()

    fresh: set[str] = set()
    if not args.qa:
        key = None if args.dry_run else load_api_key()
        fresh = set(generate_missing(items, key, args))
        if args.dry_run:
            return
        process_items(items, args, fresh)
    qa = run_qa(all_items)
    write_manifest(all_items, qa)


if __name__ == "__main__":
    main()
