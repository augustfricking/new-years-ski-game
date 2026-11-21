// During a run that has finished (in the village): keeps the save aside, earns Erik's viking, lightning and
// electric blue, for looking at the UNLOCKED cards and the NEW IN SHOP reminder. tools/diag/unlocks_restore.cs puts it back.
var who = Ski.Core.CharacterId.Erik;
string backup = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/_unlocks_backup.txt");
if (!System.IO.File.Exists(backup))
    System.IO.File.WriteAllText(backup, (int)who + "\n" + UnityEngine.PlayerPrefs.GetString("progress_" + (int)who, "") + "\n"
        + (UnityEngine.PlayerPrefs.HasKey("seen_unlocks") ? UnityEngine.PlayerPrefs.GetString("seen_unlocks") : "<none>"));
Ski.Game.SaveData.Record(who, p => { p.topSpeed = UnityEngine.Mathf.Max(p.topSpeed, 31.2f); p.bestChug = 5.5f; });
var run = Ski.Game.GameApp.Instance.Run;
return "finished " + (run != null && run.Result.HasValue) + ", pending " + Ski.Game.UnlockPopup.Pending + ", new " + Ski.Game.SaveData.NewCount(who);
