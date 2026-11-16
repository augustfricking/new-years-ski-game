using Ski.Core;
using Ski.Game;
using UnityEngine;

// Watches a replay from a world board, as the WATCH button on the map card does. Argument in
// Screenshots/pose.txt: the slope (Green, Blue...). Uses the first row with a recording.
var slope = (SlopeId)System.Enum.Parse(typeof(SlopeId), System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim());
var list = OnlineBoards.Cached(OnlineBoards.BoardId(slope));
if (list == null) { OnlineBoards.Fetch(OnlineBoards.BoardId(slope), null); return "fetching, run again"; }
foreach (var e in list)
{
    if (string.IsNullOrEmpty(e.Ghost)) continue;
    SaveData.NameAsked = true; Dialogs.Close();
    typeof(GameApp).GetMethod("WatchFromMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(GameApp.Instance, new object[] { slope, e });
    return "watching " + e.Name + " " + e.Score;
}
return "no recorded rows on " + slope + " (" + list.Count + " rows)";
