// What the music is doing now: the track id, whether it plays, its clip, length and volume.
var a = Ski.Game.AudioDirector.I;
if (a == null) return "no audio";
var f = typeof(Ski.Game.AudioDirector).GetField("_music", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var src = typeof(Ski.Game.AudioDirector).GetField("_musicA", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(a) as UnityEngine.AudioSource;
string clip = src != null && src.clip != null ? src.clip.name + " " + src.clip.length.ToString("0.0") + " s" : "none";
return "music=" + f.GetValue(a) + " playing=" + (src != null && src.isPlaying) + " loop=" + (src != null && src.loop) + " clip=" + clip
    + " volume=" + (src != null ? src.volume.ToString("0.00") : "-") + " (theme level " + a.MusicVol.ToString("0.00") + ", songs " + a.SongVol.ToString("0.00") + ")";
