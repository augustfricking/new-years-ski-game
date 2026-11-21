"""The DJ's songs made with YuE2 (the ones the game plays; the owner picked them over ACE-Step).
Titles, lyrics and order come from make_songs.py.

YuE2 (m-a-p/YuE2-3B): code Apache 2.0; weights CC BY-NC 4.0 with a grant to individual creators to
use them and publish what they make (see tools/music/_yue2/MODEL_LICENSE). Credit: "Music made with
YuE2 (m-a-p/YuE2-3B)". Runs locally on an 8 GB GPU with --low-vram (slow).

Setup (once, from tools/music):
    git clone --depth 1 https://github.com/multimodal-art-projection/YuE _yue2
    cd _yue2 && python -m uv venv --python 3.12 .venv
    python -m uv pip install --python .venv/Scripts/python.exe torch==2.10.0 --index-url https://download.pytorch.org/whl/cu128
    python -m uv pip install --python .venv/Scripts/python.exe .

Run:     tools/music/_yue2/.venv/Scripts/python.exe tools/music/make_songs_yue2.py [song id ...] [--install]
Output:  tools/music/_work/yue2/<id>/ (audio.flac and the plan) and tools/music/_work/compare/<id>_yue2.mp3;
         with --install also SkiGame/Assets/StreamingAssets/Music/<id>.mp3
"""
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "_work", "yue2")
COMPARE = os.path.join(HERE, "_work", "compare")
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "SkiGame", "Assets", "StreamingAssets", "Music"))

spec = importlib.util.spec_from_file_location("make_songs", os.path.join(HERE, "make_songs.py"))
ms = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ms)

# YuE2 reads one free-text style: language, genre, the lead voice, instruments, tempo. Short and concrete.
STYLE = {
    "techno": "English, dark minimal Berlin techno, deep deadpan male spoken-word voice, robotic vocoder hook, "
              "pounding four-on-the-floor kick, acid 303 bassline, metallic percussion, warehouse reverb, 132 BPM",
    "edm": "English, festival big room EDM, powerful bright female lead vocal, supersaw leads, pumping kick, "
           "snare roll build-ups, euphoric drop, 128 BPM",
    "pop": "English, bubblegum pop, sweet airy female lead vocal, bouncy synth bass, handclaps, sleigh bells, "
           "glossy production, 116 BPM",
    "country": "English, honky tonk country, gritty twangy male baritone lead vocal, telecaster, banjo, fiddle, "
               "pedal steel, upright bass, train beat drums, 104 BPM",
    "hiphop": "English, hard trap hip hop, aggressive gravelly male rapper, booming 808 bass, rolling hi-hats, "
              "dark piano and choir samples, 88 BPM",
    "classical": "Italian and English, baroque opera buffa, operatic coloratura female soprano, fierce staccato "
                 "string orchestra, solo violin, harpsichord continuo, 96 BPM",
    "rock": "English, loud punk rock, raspy screaming male lead vocal, gang vocals, distorted guitars, "
            "fast driving drums, 150 BPM",
    "classicrock": "English, 1970s classic rock, warm raspy male tenor lead vocal, crunchy overdriven guitar riffs, "
                   "Hammond organ, bluesy guitar solo, bass guitar, steady rock drums, 120 BPM",
    "oldschool": "English, 1990s old school boom bap hip hop, laid-back confident male rapper, dusty vinyl drum break, "
                 "jazzy sampled piano, upright bass, turntable scratches, 92 BPM",
    "afterski": "English, Alpine apres-ski schlager party anthem, hoarse cheerful male party singer, crowd "
                "singalong, oom-pah beat, accordion, brass section, tuba, cowbells, 138 BPM",
}


def lyrics_for(raw):
    """ACE-Step's section tags to YuE2's: verses, choruses (hooks), a bridge; instrumental-only sections dropped."""
    out, cur, lines = [], None, []

    def flush():
        if cur and lines:
            out.append("[%s]\n%s" % (cur, "\n".join(lines)))
    for line in raw.split("\n"):
        m = re.match(r"\[([^\]]+)\]", line.strip())
        if m:
            flush()
            name = m.group(1).split("-")[0].strip().lower()
            cur = ("Chorus" if name in ("hook", "chorus", "pre-chorus") else "Bridge" if name in ("bridge", "breakdown")
                   else "Outro" if name == "outro" else "Intro" if name == "intro" else "Verse")
            lines = []
        elif line.strip():
            lines.append(line.strip())
    flush()
    return "\n\n".join(out)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    install = "--install" in sys.argv
    songs = [s for s in ms.SONGS if not args or s["id"] in args]
    os.makedirs(WORK, exist_ok=True)
    os.makedirs(COMPARE, exist_ok=True)
    from yue2 import YuE2Pipeline
    from yue2.protocol import Sampling
    pipe = YuE2Pipeline.from_pretrained("m-a-p/YuE2-3B", vae="m-a-p/YuE2-Vae", device="cuda", low_vram=True)
    try:
        for i, song in enumerate(songs):
            sid = song["id"]
            t0 = time.time()
            print("SONG", sid, flush=True)
            try:
                result = pipe(style=STYLE[sid], lyrics=lyrics_for(song["lyrics"]), cot="full", seed=4100 + i,
                              semantic_sampling=Sampling(max_tokens=6000))
            except Exception as e:                      # one failure must not stop the others
                print("FAILED", sid, repr(e)[:600], flush=True)
                continue
            d = os.path.join(WORK, sid)
            if os.path.exists(d):
                shutil.rmtree(d)
            result.save_artifacts(d)
            mp3 = os.path.join(COMPARE, sid + "_yue2.mp3")
            length = len(result.audio) / 48000
            # A song cut off at the token limit ends mid-phrase: fade its last seconds out.
            fade = ["-af", "afade=t=out:st=%.2f:d=6" % max(length - 6, 0)] if any(result.truncated.values()) else []
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", os.path.join(d, "audio.flac")] + fade + ["-b:a", "128k", mp3], check=True)
            if install:
                shutil.copyfile(mp3, os.path.join(OUT, sid + ".mp3"))
            print("DONE", sid, "%.0f s audio in %.0f s" % (len(result.audio) / 48000, time.time() - t0),
                  "truncated" if any(result.truncated.values()) else "", flush=True)
    finally:
        pipe.close()
    if install:
        # The game's list: every song that has a file, in this order.
        meta = [dict(id=s["id"], title=s["title"], style=s["style"]) for s in ms.SONGS if os.path.exists(os.path.join(OUT, s["id"] + ".mp3"))]
        with open(os.path.join(OUT, "songs.json"), "w", encoding="utf-8") as f:
            json.dump({"songs": meta}, f, indent=1)
        print("SONGS", len(meta), flush=True)


if __name__ == "__main__":
    main()
