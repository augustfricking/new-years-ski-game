using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ski.Game
{
    /// <summary>
    /// The light story of the game: a low warm sun, cool gradient ambient, banded dusk sky, fog,
    /// post-processing and falling snow. <see cref="Storm"/> (0..1) pulls all of it toward a
    /// white-out for the cutscene.
    /// </summary>
    public sealed class WorldLook : MonoBehaviour
    {
        public static WorldLook Instance { get; private set; }

        public Light Sun { get; private set; }
        /// <summary>0 = clear dusk, 1 = full snow storm.</summary>
        public float Storm;
        /// <summary>0 = late afternoon, 1 = night (the tavern, fireworks).</summary>
        public float Night;
        /// <summary>Inside the tavern: no sun, warm dim ambient, no snowfall.</summary>
        public bool Indoors;
        /// <summary>The camera the snow should fall around.</summary>
        public Transform Follow;
        public float FogStart = 140f, FogEnd = 1100f;
        /// <summary>The view distance of the graphics settings: the fog's distances are scaled by it.</summary>
        public float Reach = 1f;
        /// <summary>The effects level of the graphics settings: bloom only on high, less snowfall on low.</summary>
        public int Effects = 1;

        public static readonly Vector3 SunDirection = new Vector3(0.74f, 0.4f, 0.54f).normalized;

        Material _sky;
        ParticleSystem _snow;
        Volume _volume;
        Vignette _vignette;
        Bloom _bloom;
        ColorAdjustments _colour;

        public static WorldLook Create(Transform parent)
        {
            var go = new GameObject("WorldLook");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WorldLook>();
            w.Build();
            Instance = w;
            return w;
        }

        void Build()
        {
            // Key light.
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.LookRotation(-SunDirection);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = Palette.Sun;
            Sun.intensity = 1.2f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 1f;

            // Ambient: cool sky, neutral middle, warm ground.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;

            _sky = Mats.Sky();
            _sky.SetVector("_SunDir", SunDirection);
            RenderSettings.skybox = _sky;
            RenderSettings.sun = Sun;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            // Post-processing.
            var volGo = new GameObject("Global Volume");
            volGo.transform.SetParent(transform, false);
            _volume = volGo.AddComponent<Volume>();
            _volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
            var bloom = _bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.6f);
            _vignette = profile.Add<Vignette>(true); _vignette.intensity.Override(0.22f); _vignette.smoothness.Override(0.4f);
            _colour = profile.Add<ColorAdjustments>(true); _colour.saturation.Override(10f); _colour.contrast.Override(8f);
            _volume.sharedProfile = profile;

            BuildSnow();
            Apply();
        }

        void BuildSnow()
        {
            var go = new GameObject("Snowfall");
            go.transform.SetParent(transform, false);
            _snow = go.AddComponent<ParticleSystem>();
            var main = _snow.main;
            main.loop = true; main.playOnAwake = true;
            main.startLifetime = 4.5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.075f);
            main.startColor = Palette.Snow;
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var shape = _snow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(46f, 18f, 46f);
            var vel = _snow.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-1.2f, 0.4f);
            vel.y = new ParticleSystem.MinMaxCurve(-2.6f, -1.6f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var noise = _snow.noise;
            noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.35f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle(Mats.SoftDot, false, 0.3f);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            if (Follow != null) _snow.transform.position = Follow.position + Follow.forward * 8f + Vector3.up * 5f;
            Apply();
        }

        public void Apply()
        {
            float s = Mathf.Clamp01(Storm), n = Mathf.Clamp01(Night);
            Color fog = Color.Lerp(Color.Lerp(Palette.Fog, Palette.SkyTop, n * 0.8f), Palette.Storm, s);
            RenderSettings.fogColor = fog;
            float fs = FogStart * Reach, fe = FogEnd * Reach;
            RenderSettings.fogStartDistance = fs * Mathf.Pow(1.5f / fs, s);
            RenderSettings.fogEndDistance = fe * Mathf.Pow(22f / fe, s); // exponential: thickens steadily
            // Bloom needs the bright range only high effects keep (HDR); without it it costs and shows nothing.
            if (_bloom != null) _bloom.active = Effects >= 2;
            Sun.intensity = Mathf.Lerp(1.02f, 0.25f, n) * Mathf.Lerp(1f, 0.45f, s);
            Sun.color = Color.Lerp(Color.Lerp(Palette.Sun, Palette.AmbientSky, n * 0.7f), Palette.Storm, s * 0.8f);
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(Palette.AmbientSky, Palette.Mul(Palette.SkyTop, 1.2f), n), Palette.Storm, s * 0.7f);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(Palette.AmbientEquator, Palette.SkyTop, n * 0.7f), Palette.Storm, s * 0.6f);
            RenderSettings.ambientGroundColor = Color.Lerp(Palette.AmbientGround, Palette.Mul(Palette.SkyTop, 0.8f), n);
            _sky.SetFloat("_Storm", s);
            _sky.SetFloat("_Stars", Mathf.Lerp(0.25f, 1f, n) * (1f - s));
            _sky.SetColor("_Top", Color.Lerp(Palette.SkyTop, Palette.Mul(Palette.SkyTop, 0.45f), n));
            _sky.SetColor("_Mid", Color.Lerp(Palette.SkyMid, Palette.Mul(Palette.SkyTop, 0.8f), n));
            _sky.SetColor("_Horizon", Color.Lerp(Palette.SkyHorizon, Palette.SkyMid, n * 0.8f));
            var emission = _snow.emission;
            emission.rateOverTime = Mathf.Lerp(90f, 1500f, s) * (Effects == 0 ? 0.35f : Effects == 1 ? 0.7f : 1f);
            var vel = _snow.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(Mathf.Lerp(-1.2f, -11f, s), Mathf.Lerp(0.4f, -5f, s));
            _vignette.intensity.Override(Mathf.Lerp(0.22f, 0.36f, s));
            _colour.saturation.Override(Mathf.Lerp(10f, -25f, s));
            if (Indoors)
            {
                Sun.intensity = 0f;
                RenderSettings.ambientSkyColor = Palette.Mul(Palette.TimberLight, 0.62f);
                RenderSettings.ambientEquatorColor = Palette.Mul(Palette.Timber, 0.6f);
                RenderSettings.ambientGroundColor = Palette.Mul(Palette.TimberDark, 0.6f);
                RenderSettings.fogColor = Palette.TimberDark;
                RenderSettings.fogStartDistance = 30f;
                RenderSettings.fogEndDistance = 120f;
                emission.rateOverTime = 0f;
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
