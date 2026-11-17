"""Downloads the image model for the concept art into tools/art/_models (not in git).

FLUX.1-schnell (Black Forest Labs, Apache 2.0): the text encoders, tokenizers, VAE and scheduler,
and the transformer as a 4-bit GGUF quantization (city96, same licence) so it fits an 8 GB GPU.
The official repository asks for a login, so the parts come from unsloth/FLUX.1-schnell, a
public copy whose files have the same checksums as the official ones (checked before use).
Only safetensors / gguf files: no pickled code.
"""
import os
from huggingface_hub import snapshot_download, hf_hub_download

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_models")
os.makedirs(ROOT, exist_ok=True)

flux = snapshot_download(
    "unsloth/FLUX.1-schnell",
    local_dir=os.path.join(ROOT, "flux-schnell"),
    allow_patterns=["model_index.json", "scheduler/*", "text_encoder/*", "text_encoder_2/*", "tokenizer/*", "tokenizer_2/*", "vae/*"],
)
print("FLUX", flux)
gguf = hf_hub_download("city96/FLUX.1-schnell-gguf", "flux1-schnell-Q4_K_S.gguf", local_dir=os.path.join(ROOT, "gguf"))
print("GGUF", gguf)
