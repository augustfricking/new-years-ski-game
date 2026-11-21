// Goes to the ski map the way the pause menu's SKI MAP does (for checking that the walkman plays on).
var app = Ski.Game.GameApp.Instance;
Ski.Game.SaveData.NameAsked = true;
Ski.Game.Dialogs.Close();
if (app.Run != null) app.Run.Paused = false;
app.Transition(app.ShowMap);
return "to map";
