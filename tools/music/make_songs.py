"""Generates the DJ's songs with ACE-Step 1.5 (MIT, runs locally; the songs are ours to use).

Lyrics and style descriptions are written here; ACE-Step makes the music and the singing. Every
song is original: no artist, title or melody is imitated.

Setup (once):  see tools/music/README.md
Run:           tools/music/_ace/.venv/Scripts/python.exe tools/music/make_songs.py [song id ...]
Output:        SkiGame/Assets/StreamingAssets/Music/<id>.mp3 and songs.json (titles, styles)
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ACE = os.path.join(HERE, "_ace")
WORK = os.path.join(HERE, "_work")
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "SkiGame", "Assets", "StreamingAssets", "Music"))

SONGS = [
    dict(id="techno", title="Gondola Pulse", style="Techno", bpm=130, duration=150, key="A minor",
         caption="Dark driving techno, four-on-the-floor kick, rolling bassline, hypnotic analog synth stabs, "
                 "crisp hi-hats, warehouse reverb, a breathy whispered vocal hook repeated over the drop, "
                 "steady and relentless, built for dancing in ski boots",
         lyrics="[Intro]\n\n[Verse]\nLast run\nLast run\nInto the white\n\n[Build]\nHold on\nHold on\nTo the night\n\n"
                "[Drop]\n\n[Breakdown]\nLast run\nInto the light\n\n[Drop]\n\n[Outro]"),
    dict(id="edm", title="Midnight on the Mountain", style="EDM", bpm=126, duration=165, key="F major",
         caption="Euphoric festival EDM, big supersaw chords, punchy sidechained kick, soaring bright female pop vocal, "
                 "huge build-ups with snare rolls, uplifting anthem drop, New Year's Eve celebration energy",
         lyrics="[Verse 1]\nTen more minutes till the year runs out\nSnow on the rooftops, we can hear them shout\n"
                "Boots on the tables and the lights turned low\nEveryone's waiting for the countdown glow\n\n"
                "[Pre-Chorus]\nHold my hand, don't let go\nTen, nine, eight, here we go\n\n"
                "[Chorus]\nMidnight on the mountain\nFireworks in the sky\nMidnight on the mountain\nWe were born to fly\n\n"
                "[Drop]\n\n[Verse 2]\nCheeks still frozen from the final run\nWe raced the sunset and the sunset won\n"
                "Now the music's loud and the night is young\nEvery heartbeat is a song unsung\n\n"
                "[Chorus]\nMidnight on the mountain\nFireworks in the sky\nMidnight on the mountain\nWe were born to fly\n\n"
                "[Drop]\n\n[Outro]\nHappy new year"),
    dict(id="pop", title="Snow Globe Heart", style="Pop", bpm=112, duration=160, key="D major",
         caption="Bright catchy modern pop, bouncy synth bass, handclaps, sleigh bell sparkle, warm female lead vocal "
                 "with harmonies in the chorus, playful and romantic winter love song",
         lyrics="[Verse 1]\nYou showed up in a borrowed coat\nGoggles on your head and a song in your throat\n"
                "Fell on the bunny hill and laughed it off\nHot chocolate kisses, a little too soft\n\n"
                "[Chorus]\nShake me up, my snow globe heart\nEverything's glitter when we're not apart\n"
                "Turn me over, watch me fall\nYou're the only one in the snowfall\n\n"
                "[Verse 2]\nChair lift swinging over sleepy pines\nYour hand in my mitten, it's a perfect sign\n"
                "Down in the village all the windows glow\nI don't ever want to let this go\n\n"
                "[Chorus]\nShake me up, my snow globe heart\nEverything's glitter when we're not apart\n"
                "Turn me over, watch me fall\nYou're the only one in the snowfall\n\n"
                "[Bridge]\nOoh, the flakes keep coming down\nOoh, a crown on every town\n\n"
                "[Chorus]\nShake me up, my snow globe heart\nEverything's glitter when we're not apart"),
    dict(id="country", title="Boots by the Fire", style="Country", bpm=96, duration=160, key="G major",
         caption="Warm country song, acoustic guitar strumming, pedal steel, fiddle, brushed drums, "
                 "friendly male baritone vocal with a smile, cozy cabin singalong storytelling",
         lyrics="[Verse 1]\nWell I came down the mountain with snow in my beard\nMy knees were a-shaking, my skis disappeared\n"
                "But the tavern was singing and the beer was cold\nAnd a fiddle was playing some song from of old\n\n"
                "[Chorus]\nSo leave your boots by the fire, darling\nHang your hat on the wall\n"
                "The lift may be closed but the night's still young\nAnd there's room for us all\n\n"
                "[Verse 2]\nThere's a girl from the valley who dances like snow\nAnd a fella from Texas who's never seen it before\n"
                "We'll toast to the hill and the friends that we found\nTill the last of the candles burns all the way down\n\n"
                "[Chorus]\nSo leave your boots by the fire, darling\nHang your hat on the wall\n"
                "The lift may be closed but the night's still young\nAnd there's room for us all\n\n"
                "[Outro]\nRoom for us all"),
    dict(id="hiphop", title="Fresh Powder", style="Hip hop", bpm=92, duration=150, key="C minor",
         caption="Laid-back boom bap hip hop, dusty drums, deep bass, jazzy piano loop, scratches, "
                 "confident male rapper with clear rhythmic flow, playful braggadocio about skiing",
         lyrics="[Verse 1]\nWoke up early, first chair, nobody there\nFresh powder on the run and the cold in the air\n"
                "Edges sharp like a pencil, carving my name\nThey see me from the gondola, they know the game\n"
                "Goggles mirror gold, I don't need to say a word\nSlide past the ski school like a low-flying bird\n\n"
                "[Hook]\nFresh powder, fresh powder\nTurn it up a little louder\nFresh powder, fresh powder\nEvery run I'm getting prouder\n\n"
                "[Verse 2]\nPark lap, switch landing, ask the crowd how it went\nHalf the money that I had went on the lift pass rent\n"
                "Après in the evening, boots up on the bench\nTelling stories bigger than the black run's trench\n\n"
                "[Hook]\nFresh powder, fresh powder\nTurn it up a little louder\nFresh powder, fresh powder\nEvery run I'm getting prouder"),
    dict(id="classical", title="Waltz of the Falling Snow", style="Classical", bpm=84, duration=150, key="E flat major",
         caption="Elegant classical orchestral waltz in three-four time, strings, flutes, French horns, harp glissandos, "
                 "a ballroom on New Year's Eve, romantic and sparkling, concert hall recording",
         lyrics="[Instrumental]", instrumental=True, timesig="3"),
    dict(id="rock", title="Black Run", style="Rock", bpm=140, duration=160, key="E minor",
         caption="High-energy arena rock, distorted electric guitars, driving bass, big pounding drums, "
                 "gritty powerful male rock vocal, shout-along chorus, guitar solo, adrenaline",
         lyrics="[Verse 1]\nStanding at the top where the warning signs are red\nWind is screaming stories of the ones who went ahead\n"
                "Heart is like a hammer and my legs are made of stone\nOne more breath and I'm going down alone\n\n"
                "[Chorus]\nBlack run, black run\nNo turning back when the night has begun\nBlack run, black run\nRiding the edge till the mountain is done\n\n"
                "[Guitar Solo]\n\n[Verse 2]\nIce beneath the moguls and the shadows getting long\nEvery bump's a drumbeat, every turn's a song\n"
                "If I hit the bottom then I'll raise a glass up high\nAnd if I don't, you'll hear me in the sky\n\n"
                "[Chorus]\nBlack run, black run\nNo turning back when the night has begun\nBlack run, black run\nRiding the edge till the mountain is done\n\n"
                "[Outro]\nBlack run"),
]


def toml_str(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'


def main():
    wanted = set(sys.argv[1:])
    os.makedirs(WORK, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    py = os.path.join(ACE, ".venv", "Scripts", "python.exe")
    for song in SONGS:
        if wanted and song["id"] not in wanted:
            continue
        out_dir = os.path.join(WORK, song["id"])
        shutil.rmtree(out_dir, ignore_errors=True)
        os.makedirs(out_dir)
        cfg = {
            "config_path": "acestep-v15-turbo",
            "lm_model_path": "acestep-5Hz-lm-0.6B",
            "backend": "pt",
            "device": "cuda",
            "offload_to_cpu": True,
            "save_dir": out_dir.replace("\\", "/"),
            "audio_format": "mp3",
            "caption": song["caption"],
            "lyrics": song["lyrics"],
            "instrumental": bool(song.get("instrumental")),
            "duration": float(song["duration"]),
            "bpm": song["bpm"],
            "keyscale": song["key"],
            "timesignature": song.get("timesig", "4"),
            "vocal_language": "en",
            "task_type": "text2music",
            "thinking": True,
            "batch_size": 1,
            "seeds": [20261231 + len(song["id"])],
            "shift": 3.0,
            "inference_steps": 8,
        }
        path = os.path.join(out_dir, "config.toml")
        with open(path, "w", encoding="utf-8") as f:
            for k, v in cfg.items():
                if isinstance(v, bool): f.write("%s = %s\n" % (k, "true" if v else "false"))
                elif isinstance(v, (int, float)): f.write("%s = %s\n" % (k, v))
                elif isinstance(v, list): f.write("%s = [%s]\n" % (k, ", ".join(str(x) for x in v)))
                else: f.write("%s = %s\n" % (k, toml_str(v)))
        print("SONG", song["id"], "...", flush=True)
        env = dict(os.environ, PYTHONIOENCODING="utf-8", PYTHONUTF8="1")
        r = subprocess.run([py, "cli.py", "-c", path, "--backend", "pt", "--log-level", "WARNING"], cwd=ACE,
                           input="y\n" * 4, text=True, capture_output=True, encoding="utf-8", errors="replace", env=env)
        made = [os.path.join(root, n) for root, _, files in os.walk(out_dir) for n in files if n.endswith(".mp3")]
        if not made:
            print("FAILED", song["id"], r.returncode, (r.stdout or "")[-1500:], (r.stderr or "")[-3000:], flush=True)
            continue
        shutil.copyfile(made[0], os.path.join(OUT, song["id"] + ".mp3"))
        print("DONE", song["id"], os.path.getsize(made[0]) // 1024, "KB", flush=True)
    meta = [dict(id=s["id"], title=s["title"], style=s["style"]) for s in SONGS if os.path.exists(os.path.join(OUT, s["id"] + ".mp3"))]
    with open(os.path.join(OUT, "songs.json"), "w", encoding="utf-8") as f:
        json.dump({"songs": meta}, f, indent=1)
    print("SONGS", len(meta), flush=True)


if __name__ == "__main__":
    main()
