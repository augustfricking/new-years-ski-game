"""Generates the DJ's songs with ACE-Step 1.5 (MIT, runs locally; the songs are ours to use).
The game now plays the YuE2 versions (make_songs_yue2.py reads its titles and lyrics from here).

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
    dict(id="techno", title="Pass Denied", style="Techno", bpm=132, duration=160, key="F minor",
         caption="male vocal, deep deadpan baritone, spoken word, vocoder, dark minimal Berlin techno, pounding four-on-the-floor kick, acid 303 bassline, metallic percussion, "
                 "industrial warehouse reverb, deadpan deep MALE spoken-word vocal, monotone German-accented narrator, "
                 "robotic vocoder on the hook, no singing, hypnotic and dry, no pop elements",
         lyrics="[Intro]\n\n[Verse - spoken, male, deadpan]\nIt is New Year's Eve\nI am at the turnstile\nMy ski pass\nhas expired\n\n"
                "[Hook - vocoder]\nBeep\nDenied\nBeep beep\nDenied\n\n[Drop]\n\n"
                "[Verse - spoken, male, deadpan]\nI have been in this queue\nsince last year\nMy toes are techno now\nThey only feel the kick\n\n"
                "[Hook - vocoder]\nBeep\nDenied\nBeep beep\nDenied\n\n[Breakdown]\nScan me again\nScan me again\n\n[Drop]\n\n[Outro]\nHappy new year\nDenied"),
    dict(id="edm", title="Countdown in My Ski Boots", style="EDM", bpm=128, duration=165, key="G major",
         caption="female vocal, bright powerful soprano, belting, huge festival big room EDM, supersaw leads, sidechained pumping kick, snare roll build-ups, airhorns, "
                 "euphoric drop with chopped vocal samples, DUET: bright young FEMALE vocal on verses and a hyped MALE MC shouting "
                 "the hype lines, crowd chanting numbers in the countdown",
         lyrics="[Verse 1 - female]\nTen to midnight and I lost my glove\nKissed a stranger in a puffer, call it love\nSequins on my helmet, glitter in my snow\nFireworks are ready, baby, here we go\n\n"
                "[Build - male MC]\nEverybody on the tables!\nHands up if you can't feel your face!\n\n[Pre-Chorus - crowd]\nTen! Nine! Eight! Seven!\n\n"
                "[Chorus - female]\nCountdown in my ski boots\nJumping till the roof breaks loose\nCountdown in my ski boots\nNew year, same old bruise\n\n[Drop]\n\n"
                "[Verse 2 - female]\nMy resolution is to fall less down\nBroke it in the gondola, the year's not out\n\n[Build - male MC]\nOne more time for the people in the queue!\n\n"
                "[Chorus - female]\nCountdown in my ski boots\nJumping till the roof breaks loose\n\n[Drop]\n\n[Outro - crowd]\nHappy new year!"),
    dict(id="pop", title="Snow Globe Heart", style="Pop", bpm=116, duration=160, key="D major",
         caption="female vocal, sweet airy alto, playful, sugary 2000s bubblegum pop, bouncy synth bass, handclaps, sleigh bells, glossy production, "
                 "sweet breathy high FEMALE lead vocal, a cheeky smooth MALE R&B vocal answers in the second verse, "
                 "flirty and playful duet, catchy whistle hook",
         lyrics="[Verse 1 - female]\nYou showed up in a borrowed coat\nGoggles on your head and a cold in your throat\nFell off the button lift, fell for me too\nHot chocolate kisses taste like snow and glue\n\n"
                "[Chorus - female]\nShake me up, my snow globe heart\nGlitter everywhere when we're apart\nTurn me over, watch me fall\nI'm the only snowflake at the ball\n\n"
                "[Verse 2 - male]\nGirl, I lost my ski poles and my dignity\nBut I found your number on the gondola seat\nCall me on the chair lift, call me after ski\nCall me anything but late for apres with me\n\n"
                "[Chorus - both]\nShake me up, my snow globe heart\nGlitter everywhere when we're apart\n\n[Bridge - female, whisper]\nIt's cold outside\nSo come inside\n\n"
                "[Chorus - both]\nTurn me over, watch me fall\nI'm the only snowflake at the ball"),
    dict(id="country", title="My Truck's Stuck at the Ski Lodge", style="Country", bpm=104, duration=160, key="A major",
         caption="male vocal, gritty raspy southern baritone, twang, honky tonk country, twangy telecaster, banjo rolls, fiddle, pedal steel, upright bass, train beat drums, "
                 "deep twangy Southern MALE baritone lead, a sassy FEMALE country singer joins on harmony in the chorus, "
                 "barroom singalong, humorous storytelling",
         lyrics="[Verse 1 - male]\nWell I drove up the mountain in a two wheel drive\nSaid the snow won't stop me, I'm a Texas guy\nNow my truck's in a snowbank and my dog's in the bar\nAnd the ski patrol is laughing at my brand new car\n\n"
                "[Chorus - male and female]\nMy truck's stuck at the ski lodge\nAnd my heart's stuck on you\nI got snow in my boots and beer on my shirt\nAnd a hangover due\n\n"
                "[Verse 2 - female]\nI said honey, take the tow rope, not the T-bar lift\nHe said darling, I'm a cowboy, and a cowboy don't quit\nThen he rode it like a bronco right into a tree\nNow he's sleeping on a sled and he's blaming me\n\n"
                "[Chorus - male and female]\nMy truck's stuck at the ski lodge\nAnd my heart's stuck on you\n\n[Fiddle Solo]\n\n[Outro - male]\nYeehaw, happy new year, y'all"),
    dict(id="hiphop", title="Fresh Powder", style="Hip hop", bpm=88, duration=160, key="C minor",
         caption="male vocal, aggressive rap, gravelly low voice, hard trap hip hop, booming 808 bass, rolling hi-hats, dark piano and choir samples, "
                 "aggressive gritty MALE rapper with fast confident flow and ad-libs, a raspy FEMALE rapper takes the second verse, "
                 "braggadocio, no singing on verses, chanted hook",
         lyrics="[Verse 1 - male rap]\nFirst chair, cold air, nobody there\nGoggles mirror gold so they can't see me stare\nCarving through the kids like a holiday sale\nSki school in a line like a snowman jail\nYeah, my pass got stamped, my nose got frost\nI'm the boss of the slope, ask Santa who lost\n\n"
                "[Hook - chant]\nFresh powder, fresh powder\nTurn it up a little louder\nFresh powder, fresh powder\nNew year, I got power\n\n"
                "[Verse 2 - female rap]\nI don't need a lift, I got hype in my legs\nMy snowboard's sharper than your Christmas eggnog dregs\nYou said black run, baby, I said bet\nNow you're in the trees sending me a regret\n\n"
                "[Hook - chant]\nFresh powder, fresh powder\nTurn it up a little louder\n\n[Outro - male]\nYeah, happy new year, stay icy"),
    dict(id="classical", title="Inverno Furioso", style="Classical", bpm=96, duration=165, key="F minor",
         caption="female vocal, operatic coloratura soprano, vibrato, baroque concerto in the style of Vivaldi's Winter from The Four Seasons, fierce staccato string orchestra, solo violin, "
                 "harpsichord continuo, comic opera buffa in the style of Mozart, operatic SOPRANO aria and a booming BASS buffo, "
                 "dramatic, theatrical, concert hall, no drums, no electronics, no pop",
         lyrics="[Instrumental - strings, furious tremolo]\n\n[Aria - soprano]\nAh! Ah! The snow, the snow!\nMy frozen toes, oh woe, oh woe!\nThe lift is stuck, the wind does blow\nAh, ah, ah, where did my ski poles go!\n\n"
                "[Bass buffo]\nSilence, silence, little goose!\nYour bindings, madam, they are loose!\nI warned you at the top, you see\nNow you must ski it down with me!\n\n"
                "[Duet - soprano and bass]\nInverno, inverno furioso!\nA glass of mulled wine, molto grazioso!\n\n[Instrumental - solo violin]\n\n"
                "[Aria - soprano, coloratura]\nAh, ah, ah, ah, ah, the new year comes!\n\n[Finale - duet]\nInverno furioso! Bravo! Bravo!"),
    dict(id="rock", title="Black Run", style="Rock", bpm=150, duration=160, key="E minor",
         caption="male vocal, screaming raspy rock tenor, powerful, loud punk rock and hard rock, distorted guitars, fast driving drums, gang vocals, "
                 "raspy screaming MALE punk vocalist, a powerful gritty FEMALE rock singer duets the bridge, "
                 "rebellious, sweaty, shout-along chorus, guitar solo, garage energy",
         lyrics="[Verse 1 - male]\nStanding at the top with a hangover head\nSign says experts only, sign says you're dead\nMy mum is on the phone and my legs are on fire\nI've been in this helmet since nineteen ninety-nine\n\n"
                "[Chorus - gang vocals]\nBlack run! Black run!\nNo turning back till the night is done\nBlack run! Black run!\nI fell on my face and I'm having fun\n\n[Guitar Solo]\n\n"
                "[Bridge - female]\nI'll see you at the bottom or I'll see you in the snow\nIf you can't stop, then don't you dare go slow\n\n"
                "[Chorus - gang vocals]\nBlack run! Black run!\nNo turning back till the night is done\n\n[Outro - male scream]\nHappy new year!"),
    dict(id="classicrock", title="Chairlift Highway", style="Classic rock", bpm=120, duration=160, key="A major",
         caption="male vocal, warm raspy classic rock tenor, 1970s classic rock, crunchy overdriven guitar riffs, Hammond organ, "
                 "bluesy guitar solo, bass guitar, steady rock drums, arena rock anthem",
         lyrics="[Verse]\nLift ticket flapping on my old blue coat\nThermos in my pocket and a hole in my glove\n"
                "Sun goes down behind the northern ridge\nLast chair swinging up above the bridge\n\n"
                "[Chorus]\nRide the chairlift highway\nUp into the burning sky\nOne more run before midnight\nThen we're gonna say goodbye\n\n"
                "[Verse]\nOld man Sven on the snowcat road\nPlays his radio loud while the cold winds blow\n"
                "The valley lights are coming on below\nTwelve more minutes and a mile of snow\n\n"
                "[Chorus]\nRide the chairlift highway\nUp into the burning sky\nOne more run before midnight\nThen we're gonna say goodbye\n\n"
                "[Bridge]\nHold on tight\nHere comes the night\nThe whole mountain's shining white\n\n"
                "[Chorus]\nRide the chairlift highway\nUp into the burning sky\nOne more run before midnight\nThen we're gonna say goodbye\n\n"
                "[Outro]\nChairlift highway\nTake me home"),
    dict(id="oldschool", title="Boombox on the Ski Lift", style="Old school hip hop", bpm=92, duration=160, key="D minor",
         caption="male vocal, laid-back confident old school rapper, 1990s boom bap hip hop, dusty vinyl drum break, jazzy sampled piano, "
                 "upright bass, turntable scratches, head-nodding groove",
         lyrics="[Verse]\nYo, it's ninety-two on the T-bar line\nBoombox on my shoulder and the bass sounds fine\n"
                "Neon onesie, got the headband tight\nWe ride the mountain like it's Friday night\n"
                "Fresh tracks, fresh kicks, fresh powder too\nSki patrol nodding like they know the crew\n\n"
                "[Chorus]\nBoombox on the ski lift, turn it up loud\nBounce with the snowflakes, rock the whole crowd\n"
                "Boombox on the ski lift, don't stop the beat\nOld school flow with the snow on our feet\n\n"
                "[Verse]\nMittens on the decks, let the record spin\nHot chocolate steaming when the cold creeps in\n"
                "Pizza on the bunny hill, French fries on the steep\nGrandma on a snowboard and she don't miss a beat\n"
                "From the top of the peak to the lodge down low\nWe bring the rhythm everywhere we go\n\n"
                "[Chorus]\nBoombox on the ski lift, turn it up loud\nBounce with the snowflakes, rock the whole crowd\n"
                "Boombox on the ski lift, don't stop the beat\nOld school flow with the snow on our feet\n\n"
                "[Outro]\nNinety-two, still true\nOn the mountain, peace"),
    dict(id="afterski", title="Boots on the Table", style="Afterski", bpm=138, duration=170, key="C major",
         caption="male vocal, drunk cheerful schlager baritone, crowd singalong, Alpine apres-ski schlager party anthem, oom-pah dance beat, accordion, brass section, tuba, cowbells, "
                 "cheesy cheerful MALE party singer with a hoarse voice, a big DRUNK CROWD SINGALONG choir shouting the chorus in the background, "
                 "live tavern atmosphere, clapping, cheering, whistles, beer hall, ski lodge",
         lyrics="[Intro - crowd cheering]\nHey! Hey! Hey!\n\n[Verse 1 - male]\nThe lift is closed, the sun is gone\nMy boots are wet but I'm keeping them on\nThe barman says it's time to dance\nI've lost my skis but not my pants\n\n"
                "[Chorus - male and crowd]\nBoots on the table! (Hey!)\nBeer in the air! (Hey!)\nBoots on the table!\nSnow in my hair!\nLa la la la la la la\nLa la la la la!\n\n"
                "[Verse 2 - male]\nGranny's on the bench and she's doing the splits\nUncle in a helmet and nothing else fits\nWe sing it once and we sing it loud\nThe whole mountain's singing with the tavern crowd\n\n"
                "[Chorus - male and crowd]\nBoots on the table! (Hey!)\nBeer in the air! (Hey!)\nBoots on the table!\nSnow in my hair!\nLa la la la la la la\nLa la la la la!\n\n"
                "[Breakdown - crowd clapping]\nOne more time!\n\n[Chorus - crowd]\nBoots on the table! Boots on the table!\n\n[Outro - crowd cheering]\nHey!"),
]

def toml_str(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'


# The other voice in each song: its tags, and where it sings (seconds).
GUEST = {
    "techno": ("female vocal, cold whispered, breathy", 70, 100),
    "edm": ("male vocal, hyped MC, shouting", 40, 62),
    "pop": ("male vocal, smooth tenor, falsetto", 75, 105),
    "country": ("female vocal, warm nasal country alto", 70, 100),
    "hiphop": ("female vocal, sultry R&B hook singer", 50, 75),
    "classical": ("male vocal, booming operatic bass", 60, 95),
    "rock": ("female vocal, punk snarl, shouting", 70, 98),
    "afterski": ("female vocal, loud beer-hall soprano", 60, 90),
}


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
            "thinking": False,                 # the caption reaches the music model as written (the LM would rewrite it)
            "use_cot_caption": False,
            "use_cot_metas": False,
            "use_cot_language": False,
            "use_cot_lyrics": False,
            "lm_temperature": 0.9,
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
        final = made[0]
        # A guest singer of the other gender takes over one part (a second verse or bridge): the
        # song is repainted there with their voice, the rest kept as it was.
        guest = GUEST.get(song["id"])
        if guest:
            gdir = out_dir + "_guest"
            shutil.rmtree(gdir, ignore_errors=True); os.makedirs(gdir)
            body = song["caption"].split(", ", 3)[-1]
            g = dict(cfg, task_type="repaint", src_audio=made[0].replace("\\", "/"), repainting_start=guest[1], repainting_end=guest[2],
                     caption=guest[0] + ", " + body, save_dir=gdir.replace("\\", "/"), thinking=False)
            gpath = os.path.join(gdir, "config.toml")
            with open(gpath, "w", encoding="utf-8") as f:
                for k, v in g.items():
                    if isinstance(v, bool): f.write("%s = %s\n" % (k, "true" if v else "false"))
                    elif isinstance(v, (int, float)): f.write("%s = %s\n" % (k, v))
                    elif isinstance(v, list): f.write("%s = [%s]\n" % (k, ", ".join(str(x) for x in v)))
                    else: f.write("%s = %s\n" % (k, toml_str(v)))
            r2 = subprocess.run([py, "cli.py", "-c", gpath, "--backend", "pt", "--log-level", "WARNING"], cwd=ACE,
                                input="y\n" * 4, text=True, capture_output=True, encoding="utf-8", errors="replace", env=env)
            gm = [os.path.join(root, n) for root, _, files in os.walk(gdir) for n in files if n.endswith(".mp3")]
            if gm: final = gm[0]
            else: print("GUEST FAILED", song["id"], (r2.stderr or "")[-800:], flush=True)
        shutil.copyfile(final, os.path.join(OUT, song["id"] + ".mp3"))
        print("DONE", song["id"], os.path.getsize(final) // 1024, "KB", "with guest" if final != made[0] else "", flush=True)
    meta = [dict(id=s["id"], title=s["title"], style=s["style"]) for s in SONGS if os.path.exists(os.path.join(OUT, s["id"] + ".mp3"))]
    with open(os.path.join(OUT, "songs.json"), "w", encoding="utf-8") as f:
        json.dump({"songs": meta}, f, indent=1)
    print("SONGS", len(meta), flush=True)


if __name__ == "__main__":
    main()
