using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>The frame rate in the top corner, when the graphics settings ask for it (to tune them by), and how
    /// the other players come in: by a direct line or only through the room.</summary>
    public sealed class FpsMeter : MonoBehaviour
    {
        Label _label;
        float _time;
        int _frames;

        public static FpsMeter Create(Transform parent)
        {
            var go = new GameObject("FpsMeter");
            go.transform.SetParent(parent, false);
            return go.AddComponent<FpsMeter>();
        }

        void Update()
        {
            if (!SaveData.ShowFps || Ui.Instance == null)
            {
                if (_label != null) { _label.RemoveFromHierarchy(); _label = null; }
                return;
            }
            if (_label == null)
            {
                _label = Ui.Text(Ui.Instance.TopLayer, "", "fps-meter");
                _label.pickingMode = PickingMode.Ignore;
                _time = 0f; _frames = 0;
            }
            _time += Time.unscaledDeltaTime;
            _frames++;
            if (_time < 0.5f) return;
            // With other players about: how many of them come by a direct line, and its round trip.
            string live = "";
            int others = LiveRooms.Others.Count;
            if (others > 0)
            {
                live = "  ·  DIRECT " + LiveLink.OpenLines + "/" + others;
                if (LiveLink.RoundTrip > 0f) live += "  " + Mathf.RoundToInt(LiveLink.RoundTrip) + " ms";
            }
            _label.text = Mathf.RoundToInt(_frames / _time) + " FPS" + live;
            _time = 0f; _frames = 0;
        }
    }
}
