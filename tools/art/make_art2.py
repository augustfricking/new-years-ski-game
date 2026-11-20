"""Round two of the concept art (the owner's notes): all four about 22-23, compact proportions
(a little short, slightly large heads), the noir graphic-novel look, and every view the 3D work
needs, plus sketches of each one's skis or board and of every item they can unlock.

  tools/art/_venv/Scripts/python.exe -I tools/art/make_art2.py [who ...] [--only views|heads|face|gear|items] [--seeds N]

Output in tools/art/_out2/: <who>_<kind>_<seed>.png, items as <who>_item_<id>.png, and contact sheets.
Uses make_art.py's model loading and prompt cache.
"""
import os
import sys
import gc
import torch
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_art as base

OUT = os.path.join(base.HERE, "_out2")
base.OUT = OUT      # the prompt cache lives here too

NOIR = ("gritty 1980s noir crime graphic novel art, heavy chiaroscuro, hard-edged solid flat black shadow shapes on one side of the face "
        "and body, bold thick black ink outlines with varying weight, coarse brush hatching, painterly textured colour with visible brush "
        "strokes, dramatic coloured rim light, muted dusky palette of purples and teals with warm skin, grounded young adults with "
        "realistic faces, stern or wry expressions, not cute, not chibi, not a child, not a teenager, not anime, not a cartoon, not a pin-up")
SHORT = "gritty noir crime graphic novel art, heavy chiaroscuro, solid black shadows, thick ink outlines, painterly colour, young adult"
PROPORTIONS = ("only slightly stylised build: a little shorter than average with a head just slightly larger than realistic, "
               "about six and a half heads tall, adult body")

PEOPLE = {
    "erik": dict(
        who="a sporty Scandinavian man in his early twenties (an adult, about 23), long angular face, square jaw with a small cleft chin, heavy straight brows, "
            "grey-blue eyes, straight nose, short swept sandy-blond hair, confident half smile",
        wear="a retro 1980s orange ski jacket with a broad navy-blue band across the chest and sleeves, navy ski pants, black gloves, "
             "red-orange ski boots",
        gear="a pair of slim carving skis with a blue top sheet and yellow stripes, short ski poles"),
    "jonte": dict(
        who="a big Scandinavian snowboarder in his early twenties (an adult, about 23), broad round face with full cheeks, small friendly eyes, "
            "wide flat upturned nose, big ears, short messy dark-brown hair, light stubble, no beard, broad and stocky with a small belly, "
            "easy-going grin",
        wear="a baggy oversized teal snowboard jacket, very baggy mustard-yellow snowboard pants bunched over the boots, dark grey gloves, "
             "black snowboard boots",
        gear="a snowboard with a red top sheet and a cream-white graphic, black bindings"),
    "maja": dict(
        who="a small sporty Scandinavian woman in her early twenties (an adult, about 22), heart-shaped face, big wide-set bright green eyes, little upturned "
            "button nose, full round cheeks, freckles, light brown hair in two thick braids, cheeky playful grin",
        wear="a roomy padded magenta-pink one-piece ski suit with a white collar and a dark belt at the waist, white gloves, white ski boots",
        gear="a pair of very short skiblades (snowblades about a metre long) with a green top sheet and black details, no poles"),
    "linn": dict(
        who="a slim Scandinavian woman in her early twenties (an adult, about 23), the tallest of her friends but with the same compact stylised build, "
            "oval face, high cheekbones, pointed chin, straight nose, almond-shaped eyes tilted up at the outer corners, long auburn hair, "
            "cool confident look",
        wear="a sleek futuristic violet shell ski jacket with sharp seams and mint-green piping, slim mint-green ski pants, dark gloves, "
             "dark ski boots",
        gear="a pair of twin-tip freestyle skis with a yellow top sheet and pink graphics, short ski poles"),
}

BG = "on a flat even dusky purple background, no scenery, no text, no labels"
KINDS = {
    "views": (1536, 768, "character model sheet of the same person in three full-body views side by side: front view, side view "
                         "(profile facing right) and back view, identical outfit, standing straight in a relaxed neutral pose with the arms "
                         "slightly away from the body, standing on flat ground in ski boots, no skis, no snowboard, no goggles, no hat, "
                         "whole figure from head to boots in every view, " + BG),
    "apose": (1536, 768, "3D modelling reference sheet of the same person in an A-pose, three full-body views side by side: front view, "
                         "side view (profile facing right) and back view, arms held straight out and down about 35 degrees away from the "
                         "body with open hands, legs slightly apart, identical outfit, standing in ski boots on flat ground, no skis, "
                         "no snowboard, no goggles, no hat, whole figure from head to boots in every view, evenly lit, " + BG),
    "heads": (1344, 768, "head model sheet of the same person: three heads side by side, front view, three-quarter view and side profile, "
                         "head and neck only, same lighting, no hat, no helmet, no goggles, " + BG),
    "face": (1024, 768, "close-up portrait of the same person: head and shoulders, front view on the left and three-quarter view on the right, "
                        "no hat, no helmet, no goggles, no text, no labels"),
}

