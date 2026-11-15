"""Step 1 of the voice pipeline: ~10 s Kokoro reference clip per character (voice identity for Chatterbox).
Run with the original venv: tools/tts/.venv/Scripts/python audio/make_refs.py"""
import sys
from pathlib import Path
import numpy as np, soundfile as sf
from kokoro_onnx import Kokoro

ROOT = Path(__file__).resolve().parent.parent
REFS = ROOT / "audio/refs"
M = ROOT / "tools/tts/models"
# who: (kokoro voice or blend, speed, reference text)
CHARS = {
    "J": ("am_puck", 1.0, "Yeah, I mean, it's not that hard, you just point it downhill and hold on. Honestly, I don't really get why everybody makes such a big deal out of it. Relax, it's just a mountain."),
    "M": ("bf_emma", 0.98, "Oh, absolutely, that sounds like a brilliant plan. I'm sure it'll go exactly the way you think it will. Do tell me more, I'm fascinated, truly."),
    "E": ("am_michael", 1.0, "Well, technically, the wax works best when the snow is cold and dry, so I checked the temperature this morning. It's actually quite interesting once you read about it."),
    "L": ("af_heart", 1.05, "Okay, everyone, listen up! We've got one more run today, and I want it to be a good one. Grab your poles, check your goggles, and let's go have some fun!"),
}
k = Kokoro(str(M / "kokoro-v1.0.onnx"), str(M / "voices-v1.0.bin"))
REFS.mkdir(parents=True, exist_ok=True)
for who, (v, sp, t) in CHARS.items():
    s, r = k.create(t, voice=v, speed=sp, lang="en-us")
    sf.write(REFS / f"{who}.wav", s, r)
    print(who, v, len(s) / r)
