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
# A darker take: closer to a noir crime comic, half the face in solid black shadow.
NOIR = ("gritty 1980s noir crime graphic novel panel, heavy chiaroscuro, half of the face in solid flat black shadow with a hard "
        "edge, bold thick black ink outlines, coarse brush hatching, painterly textured colour with visible brush strokes, "
        "dramatic coloured rim light, muted dusky palette of purples and teals with warm skin, realistic adult anatomy, "
        "stern grounded expression, not cute, not anime, not a cartoon, not a pin-up")
NOIR_SHORT = ("gritty noir crime graphic novel, heavy chiaroscuro, solid black shadows, thick ink outlines, painterly colour, "
              "realistic adult")

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
            "small friendly dark eyes under a heavy brow, wide flat upturned nose, big ears, short messy dark-brown hair, light stubble, no beard, "
            "a belly, thick neck, easy-going grin",
        wear="a baggy oversized teal snowboard jacket, very baggy mustard-yellow snowboard pants bunched over the boots, "
             "dark grey gloves, black snowboard boots, a black beanie pushed back"),
    "maja": dict(
        who="a small sporty Scandinavian woman around 26, short and compact, heart-shaped face, big wide-set bright green eyes, a little upturned button nose, "
            "full round cheeks, full lips, freckles, light brown hair in two thick braids, cheeky smile",
        wear="a roomy padded pink one-piece ski suit (not tight) with a white collar and a dark belt at the waist, white gloves, white ski boots, "
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
    """The text encoders run on the GPU a layer at a time (the big one would not fit whole beside the
    image model; on this CPU bf16 is very slow). Results are kept on disk: each prompt is encoded once."""
    import hashlib
    key = hashlib.sha1((short + "|" + full).encode("utf-8")).hexdigest()[:16]
    path = os.path.join(OUT, "_emb", key + ".pt")
    if os.path.exists(path):
        d = torch.load(path, weights_only=True)
        return d["pe"], d["pooled"]
    from accelerate import cpu_offload
    if not getattr(pipe, "_offloaded", False):
        cpu_offload(pipe.text_encoder_2, execution_device=torch.device("cuda"))
        pipe.text_encoder.to("cuda")
        pipe._offloaded = True
    with torch.no_grad():
        pe, pooled, _ = pipe.encode_prompt(prompt=short, prompt_2=full, device="cuda", max_sequence_length=512)
    pe, pooled = pe.cpu(), pooled.cpu()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    torch.save({"pe": pe, "pooled": pooled}, path)
    return pe, pooled


def main():
    args = sys.argv[1:]
    seeds = int(args[args.index("--seeds") + 1]) if "--seeds" in args else 4
    kinds = [args[args.index("--kind") + 1]] if "--kind" in args else list(KINDS)
    people = [a for a in args if a in PEOPLE] or list(PEOPLE)
    noir = "--noir" in args
    style, short_style, tag = (NOIR, NOIR_SHORT, "noir") if noir else (STYLE, SHORT, "")
    os.makedirs(OUT, exist_ok=True)
    pipe = load_pipe()
    # Prompts first, on the CPU; then the encoders go and the image model takes the GPU.
    jobs = []
    for who in people:
        for kind in kinds:
            w, h, frame = KINDS[kind]
            p = PEOPLE[who]
            wear = p["wear"]
            if kind == "face":                                       # portraits: no goggles or hats in the way
                wear = ", ".join(x for x in wear.split(", ") if "goggles" not in x and "beanie" not in x)
            full = "%s. %s. The person: %s, wearing %s. %s" % (style, frame, p["who"], wear, style)
            short = "%s, %s" % (short_style, p["who"].split(",")[0])
            pe, pooled = encode(pipe, short, full)
            jobs.append((who, kind + tag, w, h, pe, pooled))
            print("ENCODED", who, kind, flush=True)
    pipe.text_encoder = None; pipe.text_encoder_2 = None
    gc.collect(); torch.cuda.empty_cache()
    pipe.vae.to("cuda")
    # The 4-bit image model is 6.8 GB: next to the desktop it does not quite fit, and spilling into
    # shared memory is very slow. So its blocks are streamed to the GPU a few at a time.
    pipe.transformer.enable_group_offload(onload_device=torch.device("cuda"), offload_device=torch.device("cpu"),
                                          offload_type="block_level", num_blocks_per_group=6, use_stream=True)
    for who, kind, w, h, pe, pooled in jobs:
        shots = []
        for k in range(seeds):
            seed = 1000 + k * 77 + len(who) * 13 + (0 if kind.startswith("sheet") else 5)
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