# Texture source for the 3D head: straight-on and in profile, evenly lit, so the drawn features can be painted onto the model.
TEXFACE = (1344, 768, "head model sheet for 3D texturing, two views of the same head side by side, on the left a straight-on front view "
                      "looking directly at the viewer, perfectly symmetrical, on the right an exact side profile facing right, head and "
                      "neck only, the whole head with all the hair and both ears, relaxed neutral expression, youthful smooth skin with no "
                      "wrinkles, no forehead lines, no stubble, even soft frontal lighting with no cast shadows on the face, clean bold "
                      "black ink outlines around the brows, eyes, nose, lips and jaw, flat colours, no hat, no goggles, " + BG)
TEXSTYLE = ("graphic novel ink art, bold clean black ink outlines, flat even colours, crisp line work, realistic young adult face, "
            "not cute, not chibi, not anime, not a child")

# Objects (skis, boards, helmets, prints): the same ink, but nobody in the picture.
OBJ = ("product design sheet drawn in a gritty noir graphic novel style, bold thick black ink outlines, flat colours with hard "
       "black shadow shapes, coarse brush hatching, no people, no person, no face, no character, only the object")
OBJ_SHORT = "product design sheet, noir graphic novel ink style, thick black outlines, flat colours, no people, only the object"

# What each one can unlock (from Scripts/Core/Unlocks.cs), and how it looks.
ITEMS = {
    "erik": [("race_stripes", "pattern", "bold racing stripes"), ("aero", "headgear", "a sleek aerodynamic time-trial ski racing helmet with a long tail"),
             ("racesuit", "suit", "a skin-tight downhill race suit in red and white with a padded collar"),
             ("viking", "headgear", "a Viking helmet with two curved horns over a ski helmet"),
             ("gold", "suit", "a shiny all-gold ski suit"),
             ("racing", "gear", "racing skis with a red and white top sheet and race numbers"), ("chrome", "gear", "mirror chrome skis"),
             ("goldskis", "gear", "solid gold skis")],
    "jonte": [("graffiti", "pattern", "spray-paint graffiti tags"), ("snapback", "headgear", "a flat-brim snapback cap worn backwards"),
              ("rainbow", "suit", "a rainbow-striped baggy snowboard suit"), ("tiger", "pattern", "orange and black tiger stripes"),
              ("flames", "pattern", "hot-rod flames"), ("goalie", "headgear", "an ice-hockey goalie mask"),
              ("boxer", "headgear", "a red boxing head guard"),
              ("zebra", "gear", "a snowboard with black and white zebra stripes"), ("lava", "gear", "a snowboard with glowing orange lava cracks"),
              ("galaxy", "gear", "a snowboard with a purple galaxy and stars")],
    "maja": [("candy", "pattern", "pink and white candy swirls"), ("bunny", "headgear", "a ski helmet with tall white bunny ears"),
             ("ginger", "suit", "a gingerbread-man costume ski suit with white icing trim"),
             ("banana", "suit", "a full-body yellow banana costume suit"),
             ("crown", "headgear", "a small golden crown on top of a ski helmet"),
             ("unicorn", "headgear", "a white ski helmet with a golden unicorn horn and a rainbow mane"),
             ("candycane", "gear", "skiblades striped red and white like candy canes"), ("ice", "gear", "translucent ice-blue skiblades"),
             ("diamond", "gear", "skiblades covered in sparkling diamonds")],
    "linn": [("skulls", "pattern", "small white skulls"), ("devil", "headgear", "a black ski helmet with small red devil horns"),
             ("midnight", "suit", "a midnight-black ski suit with thin gold trim"), ("pines", "pattern", "dark pine-tree silhouettes"),
             ("antlers", "headgear", "a ski helmet with reindeer antlers"), ("yeti", "suit", "a shaggy white yeti fur suit"),
             ("camo", "gear", "twin-tip skis in snow camouflage"), ("wolf", "gear", "twin-tip skis with a howling wolf graphic"),
             ("aurora", "gear", "twin-tip skis with green and violet aurora lights")],
}
# Everyone's free items, drawn for each (fit and colours differ).
FREE = [("helmet", "headgear", "a ski helmet in their colours with goggles on it"), ("beanie", "headgear", "a knitted beanie with a pom-pom"),
        ("stripes", "pattern", "horizontal stripes"), ("checks", "pattern", "a checkerboard"), ("dots", "pattern", "polka dots"),
        ("camo_print", "pattern", "snow camouflage")]


