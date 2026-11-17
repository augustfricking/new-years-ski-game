using Ski.Game;
using UnityEngine;
// Signs in to a test account and deletes it (its cloud save first), then goes back to this
// device's own anonymous profile. pose.txt: "<user> <pass>" (from tools/diag/.test_account), or "result".
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var dom = System.AppDomain.CurrentDomain;
if (a[0] == "result") return "result=" + dom.GetData("delete_result");
dom.SetData("delete_result", "pending");
System.Func<System.Threading.Tasks.Task> go = async () =>
{
    try
    {
        await OnlineBoards.Connect();
        var auth = Unity.Services.Authentication.AuthenticationService.Instance;
        if (auth.IsSignedIn) auth.SignOut(true);
        await auth.SignInWithUsernamePasswordAsync(a[0], a[1]);
        string id = auth.PlayerId;
        try { await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.DeleteAsync("save", new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions()); }
        catch (System.Exception e) { Debug.Log("[Delete] save: " + e.Message); }
        await auth.DeleteAccountAsync();
        await auth.SignInAnonymouslyAsync();
        PlayerPrefs.Save();
        OnlineBoards.SignedInAgain();
        dom.SetData("delete_result", "deleted " + id + ", now " + auth.PlayerId);
    }
    catch (System.Exception e) { dom.SetData("delete_result", "failed: " + e.Message); }
};
go();
return "started";
