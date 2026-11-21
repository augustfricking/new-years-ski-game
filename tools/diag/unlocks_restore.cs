// Puts back the Editor's save that tools/diag/unlocks.cs kept aside.
string backup = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/_unlocks_backup.txt");
if (!System.IO.File.Exists(backup)) return "nothing to restore";
var lines = System.IO.File.ReadAllText(backup).Split('\n');
UnityEngine.PlayerPrefs.SetString("progress_" + lines[0], lines[1]);
if (lines[2] == "<none>") UnityEngine.PlayerPrefs.DeleteKey("seen_unlocks"); else UnityEngine.PlayerPrefs.SetString("seen_unlocks", lines[2]);
UnityEngine.PlayerPrefs.Save();
Ski.Game.SaveData.Reload();
System.IO.File.Delete(backup);
return "restored";
