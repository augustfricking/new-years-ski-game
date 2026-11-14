using Ski.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// The set at the top of the lift: a snowy shoulder with the top station, a signpost for the
    /// five runs and the other three friends waiting. The viewer stands at the origin looking +Z,
    /// down the valley.
    /// </summary>
    public static class TopStation
    {
        public static float Ground(float x, float z)
        {
            float drop = Mathf.Max(0f, z - 9f);
            return -drop * drop * 0.012f - Mathf.Max(0f, drop - 30f) * 0.25f + 0.25f * Noise.Fbm(x / 13f, z / 13f, 5);
        }

        public static GameObject Build(Transform parent, CharacterId viewer)
        {
            var root = new GameObject("TopStation");
            root.transform.SetParent(parent, false);

            // Snow.
            const int n = 60; const float size = 280f;
            var v = new Vector3[(n + 1) * (n + 1)]; var c = new Color32[v.Length]; var nr = new Vector3[v.Length];
            for (int iz = 0; iz <= n; iz++)
                for (int ix = 0; ix <= n; ix++)
                {
                    // Finer near the viewer.
                    float fx = ix / (float)n * 2f - 1f, fz = iz / (float)n * 2f - 1f;
                    float x = Mathf.Sign(fx) * fx * fx * size * 0.5f, z = Mathf.Sign(fz) * fz * fz * size * 0.5f + 30f;
                    v[iz * (n + 1) + ix] = new Vector3(x, Ground(x, z), z);
                    Color32 col = Palette.Snow; col.a = 0;
                    c[iz * (n + 1) + ix] = col;
                }
            var tris = new int[n * n * 6];
            int t = 0;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    int a = iz * (n + 1) + ix, b = a + n + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = b + 1; tris[t++] = a; tris[t++] = b + 1; tris[t++] = a + 1;
                }
            var mesh = new Mesh { name = "TopSnow", vertices = v, colors32 = c, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var snow = new GameObject("Snow");
            snow.transform.SetParent(root.transform, false);
            snow.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = snow.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Snow; mr.shadowCastingMode = ShadowCastingMode.Off;

            var pb = new MeshBuilder();
            var tb = new MeshBuilder();
            var rng = new Rng(404);

            // Top station: cabin, last pylon, cable heading down the valley.
            pb.Push(new Vector3(-13f, Ground(-13f, -9f) - 0.1f, -9f), Quaternion.Euler(0, 90, 0));
            Props.Cabin(pb, new Vector3(8f, 3.4f, 9f), 1, false, Palette.TimberLight);
            pb.Pop();
            float[] pz = { 2f, 34f, 70f };
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            for (int i = 0; i < pz.Length; i++)
            {
                float z = pz[i], y = Ground(-10f, z);
                pb.Push(new Vector3(-10f, y, z));
                Props.Pylon(pb, 8.5f);
                pb.Pop();
                Vector3 l = new Vector3(-12.1f, y + 8.1f, z), r = new Vector3(-7.9f, y + 8.1f, z);
                if (i > 0) { pb.Col = Palette.Ink; pb.Tube(prevL, l, 0.03f, 4); pb.Tube(prevR, r, 0.03f, 4); }
                else { pb.Col = Palette.Ink; pb.Tube(l + new Vector3(0, 0, -12f), l, 0.03f, 4); pb.Tube(r + new Vector3(0, 0, -12f), r, 0.03f, 4); }
                prevL = l; prevR = r;
                if (i > 0)
                {
                    pb.Push(Vector3.Lerp(prevL, l, 0.5f) + new Vector3(0, 0, -14f));
                    Props.Chair(pb, false);
                    pb.Pop();
                }
            }

            // Signpost with the five runs.
            Vector3 post = new Vector3(3.4f, Ground(3.4f, 5.5f), 5.5f);
            pb.Push(post, Quaternion.Euler(0, -18, 0));
            pb.Col = Palette.TimberDark;
            pb.Cylinder(Vector3.zero, 0.09f, 0.08f, 3.3f, 6);
            for (int i = 0; i < 5; i++)
            {
                float y = 3.0f - i * 0.42f;
                int dir = i % 2 == 0 ? 1 : -1;
                pb.Col = Palette.Slope((SlopeId)i);
                pb.Box(new Vector3(dir * 0.62f, y, 0f), new Vector3(1.15f, 0.3f, 0.05f));
                pb.Col = Palette.Snow;
                pb.Box(new Vector3(dir * 0.62f, y + 0.17f, 0f), new Vector3(1.18f, 0.05f, 0.09f));
            }
            pb.Pop();
            for (int i = 0; i < 5; i++)
            {
                int dir = i % 2 == 0 ? 1 : -1;
                var rot = Quaternion.Euler(0, -18, 0);
                VillageBuilder.Sign(root.transform, Courses.Names[i], post + rot * new Vector3(dir * 0.62f, 3.0f - i * 0.42f, -0.035f), rot, 0.13f, Palette.White);
            }

            // Safety net along the drop and a fence behind.
            for (int i = 0; i < 3; i++)
            {
                float x = -8f + i * 6f;
                pb.Push(new Vector3(x, Ground(x, -5f), -5f));
                Props.Fence(pb, 6f);
                pb.Pop();
            }

            // Trees on both flanks and behind, framing the valley.
            for (int i = 0; i < 230; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), d = rng.Range(16f, 110f);
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d * 1.3f + 22f;
                if (Mathf.Abs(x) < 15f + Mathf.Max(0f, z) * 0.3f && z > -12f) continue; // keep the view open
                if (Mathf.Abs(x + 10f) < 5f && z > -6f) continue;                          // lift line cut
                tb.Push(new Vector3(x, Ground(x, z) - 0.15f, z), Quaternion.Euler(0, rng.Range(0f, 360f), 0), Vector3.one * rng.Range(0.9f, 1.8f));
                Props.Pine(tb, rng.Range(0, 3), rng);
                tb.Pop();
            }

            pb.ToObject("Station props", Mats.Props, root.transform);
            tb.ToObject("Station trees", Mats.Trees, root.transform, true, false);

            // The other three friends, standing about with their gear.
            int slot = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i == (int)viewer) continue;
                var p = Person.Create(PersonLook.For((CharacterId)i), root.transform, SkierInfo.Get((CharacterId)i).Name);
                float x = 1.6f + slot * 1.15f, z = 4.2f - slot * 0.55f;
                p.transform.localPosition = new Vector3(x, Ground(x, z), z);
                p.transform.localRotation = Quaternion.Euler(0, -150f + slot * 28f, 0);
                var pose = slot == 1 ? Pose.Stand.WithArms(10, 14, 30, 70, 30, 60) : Pose.Stand;
                p.Snap(pose);
                p.SetGogglesUp(true, true);
                p.PolesHang = true;
                slot++;
            }
            return root;
        }
    }
}
