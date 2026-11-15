using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Somebody else on the slope: a pale skier with their name floating above. Ghosts cannot be
    /// hit. They are driven either by a recording (your own best run, another player's run from the
    /// world board) or by a rival skiing live.
    /// </summary>
    public sealed class GhostView : MonoBehaviour
    {
        public string Label { get; private set; }
        Person _person;
        Transform _name;
        float _heading, _lean;
        bool _placed;

        public static GhostView Create(Transform parent, string label, CharacterId who, Color tint)
        {
            var go = new GameObject("Ghost " + label);
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GhostView>();
            g.Build(label, who, tint);
            return g;
        }

        void Build(string label, CharacterId who, Color tint)
        {
            Label = label;
            // The friend's own outfit, washed toward the ghost colour.
            var look = PersonLook.For(who);
            look.Hero = false; look.GogglesDown = true;
            Color Wash(Color c) => Color.Lerp(c, tint, 0.5f);
            look.Jacket = Wash(look.Jacket); look.Trim = Wash(look.Trim); look.Pants = Wash(look.Pants); look.Helmet = Wash(look.Helmet);
            look.Gloves = Wash(look.Gloves); look.Boots = Wash(look.Boots); look.Skin = Wash(look.Skin); look.Hair = Wash(look.Hair);
            look.GearColour = Wash(look.GearColour); look.GearAccent = Wash(look.GearAccent); look.Goggles = Wash(look.Goggles);
            _person = Person.Create(look, transform, "Body");
            _person.Alive = false;
            _person.SideStance = look.Gear == GearKind.Board ? 1f : 0f;
            _person.Snap(Pose.Ski);
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var tm = VillageBuilder.Sign(transform, label, Vector3.zero, Quaternion.identity, 0.3f, Color.Lerp(tint, Color.white, 0.7f));
            _name = tm.transform;
            _name.localPosition = new Vector3(0f, 2.25f, 0f);
            gameObject.SetActive(false);
        }

        /// <summary>Places the ghost for this frame.</summary>
        public void Show(Vector3 pos, float heading, float speed, float air, float tuck, float plough, Camera cam, float dt)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            float turn = _placed && dt > 0f ? Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, heading * Mathf.Rad2Deg) / dt : 0f;
            _heading = heading;
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(turn * 0.25f, -24f, 24f), 1f - Mathf.Exp(-6f * dt));
            _placed = true;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, heading * Mathf.Rad2Deg, -_lean));

            Pose p = Pose.Lerp(Pose.Ski, Pose.Tuck, tuck);
            if (_person.Look.Gear != GearKind.Board) p = Pose.Lerp(p, Pose.Plough, plough);
            else p.LegOut = 13f;
            if (air > 0.3f) { p.ThighL += 24f; p.KneeL += 44f; p.ThighR += 24f; p.KneeR += 44f; p.ArmOutL += 20f; p.ArmOutR += 20f; }
            else if (speed < 1.5f) p = Pose.Stand;
            p.TorsoRoll = -_lean * 0.4f;
            _person.Target = p;

            if (cam != null)
            {
                // The name always faces the viewer and stays readable from far up the hill.
                Vector3 to = _name.position - cam.transform.position;
                float d = to.magnitude;
                _name.rotation = Quaternion.LookRotation(to, Vector3.up);
                _name.localScale = Vector3.one * Mathf.Clamp(d / 14f, 1f, 7f);
                _name.localPosition = new Vector3(0f, 2.25f + Mathf.Clamp(d / 60f, 0f, 1.2f), 0f);
            }
        }

        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); _placed = false; }
    }
}
