using Unity.Services.Authentication;
using UnityEngine;

// Deletes the anonymous player that this Editor is signed in as (and with it the scores it sent).
// Use only to clean up after a development session that reached the world boards by mistake.
if (!AuthenticationService.Instance.IsSignedIn) return "not signed in";
string id = AuthenticationService.Instance.PlayerId;
AuthenticationService.Instance.DeleteAccountAsync().ContinueWith(t =>
    Debug.Log(t.IsFaulted ? "[Cleanup] delete failed: " + t.Exception.GetBaseException().Message : "[Cleanup] deleted editor player " + id));
return "deleting " + id;
