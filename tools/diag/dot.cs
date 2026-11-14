using UnityEngine;

var ps = Object.FindFirstObjectByType<ParticleSystem>();
var r = ps.GetComponent<ParticleSystemRenderer>();
var streams = new System.Collections.Generic.List<ParticleSystemVertexStream>();
r.GetActiveVertexStreams(streams);
var tex = (Texture2D)r.sharedMaterial.GetTexture("_BaseMap");
// Copy the texture to a readable one to inspect the alpha channel.
var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
Graphics.Blit(tex, rt);
var prev = RenderTexture.active;
RenderTexture.active = rt;
var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
copy.Apply();
RenderTexture.active = prev;
RenderTexture.ReleaseTemporary(rt);
var c = copy.GetPixel(32, 32); var e = copy.GetPixel(2, 2); var m = copy.GetPixel(16, 32);
return "streams=" + string.Join(",", streams) + " centre=" + c + " corner=" + e + " mid=" + m + " mode=" + r.renderMode + " size=" + ps.main.startSize.constantMin + ".." + ps.main.startSize.constantMax + " alignment=" + r.alignment + " meshes=" + r.meshCount;
