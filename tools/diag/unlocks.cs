// Earns some of a friend's items in the Editor's save (kept aside first) and opens the shop, to look at the
// UNLOCKED cards and the shop's NEW tags. Run on the menu; then tools/diag/unlocks_restore.cs puts the save back.
// pose.txt: "who"  (Erik by default: 112 km/h and a 5.5 s stein earn his viking, lightning and electric blue)
string[] arg = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var who = arg.Length > 0 && arg[0].Length > 0 ? (Ski.Core.CharacterId)System.Enum.Parse(typeof(Ski.Core.CharacterId), arg[0]) : Ski.Core.CharacterId.Erik;
string backup = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/_unlocks_backup.txt");
if (!System.IO.File.Exists(backup))
    System.IO.File.WriteAllText(backup, (int)who + "\n" + UnityEngine.PlayerPrefs.GetString("progress_" + (int)who, "") + "\n"
        + (UnityEngine.PlayerPrefs.HasKey("seen_unlocks") ? UnityEngine.PlayerPrefs.GetString("seen_unlocks") : "<none>"));
Ski.Game.SaveData.NameAsked = true; Ski.Game.Dialogs.Close();
Ski.Game.SaveData.Record(who, p => { p.topSpeed = UnityEngine.Mathf.Max(p.topSpeed, 31.2f); p.bestChug = 5.5f; });
Ski.Game.ShopPanel.Open(Ski.Game.GameApp.Instance.transform, who, null);
return "pending " + Ski.Game.UnlockPopup.Pending + ", new " + Ski.Game.SaveData.NewCount(who);
