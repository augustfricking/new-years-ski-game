using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Another real person, with their name floating above. Two ways to look: a pale ghost while
    /// they ski the same slope as you (ghosts cannot be hit), or their own solid self once they
    /// are past the finish, in the tavern, or in a replay from the boards.
    /// </summary>
    public sealed class GhostView : MonoBehaviour
    {
        public static readonly Color Pale = new Color(0.72f, 0.88f, 1f);

        public string Label { get; private set; }
        public bool Solid { get; private set; }
        public Person Body => Solid ? _solid : _ghost;

        CharacterId _who;
        bool _onFoot;
        Person _ghost, _solid;
        Transform _name;
        TextMesh _nameText;
        float _heading, _lean, _walk;
        bool _placed;

        /// <param name="onFoot">Indoors: no skis, no poles, no helmet.</param>
        public static GhostView Create(Transform parent, string label, CharacterId who, bool solid, bool onFoot = false)
        {
            var go = new GameObject("Live " + label);
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GhostView>();
            g._who = who;
            g._onFoot = onFoot;
            g.Label = label;
            var tm = VillageBuilder.Sign(go.transform, label, Vector3.zero, Quaternion.identity, 0.3f, Color.Lerp(Pale, Color.white, 0.7f));
            g._nameText = tm;
            g._name = tm.transform;
            g._name.localPosition = new Vector3(0f, 2.25f, 0f);
            g.SetSolid(solid);
            go.SetActive(false);
            return g;
        }

        public void Rename(string label)
        {
            if (label == Label) return;
            Label = label;
            if (_nameText != null) _nameText.text = label;
        }

        /// <summary>Ghost or solid. Each body is built the first time it is needed.</summary>
        public void SetSolid(bool solid)
        {
            Solid = solid;
            if (solid && _solid == null) _solid = Build(true);
            if (!solid && _ghost == null) _ghost = Build(false);
            if (_solid != null) _solid.gameObject.SetActive(solid);
            if (_ghost != null) _ghost.gameObject.SetActive(!solid);
        }

        Person Build(bool solid)
        {
            var look = PersonLook.For(_who);
            look.GogglesDown = !solid;
            if (_onFoot) { look.Gear = GearKind.None; look.Poles = false; look.HasHelmet = false; look.HasGoggles = false; }
            if (!solid)
            {
                // The friend's own outfit, washed toward the ghost colour, kept simple.
                look.Hero = false;
                Color Wash(Color c) => Color.Lerp(c, Pale, 0.5f);
                look.Jacket = Wash(look.Jacket); look.Trim = Wash(look.Trim); look.Pants = Wash(look.Pants); look.Helmet = Wash(look.Helmet);
                look.Gloves = Wash(look.Gloves); look.Boots = Wash(look.Boots); look.Skin = Wash(look.Skin); look.Hair = Wash(look.Hair);
                look.GearColour = Wash(look.GearColour); look.GearAccent = Wash(look.GearAccent); look.Goggles = Wash(look.Goggles);
            }
            var p = Person.Create(look, transform, solid ? "Body" : "Ghost");
            p.Alive = solid;
            p.SideStance = look.Gear == GearKind.Board ? 1f : 0f;
            p.Snap(Pose.Ski);
            if (!solid) foreach (var r in p.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p;
        }

        /// <summary>Places a skier for this frame.</summary>
        public void Show(Vector3 pos, float heading, float speed, float air, float tuck, float plough, Camera cam, float dt, bool crashed = false)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            float turn = _placed && dt > 0f ? Mathf.DeltaAngle(_heading * Mathf.Rad2Deg, heading * Mathf.Rad2Deg) / dt : 0f;
            _heading = heading;
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(turn * 0.25f, -24f, 24f), 1f - Mathf.Exp(-6f * dt));
            _placed = true;
            float tumble = crashed ? Mathf.Sin(Time.time * 9f) * 50f + 40f : 0f;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(tumble, heading * Mathf.Rad2Deg, -_lean));

            var person = Body;
            Pose p = Pose.Lerp(Pose.Ski, Pose.Tuck, tuck);
            if (person.Look.Gear != GearKind.Board) p = Pose.Lerp(p, Pose.Plough, plough);
            else p.LegOut = 13f;
            if (air > 0.3f) { p.ThighL += 24f; p.KneeL += 44f; p.ThighR += 24f; p.KneeR += 44f; p.ArmOutL += 20f; p.ArmOutR += 20f; }
            else if (speed < 1.5f) p = Pose.Stand;
            p.TorsoRoll = -_lean * 0.4f;
            person.Target = p;
            PlaceName(cam);
        }

        /// <summary>Places a person on foot (the tavern): walking when they move, standing when not.</summary>
        public void ShowOnFoot(Vector3 pos, float yawDeg, float speed, Camera cam, float dt, bool punching = false)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _placed = true;
            transform.localPosition = pos;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(0f, yawDeg, 0f), 1f - Mathf.Exp(-10f * dt));
            var person = Body;
            person.Poles = PoleMode.Hang;
            if (speed > 0.25f)
            {
                _walk += dt * Mathf.Clamp(speed * 3.2f, 3f, 10f);
                person.Target = VillageLife.Walk(_walk);
            }
            else person.Target = Pose.Stand;
            if (punching)
            {
                // A straight right, as seen from across the room.
                var p = person.Target;
                p.ArmFwdR = 88f; p.ElbowR = 4f; p.ArmOutR = 4f; p.TorsoYaw = -16f;
                person.Target = p;
            }
            PlaceName(cam);
        }

        void PlaceName(Camera cam)
        {
            if (cam == null) return;
            // The name always faces the viewer and stays readable from far up the hill.
            Vector3 to = _name.position - cam.transform.position;
            float d = to.magnitude;
            _name.rotation = Quaternion.LookRotation(to, Vector3.up);
            _name.localScale = Vector3.one * Mathf.Clamp(d / 14f, 0.3f, 7f);
            _name.localPosition = new Vector3(0f, (Solid && d < 8f ? 2.0f : 2.25f) + Mathf.Clamp(d / 60f, 0f, 1.2f), 0f);
        }

        public void ShowName(bool on) { if (_name != null) _name.gameObject.SetActive(on); }

        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); _placed = false; }
    }
}
