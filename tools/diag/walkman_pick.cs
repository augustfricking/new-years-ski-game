// Plays one song on the walkman: pose.txt holds its id.
string id = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim();
Ski.Game.Dialogs.Close();
Ski.Game.Walkman.Select(id);
return Ski.Game.Walkman.Current;
