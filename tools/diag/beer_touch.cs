using Ski.Game;
using UnityEngine;

// The beer game as a phone shows it (Device.ForceTouch). pose.txt: "start" (the game running) or "slow" (the
// too-slow card, which saves nothing). Set Device.ForceTouch back to false afterwards.
string arg = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim();
var app = GameApp.Instance;
if (arg == "slow")
{
    if (app.Tavern == null || app.Tavern.Game == null) return "no game";
    typeof(BeerGame).GetMethod("TooSlow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(app.Tavern.Game, null);
    return "too slow card";
}
Device.ForceTouch = true;
SaveData.NameAsked = true; Dialogs.Close();
if (app.Tavern == null || !app.Tavern.gameObject.activeInHierarchy) app.ShowTavern();
app.Tavern.StartGame();
return "beer game on touch";
