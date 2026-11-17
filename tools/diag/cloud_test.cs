using Ski.Game;
using UnityEngine;
// The cloud save and accounts, tried from the Editor. pose.txt: status | dirty | create <user> <pass> | signin <user> <pass> | signout | result
// (test accounts only: credentials come from tools/diag/.test_account, which git ignores).
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var dom = System.AppDomain.CurrentDomain;
switch (a[0])
{
    case "dirty": CloudSync.MarkDirty(); return "marked";
    case "create":
    case "signin":
    {
        dom.SetData("cloud_result", "pending");
        System.Func<System.Threading.Tasks.Task> go = async () =>
        {
            string err = await CloudSync.Account(a[1], a[2], a[0] == "create");
            dom.SetData("cloud_result", err ?? "ok");
        };
        go();
        return "started";
    }
    case "signout":
    {
        dom.SetData("cloud_result", "pending");
        System.Func<System.Threading.Tasks.Task> go = async () => { await CloudSync.SignOut(); dom.SetData("cloud_result", "signed out"); };
        go();
        return "started";
    }
    case "result": return "result=" + dom.GetData("cloud_result");
}
return "cloud=" + CloudSync.Status + " user=" + CloudSync.Username + " err=" + CloudSync.LastError + " player=" + OnlineBoards.PlayerId + " online=" + OnlineBoards.Status;
