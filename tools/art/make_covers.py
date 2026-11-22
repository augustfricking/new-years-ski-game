"""Cover art for the DJ's and the walkman's songs, made locally with FLUX.1-schnell (Apache 2.0).

One square picture per song, of what the song is about, in the game's look: thick ink lines, flat
paint, a dusky purple and teal palette with a warm sunset glow. No lettering (the card shows the
title beside it). Several seeds each; a contact sheet to pick from; the picked ones are written,
small, to SkiGame/Assets/_Game/Resources/Covers/<id>.png, where the game loads them.

  tools/art/_venv/Scripts/python.exe -I tools/art/make_covers.py [id ...] [--seeds N]
  tools/art/_venv/Scripts/python.exe -I tools/art/make_covers.py --pick techno=1000 edm=1077 ...

Uses make_art.py's model loading and prompt cache. Output in tools/art/_covers (not in git).
"""
import os
import sys
import gc

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_art as base

OUT = os.path.join(base.HERE, "_covers")
base.OUT = OUT
GAME = os.path.normpath(os.path.join(base.HERE, "..", "..", "SkiGame", "Assets", "_Game", "Resources", "Covers"))
SIZE = 256

STYLE = ("square album cover illustration in a graphic novel style, bold thick black ink outlines, flat painted colour with hard-edged "
         "shadow shapes, a little brush texture, dusky palette of deep purples and teals lit by a warm peach and orange sunset glow, "
         "strong simple composition that reads at a small size, no text, no letters, no words, no logo, no watermark, no border")
SHORT = "album cover, graphic novel art, thick ink outlines, flat colour, dusky purple and teal, sunset glow, no text"

SONGS = {
    "techno": "a ski lift turnstile gate at night glowing with a big red X light that refuses a lift pass held out by a gloved hand, "
              "pulsing red and violet light beams, a dark mountain behind",
    "edm": "a pair of chunky ski boots standing on a glowing dance floor, laser beams and new year fireworks bursting in the night sky above "
           "a snowy mountain, confetti",
    "pop": "a glass snow globe with a heart-shaped snowy mountain and a tiny chalet inside, pink and peach sparkles, swirling snow, "
           "held up against a pastel dusk sky",
    "country": "an old pickup truck stuck in deep snow up to its wheels in front of a wooden ski lodge with warm lit windows, "
               "a cowboy hat on the bonnet, pine trees, dusk",
    "hiphop": "a snowboarder in baggy clothes carving a huge spray of fresh powder snow, a gold chain flying, seen from low below, "
              "deep blue evening sky",
    "classical": "a violin and a bow caught in a wild swirling snow storm on a mountain ridge, sheets of snow like wind, dramatic and stormy",
    "rock": "a steep icy black ski slope dropping away under a jagged dark peak, an electric guitar planted in the snow like a flag, "
            "a lightning bolt in a stormy purple sky",
    "classicrock": "a chairlift line stretching away over snowy mountains into a huge orange sunset, an empty chair swinging in the "
                   "foreground, open road feeling",
    "oldschool": "a big silver boombox riding alone on a ski lift chair high over snowy pine trees, sound waves drawn as bold rings, dusk",
    "afterski": "ski boots up on a wooden table in a cosy alpine tavern, beer steins clinking, warm lamp light, a fireplace, "
                "a window onto snowy slopes at dusk",
}


def pick(args):
    from PIL import Image
    os.makedirs(GAME, exist_ok=True)
    for a in args:
        song, seed = a.split("=")
        src = os.path.join(OUT, "%s_%s.png" % (song, seed))
        img = Image.open(src).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
        dst = os.path.join(GAME, song + ".png")
        img.save(dst, optimize=True)
        print("COVER", dst, flush=True)


def main():
    args = sys.argv[1:]
    if "--pick" in args:
        pick([a for a in args if "=" in a])
        return
    import torch
    from PIL import Image, ImageDraw
    seeds = int(args[args.index("--seeds") + 1]) if "--seeds" in args else 3
    songs = [a for a in args if a in SONGS] or list(SONGS)
    os.makedirs(OUT, exist_ok=True)
    pipe = base.load_pipe()
    jobs = []
    for song in songs:
        full = "%s. %s. %s" % (STYLE, SONGS[song], STYLE)
        short = "%s, %s" % (SHORT, SONGS[song].split(",")[0])
        pe, pooled = base.encode(pipe, short, full)
        jobs.append((song, pe, pooled))
        print("ENCODED", song, flush=True)
    pipe.text_encoder = None; pipe.text_encoder_2 = None
    gc.collect(); torch.cuda.empty_cache()
    pipe.vae.to("cuda")
    pipe.transformer.enable_group_offload(onload_device=torch.device("cuda"), offload_device=torch.device("cpu"),
                                          offload_type="block_level", num_blocks_per_group=6, use_stream=True)
    shots = []
    for song, pe, pooled in jobs:
        for k in range(seeds):
            seed = 1000 + k * 77
            g = torch.Generator("cpu").manual_seed(seed)
            img = pipe(prompt_embeds=pe.to("cuda", torch.bfloat16), pooled_prompt_embeds=pooled.to("cuda", torch.bfloat16),
                       width=768, height=768, num_inference_steps=4, guidance_scale=0.0, generator=g).images[0]
            path = os.path.join(OUT, "%s_%d.png" % (song, seed))
            img.save(path)
            shots.append((song, seed, img))
            print("IMAGE", path, flush=True)
    # One contact sheet: a row per song, a column per seed.
    t = 256
    rows = [s for s in songs]
    sheet = Image.new("RGB", (t * seeds, t * len(rows)), (40, 40, 48))
    d = ImageDraw.Draw(sheet)
    for song, seed, img in shots:
        x, y = ((seed - 1000) // 77) * t, rows.index(song) * t
        sheet.paste(img.resize((t, t)), (x, y))
        d.rectangle([x, y, x + 120, y + 18], fill=(20, 20, 24))
        d.text((x + 4, y + 3), "%s %d" % (song, seed), fill=(255, 255, 255))
    sheet.save(os.path.join(OUT, "_sheet.png"))
    print("DONE", flush=True)


if __name__ == "__main__":
    main()
