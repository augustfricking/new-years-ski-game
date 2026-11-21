// Opens the pause menu the way Escape does during a run (walkman on), for checking what the music does.
var app = Ski.Game.GameApp.Instance;
Ski.Game.SaveData.HasWalkman = true; Ski.Game.SaveData.WalkmanOn = true;
Ski.Game.AudioDirector.I.RefreshMusic();
if (app.Run == null) return "no run";
app.Run.Paused = true;
UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
Ski.Game.Dialogs.Pause(() => { app.Run.Paused = false; }, null, null, null);
return "paused";
