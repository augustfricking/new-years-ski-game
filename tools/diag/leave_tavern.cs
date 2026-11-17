using Ski.Game;
using UnityEngine;
// Walks into the tavern and straight out of its door again: you should be in the village of the
// last run (Barnbacken if none), outside the tavern, run over.
SaveData.NameAsked = true; Dialogs.Close();
var app = GameApp.Instance;
app.ShowTavern();
app.Tavern.OnLeave();
return "left the tavern";
