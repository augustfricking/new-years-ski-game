using Ski.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ski.Game
{
    /// <summary>
    /// The fitting room behind the shop window: the character on a slowly turning stand, lit and
    /// drawn into a texture the shop panel shows. Kept far below the world, out of every other view.
    /// </summary>
    public sealed class ShopStage : MonoBehaviour
    {
        public RenderTexture Texture { get; private set; }
        Camera _cam;
        Transform _turn;
        Person _person;
        float _spin, _time;
        bool _dragging;

        public static ShopStage Create(Transform parent)
        {
            var go = new GameObject("ShopStage");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, -900f, 0f);
            var s = go.AddComponent<ShopStage>();
            s.Build();
            return s;
        }

        void Build()
        {
            Texture = new RenderTexture(720, 900, 24) { name = "ShopStage", antiAliasing = 1 };
            var camGo = new GameObject("ShopCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 3.6f);
            camGo.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 34f; _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 30f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Palette.Hex("#F5EBD8");
            _cam.targetTexture = Texture;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // A round wooden stand and a warm key light.
            var b = new MeshBuilder();
            b.Col = Palette.TimberLight;
            b.Cylinder(new Vector3(0f, -0.12f, 0f), 0.75f, 0.8f, 0.12f, 24);
            b.Col = Palette.Hex("#E2D2B4");
            b.Box(new Vector3(0f, 1.5f, -1.4f), new Vector3(6f, 4f, 0.1f));
            b.ToObject("Stand", Mats.Props, transform);
            var lightGo = new GameObject("Key");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(1.5f, 2.6f, 2.4f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 9f; l.intensity = 3.5f; l.color = Color.Lerp(Palette.Lamp, Color.white, 0.6f); l.shadows = LightShadows.None;
            _turn = new GameObject("Turntable").transform;
            _turn.SetParent(transform, false);
        }

        /// <summary>Dresses the stand: the character in this look.</summary>
        public void Show(PersonLook look)
        {
            if (_person != null) Destroy(_person.gameObject);
            look.GogglesDown = false;
            look.Hero = true;
            _person = Person.Create(look, _turn, "Model");
            _person.Poles = PoleMode.Hang;
            _person.Snap(Pose.Stand);
            _person.Smile = 0.6f;
            foreach (var r in _person.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Turns the stand by a drag of the mouse (degrees); it drifts round on its own otherwise.</summary>
        public void Drag(float degrees) { _spin += degrees; _dragging = true; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _time += dt;
            if (!_dragging) _spin += dt * 18f;
            _dragging = false;
            _turn.localRotation = Quaternion.Euler(0f, 180f + _spin, 0f);
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
