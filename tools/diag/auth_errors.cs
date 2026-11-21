// Asks the account service for three things it must refuse (no account is made): a weak password, a name
// with a space, and a sign-in with a name nobody has. Writes each error code and message to Screenshots/auth_errors.txt.
string file = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/auth_errors.txt");
System.IO.File.WriteAllText(file, "");
var auth = Unity.Services.Authentication.AuthenticationService.Instance;
void Note(string what, System.Threading.Tasks.Task t)
{
    t.ContinueWith(r =>
    {
        var e = r.Exception != null ? r.Exception.InnerException : null;
        string line = what + ": " + (e == null ? "OK (!)" : e.GetType().Name + " code=" + (e is Unity.Services.Core.RequestFailedException rf ? rf.ErrorCode.ToString() : "-") + " " + e.Message);
        lock (file) System.IO.File.AppendAllText(file, line + "\n");
    });
}
if (!auth.IsSignedIn) return "not signed in (state " + auth.IsSignedIn + ")";
Note("weak password", auth.AddUsernamePasswordAsync("lastrunprobe" + UnityEngine.Random.Range(1000, 9999), "abc"));
Note("name with a space", auth.AddUsernamePasswordAsync("bad name", "Abcdefg1!x"));
return "sent; signed in as " + auth.PlayerId + " username=" + (auth.PlayerInfo != null ? auth.PlayerInfo.Username : "-");