def item_prompt(who, kind, desc):
    p = PEOPLE[who]
    if kind == "suit":
        return ("character model sheet of the same person in two full-body views side by side, front and back, wearing %s instead of "
                "the usual clothes, standing straight, %s. The person: %s, %s." % (desc, BG, p["who"], PROPORTIONS))
    if kind == "pattern":
        return ("design sheet of a ski jacket and ski pants laid flat, front and back, printed all over with %s, the base colours of: %s, %s"
                % (desc, p["wear"], BG))
    if kind == "headgear":
        return ("design sheet of %s, front view, side view and three-quarter view, sized for a head, %s" % (desc, BG))
    return ("design sheet of %s, seen from above and from the side, full length, %s" % (desc, BG))


def jobs_for(people, only, seeds):
    jobs = []
    for who in people:
        p = PEOPLE[who]
        for kind, (w, h, frame) in KINDS.items():
            if only and kind != only:
                continue
            full = "%s. %s. The person: %s, %s, wearing %s. %s" % (NOIR, frame, p["who"], PROPORTIONS, p["wear"], NOIR)
            short = "%s, %s" % (SHORT, p["who"].split(",")[0])
            jobs.append(("%s_%s" % (who, kind), w, h, short, full, seeds))
        if not only or only == "gear":
            gear = p["gear"].split(", short ski poles")[0].split(", no poles")[0]
            full = ("%s. %s lying flat side by side, seen from above, full length from tip to tail, and below them the same seen "
                    "from the side showing the curve of the tips, %s. %s" % (OBJ, gear, BG, OBJ))
            jobs.append(("%s_gear" % who, 1344, 768, OBJ_SHORT + ", " + gear[:60], full, max(2, seeds - 1)))
        if only in ("texface", "objects"):
            w, h, frame = TEXFACE
            full = "%s. %s. The person: %s. %s" % (TEXSTYLE, frame, p["who"], TEXSTYLE)
            jobs.append(("%s_texface" % who, w, h, TEXSTYLE[:80] + ", " + p["who"].split(",")[0], full, 3))
        if not only or only in ("items", "objects"):
            for iid, kind, desc in ITEMS[who] + FREE:
                if only == "objects" and kind == "suit":
                    continue
                if kind == "suit":
                    full = "%s. %s %s" % (NOIR, item_prompt(who, kind, desc), NOIR)
                    short = SHORT + ", " + desc[:60]
                else:
                    full = "%s. %s %s" % (OBJ, item_prompt(who, kind, desc), OBJ)
                    short = OBJ_SHORT + ", " + desc[:60]
                jobs.append(("%s_item_%s" % (who, iid), 1344, 768, short, full, 1))
    return jobs


def main():
    args = sys.argv[1:]
    seeds = int(args[args.index("--seeds") + 1]) if "--seeds" in args else 3
    only = args[args.index("--only") + 1] if "--only" in args else None
    people = [a for a in args if a in PEOPLE] or list(PEOPLE)
    os.makedirs(OUT, exist_ok=True)
    jobs = jobs_for(people, only, seeds)
    pipe = base.load_pipe()
    encoded = []
    for name, w, h, short, full, n in jobs:
        pe, pooled = base.encode(pipe, short, full)
        encoded.append((name, w, h, pe, pooled, n))
    print("ENCODED", len(encoded), flush=True)
    pipe.text_encoder = None; pipe.text_encoder_2 = None
    gc.collect(); torch.cuda.empty_cache()
    pipe.vae.to("cuda")
    pipe.transformer.enable_group_offload(onload_device=torch.device("cuda"), offload_device=torch.device("cpu"),
                                          offload_type="block_level", num_blocks_per_group=6, use_stream=True)
    for name, w, h, pe, pooled, n in encoded:
        shots = []
        for k in range(n):
            seed = 2000 + k * 101 + len(name) * 7
            g = torch.Generator("cpu").manual_seed(seed)
            img = pipe(prompt_embeds=pe.to("cuda", torch.bfloat16), pooled_prompt_embeds=pooled.to("cuda", torch.bfloat16),
                       width=w, height=h, num_inference_steps=4, guidance_scale=0.0, generator=g).images[0]
            path = os.path.join(OUT, "%s_%d.png" % (name, seed) if n > 1 else "%s.png" % name)
            img.save(path)
            shots.append((seed, img))
            print("IMAGE", path, flush=True)
        if n > 1:
            tw, th = w // 2, h // 2
            sheet = Image.new("RGB", (tw * 2, th * ((n + 1) // 2)), (40, 40, 48))
            d = ImageDraw.Draw(sheet)
            for i, (seed, img) in enumerate(shots):
                x, y = (i % 2) * tw, (i // 2) * th
                sheet.paste(img.resize((tw, th)), (x, y))
                d.rectangle([x, y, x + 70, y + 22], fill=(20, 20, 24))
                d.text((x + 6, y + 5), str(seed), fill=(255, 255, 255))
            sheet.save(os.path.join(OUT, "%s_sheet.png" % name))
    print("DONE", flush=True)


if __name__ == "__main__":
    main()
