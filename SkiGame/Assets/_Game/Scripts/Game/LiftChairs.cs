using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// The chairs of a lift, running round the cable loop: up the line nearer the piste, round the
    /// wheel at the top, down the other line and round again at the bottom. Riders sit on the way up
    /// and are gone (got off at the top) on the way down.
    /// </summary>
    public sealed class LiftChairs : MonoBehaviour
    {
        const float Speed = 3f;          // metres a second, as a fixed-grip chair runs

        Vector3[] _path;                 // pylon tops, top station first
        float[] _at;                     // distance along the path at each point
        float _len, _half, _side, _gap, _run;
        readonly List<Transform> _chairs = new List<Transform>();
        readonly Dictionary<int, List<GameObject>> _riders = new Dictionary<int, List<GameObject>>();

        /// <summary>Hangs chairs every gap metres round the loop; returns them so riders can be seated.</summary>
        public List<Transform> Setup(List<Vector3> path, Mesh chair, Material mat, float side, float half, float gap)
        {
            _path = path.ToArray();
            _at = new float[_path.Length];
            for (int i = 1; i < _path.Length; i++) _at[i] = _at[i - 1] + (_path[i] - _path[i - 1]).magnitude;
            _len = _at[_path.Length - 1];
            _half = half; _side = side; _gap = gap;
            int n = Mathf.Max(2, Mathf.FloorToInt(2f * _len / gap));
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Chair");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = chair;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
                _chairs.Add(go.transform);
            }
            Place();
            return _chairs;
        }

        /// <summary>Whether a chair is on its way up (where it would carry people) right now.</summary>
        public bool GoingUp(int chair) => Loop(chair) >= _len;

        public void Seat(int chair, GameObject rider)
        {
            if (!_riders.TryGetValue(chair, out var list)) _riders[chair] = list = new List<GameObject>();
            list.Add(rider);
        }

        float Loop(int chair) => Mathf.Repeat(chair * _gap + _run, 2f * _len);

        void Update()
        {
            _run += Speed * Time.deltaTime;
            Place();
        }

        void Place()
        {
            for (int i = 0; i < _chairs.Count; i++)
            {
                float s = Loop(i);
                bool up = s >= _len;
                float d = up ? 2f * _len - s : s;                // distance from the top station
                // Round the wheels at either end: swing across from one line to the other.
                float lane = up ? -1f : 1f;
                float wheel = _half * 3f;
                if (d < wheel) lane *= Mathf.Sin(Mathf.PI * 0.5f * d / wheel);
                else if (d > _len - wheel) lane *= Mathf.Sin(Mathf.PI * 0.5f * (_len - d) / wheel);
                Vector3 p = At(d) + Vector3.right * lane * _half * _side;
                _chairs[i].localPosition = p;
                _chairs[i].localRotation = Quaternion.Euler(0f, up ? 180f : 0f, 0f);
                if (_riders.TryGetValue(i, out var list))
                    for (int k = 0; k < list.Count; k++)
                        if (list[k] != null && list[k].activeSelf != up) list[k].SetActive(up);
            }
        }

        Vector3 At(float d)
        {
            for (int i = 0; i < _path.Length - 1; i++)
                if (d <= _at[i + 1]) return Vector3.Lerp(_path[i], _path[i + 1], Mathf.InverseLerp(_at[i], _at[i + 1], d));
            return _path[_path.Length - 1];
        }
    }
}
