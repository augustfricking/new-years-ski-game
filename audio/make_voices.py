"""Generates the cutscene voice lines with Chatterbox TTS (MIT licence, open weights, runs locally on GPU).

Pipeline
  1. audio/make_refs.py (Kokoro, old venv) writes one ~10 s reference clip per character to audio/refs/.
     They only fix each voice's identity; Chatterbox adds the emotion.
  2. This script reads the lines from CutsceneScript.cs (single source of truth), generates each line with
     per-line delivery settings (DELIVERY table below), picks the best of several seeds (length must be
     plausible for the text, so hallucinated extra audio is rejected), then post-processes with ffmpeg:
     optional tempo/pitch shaping, silence trim both ends, loudnorm -17 LUFS, 44.1 kHz mono Vorbis q4.

Usage (from the repo root):
    tools/tts/.venv-expressive/Scripts/python audio/make_voices.py [--only 3,7] [--seeds 4] [--ffmpeg <path>]
Raw wavs go to audio/raw/ (gitignored), the .ogg files to SkiGame/Assets/_Game/Resources/Voices.
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
# Keep the model download inside the repo's tools folder (ignored by git).
os.environ.setdefault("HF_HOME", str(ROOT / "tools/tts/models-chatterbox"))

SCRIPT = ROOT / "SkiGame/Assets/_Game/Scripts/Game/Scenes/CutsceneScript.cs"
REFS = ROOT / "audio/refs"
RAW = ROOT / "audio/raw"
OUT = ROOT / "SkiGame/Assets/_Game/Resources/Voices"
DEFAULT_FFMPEG = (r"C:\Users\augus\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
                  r"\ffmpeg-9.0.2-full_build\bin\ffmpeg.exe")

# Delivery per line (1-based index in the script):
#   text   - what the model is fed (same spoken words; punctuation tweaked for delivery)
#   exag   - Chatterbox exaggeration (0.25 flat ... 1.0 very emotional)
#   cfg    - cfg_weight (lower = faster, more fluid pacing; higher = slower, more deliberate)
#   temp   - sampling temperature (higher = more varied intonation)
#   tempo  - ffmpeg atempo applied afterwards (1.0 = unchanged)
#   pitch  - semitones, via asetrate/atempo (small values only; 0 = unchanged)
DELIVERY = {
    1:  dict(exag=0.70, cfg=0.40, temp=0.80, tempo=1.00, pitch=0.0),   # L bright, rallying
    2:  dict(exag=0.60, cfg=0.45, temp=0.80, tempo=1.00, pitch=0.0),   # E earnest, proud nerd
    3:  dict(exag=0.65, cfg=0.35, temp=0.80, tempo=1.00, pitch=0.0,    # J cocky tease
             text="Carving? You ski like you're filling in a tax form."),
    4:  dict(exag=0.40, cfg=0.55, temp=0.70, tempo=0.97, pitch=0.0,    # M deadpan
             text="Says the guy who sat down four times before lunch."),
    5:  dict(exag=0.75, cfg=0.40, temp=0.80, tempo=1.00, pitch=0.0),   # L challenge, grinning
    6:  dict(exag=0.50, cfg=0.50, temp=0.75, tempo=1.0, pitch=0.0,     # M sly, steering the topic
             text="Speaking of after-ski... Erik. Who are you kissing at midnight?"),
    7:  dict(exag=0.95, cfg=0.30, temp=0.85, tempo=1.05, pitch=0.5,    # E flustered
             text="What? Nobody! Why would you..."),
    8:  dict(exag=0.80, cfg=0.55, temp=0.75, tempo=0.96, pitch=0.0,    # L hurt and pointed
             text="Because last New Year you kissed me. Then you didn't text for three weeks."),
    9:  dict(exag=0.80, cfg=0.40, temp=0.80, tempo=1.00, pitch=-0.3,   # J amused disbelief
             text="Three weeks? Dude."),
    10: dict(exag=0.95, cfg=0.30, temp=0.85, tempo=1.05, pitch=0.5,    # E defensive
             text="I dropped my phone in the hot tub!"),
    11: dict(exag=0.45, cfg=0.55, temp=0.70, tempo=0.95, pitch=0.0,    # M dry knife
             text="Funny. He told me it fell off this lift."),
    12: dict(exag=0.90, cfg=0.30, temp=0.85, tempo=1.05, pitch=0.3,    # J alarmed
             text="Uh, guys? Where did the mountain go?"),
    13: dict(exag=1.00, cfg=0.25, temp=0.90, tempo=1.10, pitch=0.8,    # L panicked
             text="I can't even see my own skis!"),
    14: dict(exag=0.70, cfg=0.45, temp=0.80, tempo=1.0, pitch=0.3,     # E forced calm
             text="It's just a little snow. Everybody stay calm."),
    15: dict(exag=0.55, cfg=0.45, temp=0.75, tempo=1.03, pitch=0.0,    # M strained, dry
             text="You are crushing my arm, Erik."),
    16: dict(exag=1.00, cfg=0.25, temp=0.90, tempo=1.10, pitch=0.3,    # J shouting relief
             text="Top station! Bar up, bar up!"),
    17: dict(exag=0.65, cfg=0.40, temp=0.80, tempo=1.00, pitch=0.0,    # L upbeat, catching breath
             text="Okay. Maps out. One last run... then we party."),
}
REF_FILES = {"J": "J.wav", "M": "M.wav", "E": "E.wav", "L": "L.wav"}
WORDS_PER_SEC = 2.9  # natural conversational pace, used to reject truncated/hallucinated takes


def read_lines():
    text = SCRIPT.read_text(encoding="utf-8")
    beats = re.findall(r'new Beat \{ Who = (\w),.*?Text = "((?:[^"\\]|\\.)*)"', text)
    if not beats:
        sys.exit("No beats found in " + str(SCRIPT))
    return [(who, line.replace('\\"', '"')) for who, line in beats]


def silence_ends(wav, ffmpeg):
    """Speech duration after trimming both ends (what the player will hear)."""
    r = subprocess.run([ffmpeg, "-hide_banner", "-i", str(wav), "-af",
                        "silenceremove=start_periods=1:start_threshold=-45dB,areverse,"
                        "silenceremove=start_periods=1:start_threshold=-45dB,areverse,ametadata=print",
                        "-f", "null", "-"], capture_output=True, text=True)
    m = re.findall(r"time=(\d+):(\d+):([\d.]+)", r.stderr)
    h, mi, s = m[-1]
    return int(h) * 3600 + int(mi) * 60 + float(s)


def encode(wav, ogg, d, ffmpeg):
    filters = []
    if abs(d["pitch"]) > 0.01:
        f = 2 ** (d["pitch"] / 12)
        filters.append(f"asetrate=24000*{f:.5f},aresample=24000,atempo={1 / f:.5f}")  # pitch only
    if abs(d["tempo"] - 1.0) > 0.005:
        filters.append(f"atempo={d['tempo']:.4f}")
    filters += ["silenceremove=start_periods=1:start_threshold=-45dB",
                "areverse", "silenceremove=start_periods=1:start_threshold=-45dB", "areverse",
                "loudnorm=I=-17:TP=-1.5"]
    subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-i", str(wav), "-af", ",".join(filters),
                    "-ar", "44100", "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", str(ogg)], check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or DEFAULT_FFMPEG)
    ap.add_argument("--seeds", type=int, default=4, help="takes per line; the most plausible one is kept")
    ap.add_argument("--only", default="", help="comma-separated line numbers to regenerate")
    args = ap.parse_args()
    only = {int(x) for x in args.only.split(",") if x}

    import torch
    import torchaudio as ta
    from chatterbox.tts import ChatterboxTTS

    RAW.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    model = ChatterboxTTS.from_pretrained(device="cuda" if torch.cuda.is_available() else "cpu")
    lines = read_lines()
    total = 0.0
    for i, (who, line) in enumerate(lines, start=1):
        d = DELIVERY[i]
        if only and i not in only:
            continue
        text = d.get("text", line)
        expected = max(0.9, len(line.split()) / WORDS_PER_SEC)
        best = None
        for seed in range(args.seeds):
            torch.manual_seed(1000 * i + seed)
            wav = model.generate(text, audio_prompt_path=str(REFS / REF_FILES[who]),
                                 exaggeration=d["exag"], cfg_weight=d["cfg"], temperature=d["temp"])
            cand = RAW / f"cut_{i:02d}_s{seed}.wav"
            ta.save(str(cand), wav, model.sr)
            dur = silence_ends(cand, args.ffmpeg)
            score = abs(dur * d["tempo"] - expected) / expected
            print(f"   line {i:02d} seed {seed}: {dur:4.1f}s (expected ~{expected:3.1f}s) score {score:.2f}")
            if best is None or score < best[0]:
                best = (score, cand, dur)
        shutil.copyfile(best[1], RAW / f"cut_{i:02d}.wav")
        encode(RAW / f"cut_{i:02d}.wav", OUT / f"cut_{i:02d}.ogg", d, args.ffmpeg)
        total += best[2] / d["tempo"]
        print(f"{i:02d} {who} {best[2] / d['tempo']:4.1f}s  {line}")
    print(f"total speech: {total:.1f}s")


if __name__ == "__main__":
    main()
