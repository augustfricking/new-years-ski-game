using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// Builds vertex-coloured meshes from simple shapes. Set <see cref="Col"/> and <see cref="M"/>
    /// (the local transform), then add shapes. Everything procedural in the game goes through this.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Color32> _c = new List<Color32>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int> _t = new List<int>();
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public Matrix4x4 M = Matrix4x4.identity;
        public Color Col = Color.white;
        public int VertexCount => _v.Count;

        public MeshBuilder Colour(Color c) { Col = c; return this; }
        public void Push(Matrix4x4 local) { _stack.Push(M); M = M * local; }
        public void Push(Vector3 pos, Quaternion rot, Vector3 scale) => Push(Matrix4x4.TRS(pos, rot, scale));
        public void Push(Vector3 pos, Quaternion rot) => Push(Matrix4x4.TRS(pos, rot, Vector3.one));
        public void Push(Vector3 pos) => Push(Matrix4x4.Translate(pos));
        public void Pop() { M = _stack.Pop(); }

        int Vert(Vector3 p, Vector3 n, Vector2 uv = default)
        {
            _v.Add(M.MultiplyPoint3x4(p));
            _n.Add(M.MultiplyVector(n).normalized);
            _c.Add(Col);
            _uv.Add(uv);
            return _v.Count - 1;
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = Vert(a, n); Vert(b, n); Vert(c, n);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
        }

        /// <summary>Flat quad, corners in clockwise order seen from the front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i = Vert(a, n, new Vector2(0, 0)); Vert(b, n, new Vector2(0, 1)); Vert(c, n, new Vector2(1, 1)); Vert(d, n, new Vector2(1, 0));
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }

        public void DoubleQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Quad(a, b, c, d); Quad(d, c, b, a); }

        public void Box(Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = centre + new Vector3(-h.x, -h.y, -h.z), p001 = centre + new Vector3(-h.x, -h.y, h.z);
            Vector3 p010 = centre + new Vector3(-h.x, h.y, -h.z), p011 = centre + new Vector3(-h.x, h.y, h.z);
            Vector3 p100 = centre + new Vector3(h.x, -h.y, -h.z), p101 = centre + new Vector3(h.x, -h.y, h.z);
            Vector3 p110 = centre + new Vector3(h.x, h.y, -h.z), p111 = centre + new Vector3(h.x, h.y, h.z);
            Quad(p000, p010, p110, p100); // back (-z)
            Quad(p101, p111, p011, p001); // front (+z)
            Quad(p001, p011, p010, p000); // left
            Quad(p100, p110, p111, p101); // right
            Quad(p010, p011, p111, p110); // top
            Quad(p001, p000, p100, p101); // bottom
        }

        /// <summary>A box whose top face is scaled (a wedge-like tapered block).</summary>
        public void TaperedBox(Vector3 baseCentre, Vector3 baseSize, Vector2 topScale, float height, Vector3 topOffset = default)
        {
            Vector3 b = baseSize * 0.5f;
            float tx = b.x * topScale.x, tz = b.z * topScale.y;
            Vector3 t = baseCentre + Vector3.up * height + topOffset;
            Vector3 a0 = baseCentre + new Vector3(-b.x, 0, -b.z), a1 = baseCentre + new Vector3(-b.x, 0, b.z);
            Vector3 a2 = baseCentre + new Vector3(b.x, 0, b.z), a3 = baseCentre + new Vector3(b.x, 0, -b.z);
            Vector3 t0 = t + new Vector3(-tx, 0, -tz), t1 = t + new Vector3(-tx, 0, tz), t2 = t + new Vector3(tx, 0, tz), t3 = t + new Vector3(tx, 0, -tz);
            Quad(a0, t0, t3, a3); Quad(a2, t2, t1, a1); Quad(a1, t1, t0, a0); Quad(a3, t3, t2, a2);
            Quad(t0, t1, t2, t3); Quad(a1, a0, a3, a2);
        }

        /// <summary>Cylinder or cone frustum along +Y from the base centre.</summary>
        public void Cylinder(Vector3 baseCentre, float r0, float r1, float height, int sides = 8, bool smooth = true, bool caps = true, float twist = 0f)
        {
            float slope = (r0 - r1) / Mathf.Max(height, 1e-4f);
            int start = _v.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + twist;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 n = new Vector3(dir.x, slope, dir.z).normalized;
                if (smooth)
                {
                    Vert(baseCentre + dir * r0, n, new Vector2(i / (float)sides, 0));
                    Vert(baseCentre + dir * r1 + Vector3.up * height, n, new Vector2(i / (float)sides, 1));
                }
            }
            if (smooth)
            {
                for (int i = 0; i < sides; i++)
                {
                    int a = start + i * 2;
                    _t.Add(a); _t.Add(a + 1); _t.Add(a + 3);
                    _t.Add(a); _t.Add(a + 3); _t.Add(a + 2);
                }
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.PI * 2f + twist, a1 = (i + 1) / (float)sides * Mathf.PI * 2f + twist;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    Vector3 b0 = baseCentre + d0 * r0, b1 = baseCentre + d1 * r0;
                    Vector3 t0 = baseCentre + d0 * r1 + Vector3.up * height, t1 = baseCentre + d1 * r1 + Vector3.up * height;
                    if (r1 <= 1e-4f) Tri(b0, t0, b1); else Quad(b0, t0, t1, b1);
                }
            }
            if (caps)
            {
                if (r1 > 1e-4f) Disc(baseCentre + Vector3.up * height, r1, sides, true, twist);
                if (r0 > 1e-4f) Disc(baseCentre, r0, sides, false, twist);
            }
        }

        public void Disc(Vector3 centre, float r, int sides, bool up, float twist = 0f)
        {
            Vector3 n = up ? Vector3.up : Vector3.down;
            int c = Vert(centre, n);
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + twist;
                Vert(centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r, n);
            }
            for (int i = 0; i < sides; i++)
            {
                if (up) { _t.Add(c); _t.Add(c + 2 + i); _t.Add(c + 1 + i); }
                else { _t.Add(c); _t.Add(c + 1 + i); _t.Add(c + 2 + i); }
            }
        }

        /// <summary>Cylinder between two points.</summary>
        public void Tube(Vector3 a, Vector3 b, float r0, float r1, int sides = 6, bool caps = true)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            Push(a, Quaternion.FromToRotation(Vector3.up, d / len));
            Cylinder(Vector3.zero, r0, r1, len, sides, true, caps);
            Pop();
        }

        public void Tube(Vector3 a, Vector3 b, float r, int sides = 6) => Tube(a, b, r, r, sides);

        /// <summary>Ellipsoid.</summary>
        public void Sphere(Vector3 centre, Vector3 radii, int segments = 10, int rings = 6)
        {
            int start = _v.Count;
            for (int y = 0; y <= rings; y++)
            {
                float v = y / (float)rings, phi = v * Mathf.PI;
                for (int x = 0; x <= segments; x++)
                {
                    float u = x / (float)segments, th = u * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    Vector3 n = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z).normalized;
                    Vert(centre + Vector3.Scale(d, radii), n, new Vector2(u, v));
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < segments; x++)
                {
                    int a = start + y * (segments + 1) + x, b = a + segments + 1;
                    if (y > 0) { _t.Add(a); _t.Add(a + 1); _t.Add(b + 1); }
                    if (y < rings - 1) { _t.Add(a); _t.Add(b + 1); _t.Add(b); }
                }
        }

        public void Sphere(Vector3 centre, float r, int segments = 10, int rings = 6) => Sphere(centre, new Vector3(r, r, r), segments, rings);

        /// <summary>Rounded limb between two points: a tapered tube with a ball at each end.</summary>
        public void Capsule(Vector3 a, Vector3 b, float ra, float rb, int sides = 8)
        {
            Tube(a, b, ra, rb, sides, false);
            Sphere(a, ra, sides, 4);
            Sphere(b, rb, sides, 4);
        }

        /// <summary>Surface of revolution around +Y: profile points are (radius, height).</summary>
        public void Lathe(Vector3 origin, IList<Vector2> profile, int sides = 10, bool capTop = true, bool capBottom = true)
        {
            int start = _v.Count;
            for (int p = 0; p < profile.Count; p++)
            {
                Vector2 prev = profile[Mathf.Max(p - 1, 0)], next = profile[Mathf.Min(p + 1, profile.Count - 1)];
                Vector2 tan = (next - prev).normalized;
                Vector2 pn = new Vector2(tan.y, -tan.x); // outward for increasing height
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Vert(origin + dir * profile[p].x + Vector3.up * profile[p].y, new Vector3(dir.x * pn.x, pn.y, dir.z * pn.x), new Vector2(i / (float)sides, p / (float)(profile.Count - 1)));
                }
            }
            for (int p = 0; p < profile.Count - 1; p++)
                for (int i = 0; i < sides; i++)
                {
                    int a = start + p * (sides + 1) + i, b = a + sides + 1;
                    _t.Add(a); _t.Add(b); _t.Add(b + 1);
                    _t.Add(a); _t.Add(b + 1); _t.Add(a + 1);
                }
            if (capTop && profile[profile.Count - 1].x > 1e-4f) Disc(origin + Vector3.up * profile[profile.Count - 1].y, profile[profile.Count - 1].x, sides, true);
            if (capBottom && profile[0].x > 1e-4f) Disc(origin + Vector3.up * profile[0].y, profile[0].x, sides, false);
        }

        /// <summary>One cross-section of a <see cref="Loft"/>: an ellipse in the XZ plane at a height.</summary>
        public struct Ring
        {
            public float Y, Rx, Rz, OffX, OffZ;
            public Color Colour;
            public bool HasColour;
            /// <summary>Shape of the section: 2 (or 0) is an ellipse, higher is squarer (a jaw, a chest).</summary>
            public float Sq;
            public Ring Square(float n) { Sq = n; return this; }
            public float Exponent => Sq > 0.01f ? 2f / Sq : 1f;
            public Ring(float y, float rx, float rz, float offZ = 0f, float offX = 0f) { Y = y; Rx = rx; Rz = rz; OffZ = offZ; OffX = offX; Colour = default; HasColour = false; Sq = 0f; }
            public Ring Tint(Color c) { Colour = c; HasColour = true; return this; }
        }

        /// <summary>
        /// A smooth skin stretched over a stack of elliptical rings: the workhorse for organic
        /// shapes (torsos, limbs, heads). Rings go bottom to top along +Y; each may be shifted
        /// sideways for curvature and carry its own colour. Ends are closed with flat caps unless
        /// the end ring has zero size.
        /// </summary>
        public void Loft(IList<Ring> rings, int sides = 12, bool capBottom = true, bool capTop = true)
        {
            int n = rings.Count, w = sides + 1;
            var p = new Vector3[n * w];
            for (int i = 0; i < n; i++)
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a), e = rings[i].Exponent;
                    if (e != 1f) { cs = Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), e); sn = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e); }
                    p[i * w + k] = new Vector3(rings[i].OffX + cs * rings[i].Rx, rings[i].Y, rings[i].OffZ + sn * rings[i].Rz);
                }
            int start = _v.Count;
            Color keep = Col;
            for (int i = 0; i < n; i++)
            {
                if (rings[i].HasColour) Col = rings[i].Colour; else Col = keep;
                for (int k = 0; k <= sides; k++)
                {
                    // Normal from neighbouring points on the skin.
                    Vector3 up = p[Mathf.Min(i + 1, n - 1) * w + k] - p[Mathf.Max(i - 1, 0) * w + k];
                    Vector3 around = p[i * w + (k + 1) % sides] - p[i * w + (k + sides - 1) % sides];
                    Vector3 nrm = Vector3.Cross(up, around);
                    if (nrm.sqrMagnitude < 1e-10f)
                    {
                        float a = k / (float)sides * Mathf.PI * 2f;
                        nrm = i == 0 ? Vector3.down : i == n - 1 ? Vector3.up : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    }
                    Vert(p[i * w + k], nrm.normalized, new Vector2(k / (float)sides, i / (float)(n - 1)));
                }
            }
            Col = keep;
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * w + k, b = a + w;
                    _t.Add(a); _t.Add(b); _t.Add(b + 1);
                    _t.Add(a); _t.Add(b + 1); _t.Add(a + 1);
                }
            if (capBottom && rings[0].Rx > 1e-4f)
            {
                if (rings[0].HasColour) Col = rings[0].Colour;
                Vector3 c = new Vector3(rings[0].OffX, rings[0].Y, rings[0].OffZ);
                for (int k = 0; k < sides; k++) Tri(c, p[k], p[k + 1]);
                Col = keep;
            }
            if (capTop && rings[n - 1].Rx > 1e-4f)
            {
                if (rings[n - 1].HasColour) Col = rings[n - 1].Colour;
                Vector3 c = new Vector3(rings[n - 1].OffX, rings[n - 1].Y, rings[n - 1].OffZ);
                for (int k = 0; k < sides; k++) Tri(c, p[(n - 1) * w + k + 1], p[(n - 1) * w + k]);
                Col = keep;
            }
        }

        /// <summary>
        /// A pen stroke lying on a surface: a ribbon through the points, facing along the normals,
        /// thick in the middle and tapering to nothing at both ends, as a brush pen draws it.
        /// </summary>
        public void Stroke(IList<Vector3> pts, IList<Vector3> normals, float width)
        {
            int n = pts.Count;
            if (n < 2) return;
            Vector3 prevL = default, prevR = default;
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                float u = i / (float)(n - 1);
                float w = width * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.04f, 0.96f, u)), 0.7f);
                Vector3 side = Vector3.Cross(normals[i], t).normalized * w;
                Vector3 l = pts[i] - side, r = pts[i] + side;
                if (i > 0) Quad(prevL, l, r, prevR);
                prevL = l; prevR = r;
            }
        }

        /// <summary>Extrudes a 2D outline (in XY, counter-clockwise, convex) along Z.</summary>
        public void Prism(IList<Vector2> outline, float z0, float z1)
        {
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                Quad(new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1));
            }
            for (int i = 1; i < n - 1; i++)
            {
                Tri(new Vector3(outline[0].x, outline[0].y, z1), new Vector3(outline[i].x, outline[i].y, z1), new Vector3(outline[i + 1].x, outline[i + 1].y, z1));
                Tri(new Vector3(outline[0].x, outline[0].y, z0), new Vector3(outline[i + 1].x, outline[i + 1].y, z0), new Vector3(outline[i].x, outline[i].y, z0));
            }
        }

        /// <summary>Appends another mesh (for combining), transformed by <see cref="M"/> and tinted by <see cref="Col"/>.</summary>
        public void Append(Mesh mesh)
        {
            var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors32; var t = mesh.triangles;
            int start = _v.Count;
            Color32 tint = Col;
            bool hasCol = c != null && c.Length == v.Length;
            for (int i = 0; i < v.Length; i++)
            {
                _v.Add(M.MultiplyPoint3x4(v[i]));
                _n.Add(M.MultiplyVector(n[i]).normalized);
                Color32 src = hasCol ? c[i] : new Color32(255, 255, 255, 255);
                _c.Add(new Color32((byte)(src.r * tint.r / 255), (byte)(src.g * tint.g / 255), (byte)(src.b * tint.b / 255), src.a));
                _uv.Add(Vector2.zero);
            }
            for (int i = 0; i < t.Length; i++) _t.Add(start + t[i]);
        }

        public Mesh Build(string name, bool outlineNormals = true)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetColors(_c);
            mesh.SetUVs(0, _uv);
            mesh.SetTriangles(_t, 0);
            if (outlineNormals) mesh.SetUVs(3, SmoothNormals(_v, _n));
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>Per-vertex average of the normals of all vertices at the same position.</summary>
        public static List<Vector3> SmoothNormals(IList<Vector3> verts, IList<Vector3> normals)
        {
            var sum = new Dictionary<Vector3Int, Vector3>(verts.Count);
            var keys = new Vector3Int[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                var p = verts[i];
                var k = new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
                keys[i] = k;
                sum.TryGetValue(k, out var s);
                sum[k] = s + normals[i];
            }
            var result = new List<Vector3>(verts.Count);
            for (int i = 0; i < verts.Count; i++) result.Add(sum[keys[i]].normalized);
            return result;
        }

        /// <summary>Adds smoothed outline normals (UV3) to an imported mesh.</summary>
        public static void BakeOutlineNormals(Mesh mesh)
        {
            if (!mesh.isReadable) return;
            mesh.SetUVs(3, SmoothNormals(mesh.vertices, mesh.normals));
        }

        public void Clear() { _v.Clear(); _n.Clear(); _c.Clear(); _uv.Clear(); _t.Clear(); _stack.Clear(); M = Matrix4x4.identity; }

        /// <summary>Creates a GameObject showing the built mesh.</summary>
        public GameObject ToObject(string name, Material material, Transform parent = null, bool shadows = true, bool outlineNormals = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Build(name, outlineNormals);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }
}
