"""The game's background theme: a noir-funk instrumental made with YuE2's instrumental workflow, then
cut into a seamless loop (make_loops_yue2.find_loop / make_loop).

YuE2 writes the score; every note of its vocal line moves to the instrument part (the official
yue2-music skill's converter, _yue2/skills/yue2-music/instrumental/scripts/instrumentalize.py), so
nothing is left for a voice to sing; YuE2 then renders that score with only section tags for lyrics
and "no vocals, no singing, no choir, no spoken words" in the style. Low-VRAM mode for an 8 GB GPU.

Run:     tools/music/_yue2/.venv/Scripts/python.exe tools/music/make_theme_yue2.py [--takes 3] [--install N]
Output:  tools/music/_work/theme/take<N>/ (score, audio) and take<N>_loop.mp3 / take<N>_preview3x.mp3;
         --install N copies take N's loop to SkiGame/Assets/StreamingAssets/Music/theme.mp3 (what the game plays)
Credit and licence: see make_songs_yue2.py.
"""
import os

import shutil
import subprocess
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "_work", "theme")
GAME = os.path.abspath(os.path.join(HERE, "..", "..", "SkiGame", "Assets", "StreamingAssets", "Music", "theme.mp3"))
SKILL = os.path.join(HERE, "_yue2", "skills", "yue2-music", "instrumental", "scripts")
sys.path.insert(0, SKILL)
sys.path.insert(0, HERE)

STYLE = ("Instrumental, stylish noir crime funk, walking upright bass, crisp drums, muted trumpet, vibraphone, wah guitar, "
         "tense and cool, 104 BPM, no vocals, no singing, no choir, no spoken words.")
PLAN_LYRICS = "[Intro]\n\n[Verse]\n\n[Chorus]\n\n[Verse]\n\n[Chorus]\n\n[Bridge]\n\n[Chorus]\n\n[Outro]\n"


def encode(wav_audio, sr, path, kbps=160):
    import soundfile as sf
    tmp = path + ".wav"
    sf.write(tmp, wav_audio.astype(np.float32), sr, subtype="PCM_16")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-b:a", "%dk" % kbps, path], check=True)
    os.remove(tmp)


def main():
    args = sys.argv[1:]
    takes = int(args[args.index("--takes") + 1]) if "--takes" in args else 3
    if "--install" in args:
        n = int(args[args.index("--install") + 1])
        shutil.copyfile(os.path.join(WORK, "take%d_loop.mp3" % n), GAME)
        print("INSTALLED take", n, "->", GAME)
        return
    import soundfile as sf
    from instrumentalize import convert_score
    from instrumental import lyric_tags
    from yue2 import YuE2Pipeline
    from yue2.protocol import SongRequest, Sampling
    from make_loops_yue2 import find_loop, make_loop
    os.makedirs(WORK, exist_ok=True)
    pipe = YuE2Pipeline.from_pretrained("m-a-p/YuE2-3B", vae="m-a-p/YuE2-Vae", device="cuda", low_vram=True)
    try:
        for k in range(1, takes + 1):
            t0 = time.time()
            d = os.path.join(WORK, "take%d" % k)
            shutil.rmtree(d, ignore_errors=True)
            os.makedirs(d)
            print("TAKE", k, flush=True)
            try:
                plan = pipe.plan(request=SongRequest(id="theme%d" % k, style=STYLE, lyrics=PLAN_LYRICS, cot="full", seed=9100 + k))
                if plan.truncated or not plan.abc:
                    print("FAILED", k, "empty or truncated score", flush=True)
                    continue
                with open(os.path.join(d, "original.abc"), "w", encoding="utf-8") as f:
                    f.write(plan.abc)
                abc, transfer = convert_score(plan.abc, overlap="vocal", keep_chords=True)
                with open(os.path.join(d, "score.abc"), "w", encoding="utf-8") as f:
                    f.write(abc)
                tags = lyric_tags(abc)                      # section names only: nothing to sing
                song = pipe(style=STYLE, lyrics=tags, abc=abc, cot="full", seed=9100 + k,
                            semantic_sampling=Sampling(max_tokens=6000))
                song.save_artifacts(os.path.join(d, "native"))
            except Exception as ex:
                print("FAILED", k, repr(ex)[:600], flush=True)
                continue
            audio, sr = sf.read(os.path.join(d, "native", "audio.flac"), always_2d=True)
            try:
                s, e, sim, tempo, beat = find_loop(audio, sr)
            except Exception as ex:
                print("NOLOOP", k, repr(ex)[:300], flush=True)
                continue
            loop = make_loop(audio, sr, s, e, beat)
            peak = float(np.abs(loop).max())
            if peak > 0.98:
                loop = loop * (0.98 / peak)
            encode(loop, sr, os.path.join(WORK, "take%d_loop.mp3" % k))
            encode(np.concatenate([loop, loop, loop]), sr, os.path.join(WORK, "take%d_preview3x.mp3" % k))
            print("DONE take", k, "%.0f s made, loop %.1f s (%.0f BPM, match %.2f), %.0f s"
                  % (len(audio) / sr, (e - s) / sr, tempo, sim, time.time() - t0), flush=True)
            print("TRANSFER", {x: transfer[x] for x in list(transfer)[:8]}, flush=True)
    finally:
        pipe.close()


if __name__ == "__main__":
    main()
