using UnityEngine;
using UnityEngine.Rendering;

// Rough render cost of whatever is on screen: renderers, triangles and the smoothed frame time.
int renderers = 0; long tris = 0;
foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
{
    var r = mf.GetComponent<MeshRenderer>();
    if (r == null || !r.enabled || !mf.gameObject.activeInHierarchy || mf.sharedMesh == null) continue;
    renderers++;
    tris += mf.sharedMesh.triangles.Length / 3;
}
return Ski.Game.GameApp.Instance.Screen + ": renderers=" + renderers + " tris=" + tris + " frame=" + (Time.smoothDeltaTime * 1000f).ToString("0.0") + " ms (" + (1f / Time.smoothDeltaTime).ToString("0") + " fps) lights=" + Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
