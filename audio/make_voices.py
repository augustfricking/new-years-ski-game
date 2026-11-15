"""Generates the cutscene voice lines with Kokoro TTS (open weights, runs locally).

Reads the lines straight from CutsceneScript.cs, so the script file stays the single source of
truth. Writes audio/raw/cut_XX.wav and, through ffmpeg, the .ogg files Unity loads from
SkiGame/Assets/_Game/Resources/Voices.

Usage (from the repo root):
    tools/tts/.venv/Scripts/python audio/make_voices.py [--ffmpeg <path to ffmpeg.exe>]
"""
import argparse
import re
import shutil
import subprocess
import sys
from pathlib import Path

import soundfile as sf
from kokoro_onnx import Kokoro

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "SkiGame/Assets/_Game/Scripts/Game/Scenes/CutsceneScript.cs"
MODELS = ROOT / "tools/tts/models"
RAW = ROOT / "audio/raw"
OUT = ROOT / "SkiGame/Assets/_Game/Resources/Voices"

# One voice per friend: (Kokoro voice, speed).
VOICES = {
    "J": ("am_puck", 1.08),     # Jonte: cocky snowboarder
    "M": ("bf_emma", 1.04),     # Maja: dry and sharp
    "E": ("am_michael", 1.0),   # Erik: earnest
    "L": ("af_heart", 1.06),    # Linn: bright, takes charge
}


def read_lines():
    text = SCRIPT.read_text(encoding="utf-8")
    beats = re.findall(r'new Beat \{ Who = (\w),.*?Text = "((?:[^"\\]|\\.)*)"', text)
    if not beats:
        sys.exit("No beats found in " + str(SCRIPT))
    return [(who, line.replace('\\"', '"')) for who, line in beats]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or "ffmpeg")
    args = ap.parse_args()

    RAW.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    kokoro = Kokoro(str(MODELS / "kokoro-v1.0.onnx"), str(MODELS / "voices-v1.0.bin"))
    total = 0.0
    for i, (who, line) in enumerate(read_lines(), start=1):
        voice, speed = VOICES[who]
        samples, rate = kokoro.create(line, voice=voice, speed=speed, lang="en-us")
        wav = RAW / f"cut_{i:02d}.wav"
        sf.write(wav, samples, rate)
        ogg = OUT / f"cut_{i:02d}.ogg"
        # Trim silence at both ends, normalise loudness, encode as Vorbis.
        subprocess.run(
            [args.ffmpeg, "-y", "-loglevel", "error", "-i", str(wav),
             "-af", "silenceremove=start_periods=1:start_threshold=-45dB,areverse,"
                    "silenceremove=start_periods=1:start_threshold=-45dB,areverse,loudnorm=I=-17:TP=-1.5",
             "-ar", "44100", "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", str(ogg)],
            check=True)
        seconds = len(samples) / rate
        total += seconds
        print(f"{i:02d} {who} {seconds:4.1f}s  {line}")
    print(f"total speech: {total:.1f}s")


if __name__ == "__main__":
    main()
