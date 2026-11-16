# Songs for the DJ

The seven songs are generated locally with **ACE-Step 1.5** (MIT licence; the generated music is
free to use). Lyrics and style descriptions are in `make_songs.py`; every song is original.

Setup (once, from the repository root, Git Bash):

    git clone --depth 1 https://github.com/ACE-Step/ACE-Step-1.5.git tools/music/_ace
    python -m pip install --user uv
    (cd tools/music/_ace && python -m uv sync)

The first run downloads the model files (several GB, Hugging Face) into `tools/music/_ace/checkpoints`.
On an 8 GB GPU it uses the 2B turbo model with the 0.6B language model and CPU offload.

Generate all songs, or some by id:

    tools/music/_ace/.venv/Scripts/python.exe tools/music/make_songs.py
    tools/music/_ace/.venv/Scripts/python.exe tools/music/make_songs.py rock pop

Output: `SkiGame/Assets/StreamingAssets/Music/<id>.mp3` and `songs.json`. The game streams them
from there when they are played, so they do not slow the start.
