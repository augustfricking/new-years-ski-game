using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// A sculpted, dressed body made in Blender (tools/blender/build_hero.py) and stored in
    /// Resources/Models as a small binary file: skinned parts (skin, jacket, pants, gloves), the
    /// skeleton they are bound to, face landmarks and the outline of the skull. Colours are not in
    /// the file: every vertex names a slot, and each character paints the slots its own way.
    /// Coordinates are the game's: metres, y up, facing +z, the character's left at -x.
    /// </summary>
    public sealed class SkinModel
    {
        public const int Hips = 0, Torso = 1, Head = 2, ShoulderL = 3, ElbowL = 4, HandL = 5, ShoulderR = 6, ElbowR = 7, HandR = 8,
            HipL = 9, KneeL = 10, FootL = 11, HipR = 12, KneeR = 13, FootR = 14, BoneCount = 15;
        public const int SlotSkin = 0, SlotLip = 1, SlotMouth = 2, SlotBrow = 3, SlotJacket = 4, SlotTrim = 5, SlotPants = 6, SlotGlove = 7, SlotInk = 8, SlotTeeth = 9, SlotCount = 10;

        public sealed class Key { public string Name; public int[] Index; public Vector3[] Delta; }
        public sealed class Part
        {
            public string Name;
            public Vector3[] Vertices, Normals;
            public byte[] Slot, Crease;
            public BoneWeight[] Weights;
            public int[] Triangles;
            public Key[] Keys;
        }

        public float Height, HeadRadius, EyeRadius, MouthHalf;
        public Vector3[] Joints;
        public Vector3 EyeL, EyeR, Mouth, Nose, EarL, GripL, PalmL;
        /// <summary>The skull at a row of heights: x = height above the head joint, y = half width, z = front, w = back (all relative to the head joint).</summary>
        public Vector4[] Rings;
        public Part[] Parts;

        static readonly Dictionary<string, SkinModel> Cache = new Dictionary<string, SkinModel>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Cache.Clear();

        /// <summary>The model of that name, or null when there is none (the caller falls back to simple shapes).</summary>
        public static SkinModel Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var m)) return m;
            var asset = Resources.Load<TextAsset>("Models/" + name);
            if (asset != null)
            {
                try { m = Parse(asset.bytes); }
                catch (System.Exception e) { Debug.LogWarning("Model " + name + " could not be read: " + e.Message); m = null; }
            }
            Cache[name] = m;
            return m;
        }

        static Vector3 V3(BinaryReader r) { float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(); return new Vector3(x, y, z); }
        static string Str(BinaryReader r) { int n = r.ReadInt32(); return System.Text.Encoding.ASCII.GetString(r.ReadBytes(n)); }

        static SkinModel Parse(byte[] bytes)
        {
            using var r = new BinaryReader(new MemoryStream(bytes));
            if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(4)) != "SKM3") throw new InvalidDataException("not SKM3");
            var m = new SkinModel { Height = r.ReadSingle() };
            int bones = r.ReadInt32();
            m.Joints = new Vector3[bones];
            for (int i = 0; i < bones; i++) m.Joints[i] = V3(r);
            m.HeadRadius = r.ReadSingle();
            m.EyeL = V3(r); m.EyeR = V3(r); m.EyeRadius = r.ReadSingle();
            m.Mouth = V3(r); m.MouthHalf = r.ReadSingle();
            m.Nose = V3(r); m.EarL = V3(r); m.GripL = V3(r); m.PalmL = V3(r);
            m.Rings = new Vector4[r.ReadInt32()];
            for (int i = 0; i < m.Rings.Length; i++) m.Rings[i] = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            m.Parts = new Part[r.ReadInt32()];
            for (int p = 0; p < m.Parts.Length; p++)
            {
                var part = m.Parts[p] = new Part { Name = Str(r) };
                int n = r.ReadInt32();
                Vector3 lo = V3(r), size = V3(r);
                part.Vertices = new Vector3[n];
                for (int i = 0; i < n; i++)
                    part.Vertices[i] = new Vector3(lo.x + r.ReadUInt16() / 65535f * size.x, lo.y + r.ReadUInt16() / 65535f * size.y, lo.z + r.ReadUInt16() / 65535f * size.z);
                part.Slot = new byte[n]; part.Crease = new byte[n];
                for (int i = 0; i < n; i++) { part.Slot[i] = r.ReadByte(); part.Crease[i] = r.ReadByte(); }
                part.Weights = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    var w = new BoneWeight();
                    w.boneIndex0 = r.ReadByte(); w.weight0 = r.ReadByte() / 255f;
                    w.boneIndex1 = r.ReadByte(); w.weight1 = r.ReadByte() / 255f;
                    w.boneIndex2 = r.ReadByte(); w.weight2 = r.ReadByte() / 255f;
                    w.boneIndex3 = r.ReadByte(); w.weight3 = r.ReadByte() / 255f;
                    part.Weights[i] = w;
                }
                part.Triangles = new int[r.ReadInt32()];
                for (int i = 0; i < part.Triangles.Length; i++) part.Triangles[i] = r.ReadUInt16();
                part.Keys = new Key[r.ReadInt32()];
                for (int k = 0; k < part.Keys.Length; k++)
                {
                    var key = part.Keys[k] = new Key { Name = Str(r) };
                    float scale = r.ReadSingle() / 32767f;
                    int count = r.ReadInt32();
                    key.Index = new int[count]; key.Delta = new Vector3[count];
                    for (int i = 0; i < count; i++)
                    {
                        key.Index[i] = r.ReadUInt16();
                        key.Delta[i] = new Vector3(r.ReadInt16() * scale, r.ReadInt16() * scale, r.ReadInt16() * scale);
                    }
                }
                // Smooth normals, worked out once.
                var tmp = new Mesh();
                tmp.SetVertices(part.Vertices);
                tmp.SetTriangles(part.Triangles, 0);
                tmp.RecalculateNormals();
                part.Normals = tmp.normals;
                Object.DestroyImmediate(tmp);
            }
            return m;
        }

        /// <summary>
        /// One part as a mesh in a character's own colours and size. Creases are darkened toward
        /// ink, the way a comic artist shades folds. Expressions become blend shapes when asked for.
        /// </summary>
        /// <param name="printed">Slots that carry a printed pattern: their faces go to a second sub-mesh, white, with <paramref name="printUv"/> for the texture.</param>
        public Mesh Build(Part part, Color[] slots, float scale, Matrix4x4[] bindPoses, bool expressions, System.Func<Vector3, int, Color, Color> tint = null,
                          System.Func<int, bool> printed = null, System.Func<Vector3, int, Vector2> printUv = null)
        {
            int n = part.Vertices.Length;
            var v = new Vector3[n];
            var c = new Color32[n];
            var uv = printUv != null ? new Vector2[n] : null;
            for (int i = 0; i < n; i++)
            {
                v[i] = part.Vertices[i] * scale;
                Color col = slots[Mathf.Min(part.Slot[i], SlotCount - 1)];
                if (tint != null) col = tint(part.Vertices[i], part.Slot[i], col);
                if (printed != null && printed(part.Slot[i])) { col = Color.white; uv[i] = printUv(part.Vertices[i], part.Slot[i]); }
                float crease = part.Crease[i] / 255f;
                // Deep creases are drawn in, like an inker's line: eye sockets, nose wings, the corners
                // of the mouth, the folds of the cloth. Shallow ones stay clean.
                float line = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(part.Slot[i] == SlotSkin ? 0.32f : 0.4f, part.Slot[i] == SlotSkin ? 0.55f : 0.65f, crease));
                float shade = crease * crease * (part.Slot[i] == SlotSkin ? 0.12f : 0.25f);
                c[i] = Color.Lerp(col, Palette.Ink, Mathf.Max(shade, line * (part.Slot[i] == SlotSkin ? 0.7f : 0.8f)));
            }
            var mesh = new Mesh { name = part.Name };
            mesh.SetVertices(v);
            mesh.SetNormals(part.Normals);
            mesh.SetColors(c);
            if (printed == null) mesh.SetTriangles(part.Triangles, 0);
            else
            {
                // Faces wholly on printed cloth take the print; the rest stay plain.
                mesh.SetUVs(0, uv);
                var plain = new System.Collections.Generic.List<int>(part.Triangles.Length);
                var print = new System.Collections.Generic.List<int>(part.Triangles.Length);
                var t = part.Triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    bool p = printed(part.Slot[t[i]]) && printed(part.Slot[t[i + 1]]) && printed(part.Slot[t[i + 2]]);
                    var list = p ? print : plain;
                    list.Add(t[i]); list.Add(t[i + 1]); list.Add(t[i + 2]);
                }
                mesh.subMeshCount = 2;
                mesh.SetTriangles(plain, 0);
                mesh.SetTriangles(print, 1);
            }
            mesh.boneWeights = part.Weights;
            mesh.bindposes = bindPoses;
            if (expressions)
            {
                var delta = new Vector3[n];
                foreach (var key in part.Keys)
                {
                    System.Array.Clear(delta, 0, n);
                    for (int i = 0; i < key.Index.Length; i++) delta[key.Index[i]] = key.Delta[i] * scale;
                    mesh.AddBlendShapeFrame(key.Name, 100f, delta, null, null);
                }
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        // ---- the skull, for fitting things to it ---------------------------------------------------

        Vector4 RingAt(float y)
        {
            if (y <= Rings[0].x) return Rings[0];
            for (int i = 0; i < Rings.Length - 1; i++)
                if (y <= Rings[i + 1].x) return Vector4.Lerp(Rings[i], Rings[i + 1], Mathf.InverseLerp(Rings[i].x, Rings[i + 1].x, y));
            return Rings[Rings.Length - 1];
        }

        /// <summary>Half the width of the skull at a height above the head joint (model metres).</summary>
        public float HalfWidth(float y) => RingAt(y).y;
        /// <summary>The front of the skull (beside the nose) at a height above the head joint.</summary>
        public float Front(float y) => RingAt(y).z;
        public float Back(float y) => RingAt(y).w;
        /// <summary>The top of the head, above the head joint.</summary>
        public float Crown => Height - Joints[Head].y;
    }
}
