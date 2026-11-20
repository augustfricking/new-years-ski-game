"""Concept art of the four friends, made locally with FLUX.1-schnell (Apache 2.0).

Each character gets a turnaround (front and side, full figure) and face close-ups, in a noir
graphic-novel look: heavy hand-inked outlines, hatching in the shadows, flat colour with hard
shadow shapes, adult proportions. The prompts describe the style, never another game's
characters. Several seeds per picture; the owner picks before anything is built in 3D.

  tools/art/_venv/Scripts/python.exe -I tools/art/make_art.py [who ...] [--seeds N] [--kind sheet|face]

Output: tools/art/_out/<who>_<kind>_<seed>.png (not in git) and a contact sheet per character.
"""
import os
import sys
import gc
import torch
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
MODELS = os.path.join(HERE, "_models")
OUT = os.path.join(HERE, "_out")
FLUX = os.path.join(MODELS, "flux-schnell")
GGUF = os.path.join(MODELS, "gguf", "flux1-schnell-Q4_K_S.gguf")

STYLE = ("mature noir graphic novel illustration, realistic adult anatomy and proportions, inked by hand with thick confident "
         "black brush lines, large spotted black shadow shapes on one side of the face and body, fine cross-hatching in the "
         "half-tones, two-tone cel shading with hard shadow edges, painterly flat colour fills, muted desaturated wintry palette "
         "with a few saturated accents, dramatic side lighting, gritty and grounded, not cute, not anime, not a cartoon")
# The CLIP encoder reads only 77 tokens: it gets the style and the gist, the T5 encoder gets everything.
SHORT = ("mature noir graphic novel illustration, hand-inked thick black brush lines, spotted black shadows, cross-hatching, "
         "two-tone cel shading, muted palette, realistic adult proportions")

PEOPLE = {
    "erik": dict(
        who="a tall athletic Scandinavian man around 30, long angular face, square jaw with a cleft chin, heavy straight brows, "
            "hooded grey-blue eyes, long straight nose with a slight bump, short swept sandy-blond hair, confident half smile",
        wear="a retro 1980s one-colour orange ski jacket with a broad navy-blue band across the chest and sleeves, navy ski pants, "
             "black gloves, orange ski boots, ski goggles pushed up on his forehead"),
    "jonte": dict(
        who="a big broad-shouldered Scandinavian snowboarder around 28, round fleshy face with full cheeks and a soft double chin, "
            "small friendly dark eyes under a heavy brow, wide flat upturned nose, big ears, short messy dark-brown hair, stubble, "
            "a belly, thick neck, easy-going grin",
        wear="a baggy oversized teal snowboard jacket, very baggy mustard-yellow snowboard pants bunched over the boots, "
             "dark grey gloves, black snowboard boots, a black beanie pushed back"),
    "maja": dict(
        who="a small petite Scandinavian woman around 26, heart-shaped face, big wide-set bright green eyes, a little upturned button nose, "
            "full round cheeks, full lips, freckles, light brown hair in two thick braids, cheeky smile",
        wear="a fitted pink one-piece ski suit with a white collar and a dark belt at the waist, white gloves, white ski boots, "
             "lavender goggles on her forehead"),
    "linn": dict(
        who="a tall slim Scandinavian woman around 29, long oval face, high sharp cheekbones over hollow cheeks, a pointed chin, "
            "a long straight nose, almond-shaped eyes tilted up at the outer corners, long auburn hair, long neck, cool serious look",
        wear="a sleek futuristic lilac-purple shell ski jacket with sharp seams and mint-green piping, slim mint-green ski pants, "
             "dark gloves, dark ski boots, mint goggles on her forehead"),
}

KINDS = {
    "sheet": (1344, 768, "character turnaround sheet of the same person: full body front view on the left and full body side view "
                         "(profile, facing right) on the right, standing straight, arms relaxed, whole figure from head to boots visible, "
                         "no text, no labels"),
    "face": (1024, 768, "close-up portrait sheet of the same person: head and neck only, front view on the left and three-quarter view on the right, "
                        "the face fills the frame, no hat, no helmet, no goggles, no text, no labels"),
}


def load_pipe():
    from diffusers import FluxPipeline, FluxTransformer2DModel, GGUFQuantizationConfig
    transformer = FluxTransformer2DModel.from_single_file(
        GGUF, quantization_config=GGUFQuantizationConfig(compute_dtype=torch.bfloat16),
        torch_dtype=torch.bfloat16, config=FLUX, subfolder="transformer")
    pipe = FluxPipeline.from_pretrained(FLUX, transformer=transformer, torch_dtype=torch.bfloat16)
    return pipe


def encode(pipe, short, full):
    """The text encoders run on the CPU (the big one would not fit beside the image model)."""
    pipe.text_encoder.to("cpu"); pipe.text_encoder_2.to("cpu")
    with torch.no_grad():
        pe, pooled, _ = pipe.encode_prompt(prompt=short, prompt_2=full, device="cpu", max_sequence_length=512)
    return pe, pooled


def main():
    args = sys.argv[1:]
    seeds = int(args[args.index("--seeds") + 1]) if "--seeds" in args else 4
    kinds = [args[args.index("--kind") + 1]] if "--kind" in args else list(KINDS)
    people = [a for a in args if a in PEOPLE] or list(PEOPLE)
    os.makedirs(OUT, exist_ok=True)
    pipe = load_pipe()
    # Prompts first, on the CPU; then the encoders go and the image model takes the GPU.
    jobs = []
    for who in people:
        for kind in kinds:
            w, h, frame = KINDS[kind]
            p = PEOPLE[who]
            full = "%s. %s. The person: %s, wearing %s. %s" % (STYLE, frame, p["who"], p["wear"], STYLE)
            short = "%s, %s" % (SHORT, p["who"].split(",")[0])
            pe, pooled = encode(pipe, short, full)
            jobs.append((who, kind, w, h, pe, pooled))
            print("ENCODED", who, kind, flush=True)
    pipe.text_encoder = None; pipe.text_encoder_2 = None
    gc.collect()
    pipe.vae.to("cuda")
    pipe.transformer.to("cuda")
    for who, kind, w, h, pe, pooled in jobs:
        shots = []
        for k in range(seeds):
            seed = 1000 + k * 77 + len(who) * 13 + (0 if kind == "sheet" else 5)
            g = torch.Generator("cpu").manual_seed(seed)
            img = pipe(prompt_embeds=pe.to("cuda", torch.bfloat16), pooled_prompt_embeds=pooled.to("cuda", torch.bfloat16),
                       width=w, height=h, num_inference_steps=4, guidance_scale=0.0, generator=g).images[0]
            path = os.path.join(OUT, "%s_%s_%d.png" % (who, kind, seed))
            img.save(path)
            shots.append((seed, img))
            print("IMAGE", path, flush=True)
        # A contact sheet, numbered by seed.
        tw, th = w // 2, h // 2
        sheet = Image.new("RGB", (tw * 2, th * ((len(shots) + 1) // 2)), (40, 40, 48))
        d = ImageDraw.Draw(sheet)
        for i, (seed, img) in enumerate(shots):
            x, y = (i % 2) * tw, (i // 2) * th
            sheet.paste(img.resize((tw, th)), (x, y))
            d.rectangle([x, y, x + 70, y + 22], fill=(20, 20, 24))
            d.text((x + 6, y + 5), str(seed), fill=(255, 255, 255))
        sheet.save(os.path.join(OUT, "%s_%s_sheet.png" % (who, kind)))
    print("DONE", flush=True)


if __name__ == "__main__":
    main()
