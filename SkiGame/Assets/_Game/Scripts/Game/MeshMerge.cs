using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// Folds the many small meshes of a figure (head, limbs, boots, skis, each on its own joint, and the
    /// sculpt's skinned parts) into one skinned mesh with a sub-mesh per material, so a person in the crowd
    /// costs a draw per material instead of one per part, in every pass (shadows, depth, colour). A rigid
    /// part becomes one bone (its joint); a skinned part keeps its bones and bind poses. Parts that print a
    /// pattern over themselves with a second material, and faces that move with blend shapes, stay apart.
    /// The smoothed normals the outline shell needs ride in the tangents (marked w = 2), which skinning
    /// turns with the limbs.
    /// </summary>
    public static class MeshMerge
    {
        static readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(), S = new List<Vector3>();
        static readonly List<Color> C = new List<Color>();
        static readonly List<Vector2> U = new List<Vector2>();
        static readonly List<int> I = new List<int>();

        /// <summary>Merges the figure under root; returns the new renderer (on root), or null if there was nothing to merge.</summary>
        public static SkinnedMeshRenderer Figure(Transform root, Transform rootBone, List<Mesh> owned)
        {
            var bones = new List<Transform>();
            var binds = new List<Matrix4x4>();
            var skinBone = new Dictionary<Transform, int>();
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var tan = new List<Vector4>();
            var col = new List<Color>(); var uv = new List<Vector2>(); var weights = new List<BoneWeight>();
            var mats = new List<Material>(); var tris = new List<List<int>>();
            var merged = new List<Renderer>();
            bool hasUv = false, fourBones = false;
            var shadows = ShadowCastingMode.Off;
            Matrix4x4 rootW2L = root.worldToLocalMatrix, rootL2W = root.localToWorldMatrix;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Mesh mesh;
                var rm = r.sharedMaterials;
                int[] map;
                Matrix4x4 toRoot = rootW2L * r.transform.localToWorldMatrix;
                if (r is SkinnedMeshRenderer sk)
                {
                    mesh = sk.sharedMesh;
                    if (mesh == null || !mesh.isReadable || mesh.blendShapeCount > 0 || rm.Length != mesh.subMeshCount) continue;
                    // Its bones, with bind poses carried from its own space to the root's (the same for every part of a sculpt).
                    var sb = sk.bones; var bp = mesh.bindposes;
                    map = new int[sb.Length];
                    Matrix4x4 fromRoot = toRoot.inverse;
                    for (int i = 0; i < sb.Length; i++)
                    {
                        if (!skinBone.TryGetValue(sb[i], out int k)) { k = bones.Count; skinBone[sb[i]] = k; bones.Add(sb[i]); binds.Add(bp[i] * fromRoot); }
                        map[i] = k;
                    }
                    fourBones |= sk.quality == SkinQuality.Bone4;
                }
                else if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    mesh = mf != null ? mf.sharedMesh : null;
                    if (mesh == null || !mesh.isReadable || rm.Length != mesh.subMeshCount) continue;
                    // A rigid part: one bone, bound as it stands now.
                    map = new[] { bones.Count };
                    bones.Add(r.transform);
                    binds.Add(r.transform.worldToLocalMatrix * rootL2W);
                }
                else continue;     // lines, particles

                int baseV = pos.Count;
                V.Clear(); N.Clear(); C.Clear(); U.Clear(); S.Clear();
                mesh.GetVertices(V); mesh.GetNormals(N); mesh.GetColors(C); mesh.GetUVs(0, U);
                if (mesh.HasVertexAttribute(VertexAttribute.TexCoord3)) mesh.GetUVs(3, S);
                var bw = r is SkinnedMeshRenderer ? mesh.boneWeights : null;
                for (int i = 0; i < V.Count; i++)
                {
                    pos.Add(toRoot.MultiplyPoint3x4(V[i]));
                    Vector3 n = i < N.Count ? toRoot.MultiplyVector(N[i]).normalized : Vector3.up;
                    nrm.Add(n);
                    Vector3 sn = i < S.Count && S[i].sqrMagnitude > 0.01f ? toRoot.MultiplyVector(S[i]).normalized : n;
                    tan.Add(new Vector4(sn.x, sn.y, sn.z, 2f));
                    col.Add(i < C.Count ? C[i] : Color.white);
                    if (i < U.Count) { uv.Add(U[i]); hasUv = true; } else uv.Add(Vector2.zero);
                    if (bw != null && i < bw.Length)
                    {
                        var w = bw[i];
                        w.boneIndex0 = map[w.boneIndex0]; w.boneIndex1 = map[w.boneIndex1];
                        w.boneIndex2 = map[w.boneIndex2]; w.boneIndex3 = map[w.boneIndex3];
                        weights.Add(w);
                    }
                    else weights.Add(new BoneWeight { boneIndex0 = map[0], weight0 = 1f });
                }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int g = mats.IndexOf(rm[sub]);
                    if (g < 0) { g = mats.Count; mats.Add(rm[sub]); tris.Add(new List<int>()); }
                    I.Clear();
                    mesh.GetTriangles(I, sub);
                    var t = tris[g];
                    for (int i = 0; i < I.Count; i++) t.Add(I[i] + baseV);
                }
                if (r.shadowCastingMode != ShadowCastingMode.Off) shadows = ShadowCastingMode.On;
                merged.Add(r);
            }
            if (merged.Count < 2) return null;

            var m = new Mesh { name = root.name + " (merged)" };
            if (pos.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(pos); m.SetNormals(nrm); m.SetTangents(tan); m.SetColors(col);
            if (hasUv) m.SetUVs(0, uv);
            m.subMeshCount = mats.Count;
            for (int g = 0; g < mats.Count; g++) m.SetTriangles(tris[g], g, false);
            m.boneWeights = weights.ToArray();
            m.bindposes = binds.ToArray();
            m.RecalculateBounds();
            Bounds b = m.bounds;
            m.UploadMeshData(false);
            owned?.Add(m);

            // The parts go; their meshes were the figure's own.
            foreach (var r in merged)
            {
                Mesh old = r is SkinnedMeshRenderer s2 ? s2.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                owned?.Remove(old);
                Kill(old);
                if (r is SkinnedMeshRenderer) Kill(r.gameObject);
                else { var mf = r.GetComponent<MeshFilter>(); Kill(r); Kill(mf); }
            }

            var smr = root.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = m;
            smr.bones = bones.ToArray();
            smr.rootBone = rootBone;
            smr.sharedMaterials = mats.ToArray();
            smr.quality = fourBones ? SkinQuality.Bone4 : SkinQuality.Bone2;
            smr.skinnedMotionVectors = false;
            smr.updateWhenOffscreen = false;
            smr.shadowCastingMode = shadows;
            // Bounds about the hips, roomy enough for flung arms and a ragdoll's tumble.
            Vector3 c = rootBone != null ? rootBone.InverseTransformPoint(root.TransformPoint(b.center)) : b.center;
            smr.localBounds = new Bounds(c, Vector3.one * (b.size.magnitude + 1f));
            return smr;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
