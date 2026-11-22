using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>The frame rate in the top corner, when the graphics settings ask for it (to tune them by).</summary>
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
            _label.text = Mathf.RoundToInt(_frames / _time) + " FPS";
            _time = 0f; _frames = 0;
        }
    }
}
