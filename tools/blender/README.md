# Character models

The skiers' bodies are sculpted in Blender, by script, from Blender Studio's **Human Base Meshes**
(CC0, https://www.blender.org/download/demo-files/ > Asset Bundles). The bundle is not in git:

    mkdir -p tools/blender/_downloads/human-base-meshes
    curl -L -o hbm.zip https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.2.0.zip
    # unzip so that tools/blender/_downloads/human-base-meshes/human_base_meshes_bundle.blend exists

Then, from the repository root (Git Bash, Blender 5.x):

    sh tools/blender/build_all.sh

`build_hero.py` pushes the realistic body into comic proportions (one profile per character, at the
top of the file), lifts a puffer jacket, ski pants and gloves off the body's own surface, closes
the hands, weights everything to the game's fifteen joints, makes the face expressions and writes
`SkiGame/Assets/_Game/Resources/Models/<name>.bytes`. `SkinModel.cs` reads those files and `Person`
wears them. Preview renders go to `SkiGame/Screenshots/blender/`.

`inspect_bundle.py` and `explore_body.py` list what is in the bundle and how a body is laid out.
