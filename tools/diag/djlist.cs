// Opens the DJ's request list in the tavern (to see every song fits). Run on the menu after the tavern has opened.
var tavern = UnityEngine.Object.FindFirstObjectByType<Ski.Game.TavernScene>();
if (tavern == null) { Ski.Game.SaveData.NameAsked = true; Ski.Game.Dialogs.Close(); Ski.Game.GameApp.Instance.ShowTavern(); return "opening the tavern: run again"; }
typeof(Ski.Game.TavernScene).GetMethod("OpenPicker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(tavern, null);
return "songs " + Ski.Game.Songs.All.Count;
