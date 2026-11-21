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

## YuE2 versions (for comparing)

`make_songs_yue2.py` makes the same songs (titles, lyrics) with **YuE2** (`m-a-p/YuE2-3B`): code
Apache 2.0, weights CC BY-NC 4.0 with a grant to individual creators to use them and publish the
songs (`_yue2/MODEL_LICENSE`; credit "Music made with YuE2"). Setup is in the script's header. On an
8 GB GPU it runs with `low_vram` and is slow. Output goes to `_work/compare/<id>_yue2.mp3`, next to
the ACE-Step versions (`<id>_ace.mp3`); `--install` puts them in the game instead.
