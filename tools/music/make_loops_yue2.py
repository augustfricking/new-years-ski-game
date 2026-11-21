"""Looping game music made with YuE2 (instrumental), for the owner to choose from before anything in the
game changes. Each candidate is generated, then cut into a seamless loop: the start and end are beats
the same number of whole bars apart, picked where the music sounds most alike, and the end crossfades
into what comes just before the start, so the loop runs on with no bump.

Run:     tools/music/_yue2/.venv/Scripts/python.exe tools/music/make_loops_yue2.py [name ...] [--reuse]
         (--reuse cuts the loop again from audio already made, without generating)
         (needs librosa in that environment: python -m uv pip install --python tools/music/_yue2/.venv/Scripts/python.exe librosa)
Output:  tools/music/_work/loops/<name>_loop.ogg (the loop itself) and <name>_preview3x.mp3 (it played three times,
         to listen for the seam). Licence and credit: see make_songs_yue2.py.
"""
import os
import subprocess
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "_work", "loops")

# Which game music each would replace (AudioDirector.Music ids) and how it should sound.
LOOPS = {
    "ski_synth": ("music_ski", "instrumental, no vocals, 1980s synthwave ski montage, driving pulsing bass, gated drums, bright synth "
                               "arpeggios, soaring lead synth, energetic and cool, 118 BPM"),
    "ski_noir": ("music_ski", "instrumental, no vocals, stylish noir crime funk, walking upright bass, crisp drums, muted trumpet, "
                              "vibraphone, wah guitar, tense and cool, 104 BPM"),
    "menu_lodge": ("music_menu", "instrumental, no vocals, cozy winter lodge lounge, mellow Rhodes electric piano, warm bass, soft "
                                 "brushed drums, sleigh bells, calm and warm, 84 BPM"),
}


def find_loop(audio, sr):
    """Start and end sample of the best loop: whole 4-bar steps, at least 40 s, where the music matches."""
    import librosa
    mono = audio.mean(axis=1) if audio.ndim == 2 else audio
    asr = 22050
    m = librosa.resample(mono.astype(np.float32), orig_sr=sr, target_sr=asr)
    tempo, beat_frames = librosa.beat.beat_track(y=m, sr=asr)
    beat_frames = np.asarray(beat_frames)
    chroma = librosa.feature.chroma_cqt(y=m, sr=asr)
    mfcc = librosa.feature.mfcc(y=m, sr=asr, n_mfcc=13)
    rms = librosa.feature.rms(y=m)
    feat = np.vstack([chroma, mfcc / (np.std(mfcc, axis=1, keepdims=True) + 1e-6) * 0.5, rms / (rms.max() + 1e-6) * 4])
    sync = librosa.util.sync(feat, beat_frames, aggregate=np.mean)       # one column per beat (plus the edges)
    times = librosa.frames_to_time(beat_frames, sr=asr)
    n = len(times)
    dur = len(mono) / sr
    best, pick = -1e9, None
    for i in range(n):
        if times[i] < 6.0 or times[i] > dur * 0.4:
            continue
        for j in range(i + 16, n, 16):
            if times[j] > dur - 6.0:
                break
            if times[j] - times[i] < 40.0:
                continue
            a = sync[:, max(i - 2, 0):i + 2].ravel()
            b = sync[:, max(j - 2, 0):j + 2].ravel()
            k = min(len(a), len(b))
            sim = float(np.dot(a[:k], b[:k]) / (np.linalg.norm(a[:k]) * np.linalg.norm(b[:k]) + 1e-9))
            score = sim + 0.002 * (times[j] - times[i])          # a little in favour of longer loops
            if score > best:
                best, pick = score, (i, j, sim)
    if pick is None:
        raise RuntimeError("no loop found (song too short?)")
    i, j, sim = pick
    beat = float(np.median(np.diff(times))) if n > 1 else 0.5
    return int(times[i] * sr), int(times[j] * sr), sim, float(np.atleast_1d(tempo)[0]), beat


def make_loop(audio, sr, s, e, beat):
    """The loop's last beat fades into the beat before its start (equal power), so the end runs into the start."""
    x = int(min(beat, 0.6) * sr)
    x = min(x, s)
    loop = audio[s:e].copy()
    t = np.linspace(0.0, 1.0, x)[:, None]
    loop[-x:] = loop[-x:] * np.cos(t * np.pi / 2) + audio[s - x:s] * np.sin(t * np.pi / 2)
    return loop


def main():
    import soundfile as sf
    names = [a for a in sys.argv[1:] if a in LOOPS] or list(LOOPS)
    reuse = "--reuse" in sys.argv          # cut the loop again from audio made before
    os.makedirs(WORK, exist_ok=True)
    from yue2 import YuE2Pipeline
    from yue2.protocol import Sampling
    need = [n for n in names if not (reuse and os.path.exists(os.path.join(WORK, n, "audio.flac")))]
    pipe = YuE2Pipeline.from_pretrained("m-a-p/YuE2-3B", vae="m-a-p/YuE2-Vae", device="cuda", low_vram=True) if need else None
    try:
        for k, name in enumerate(names):
            use, style = LOOPS[name]
            t0 = time.time()
            print("LOOP", name, "for", use, flush=True)
            d = os.path.join(WORK, name)
            if not (reuse and os.path.exists(os.path.join(d, "audio.flac"))):
                try:
                    result = pipe(style=style, lyrics="", cot="full", seed=7300 + k,
                                  semantic_sampling=Sampling(min_tokens=2500, max_tokens=5000))
                except Exception as ex:
                    print("FAILED", name, repr(ex)[:600], flush=True)
                    continue
                if os.path.exists(d):
                    import shutil
                    shutil.rmtree(d)
                result.save_artifacts(d)
            audio, sr = sf.read(os.path.join(d, "audio.flac"), always_2d=True)
            try:
                s, e, sim, tempo, beat = find_loop(audio, sr)
            except Exception as ex:
                print("NOLOOP", name, repr(ex)[:300], flush=True)
                continue
            loop = make_loop(audio, sr, s, e, beat)
            peak = float(np.abs(loop).max())
            if peak > 0.98:
                loop = loop * (0.98 / peak)
            # Written as WAV and encoded by ffmpeg (libsndfile's own Vorbis writer crashes on long audio).
            wav = os.path.join(WORK, name + "_loop.wav")
            sf.write(wav, loop.astype(np.float32), sr, subtype="PCM_16")
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "5",
                            os.path.join(WORK, name + "_loop.ogg")], check=True)
            wav3 = os.path.join(WORK, name + "_preview3x.wav")
            sf.write(wav3, np.concatenate([loop, loop, loop]).astype(np.float32), sr, subtype="PCM_16")
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav3, "-b:a", "160k", wav3[:-4] + ".mp3"], check=True)
            os.remove(wav3)
            os.remove(wav)
            print("DONE", name, "%.0f s generated, loop %.1f-%.1f s (%.1f s, %.0f BPM, match %.2f) in %.0f s"
                  % (len(audio) / sr, s / sr, e / sr, (e - s) / sr, tempo, sim, time.time() - t0), flush=True)
    finally:
        if pipe is not None:
            pipe.close()


if __name__ == "__main__":
    main()
