// Which Play does AudioDirector.Click reach: the sound effect (4 parameters, 2 optional) or the music (2 parameters)?
// Calls Click with a fresh "input" so it is not held back, and reports what the music became.
var a = Ski.Game.AudioDirector.I;
var t = typeof(Ski.Game.AudioDirector);
var musicField = t.GetField("_music", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
t.GetField("_lastInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(a, UnityEngine.Time.unscaledTime);
string before = (string)musicField.GetValue(a);
Ski.Game.AudioDirector.Click();
string after = (string)musicField.GetValue(a);
return "music before click: " + before + "  after: " + after;
