using System;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ski.Game
{
    /// <summary>
    /// Plays one run: builds the course, reads the keyboard and mouse, steps the simulation at a
    /// fixed rate and keeps the first-person rig in sync. Destroy it to throw the whole run away.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        public RunSession Session { get; private set; }
        public PlayerRig Rig { get; private set; }
        public KidView Kids { get; private set; }
        /// <summary>When set, the bot plays instead of the keyboard (used for tests and attract mode).</summary>
        public Bot Autopilot;
        public bool Paused;
        public bool InputEnabled = true;
        public float MouseSensitivity = 0.12f;
        public event Action<RunEvent> OnEvent;

        float _acc;
        bool _jump, _pole;

        public static RunController Create(Transform parent, SlopeId slope, CharacterId who)
        {
            var go = new GameObject("Run " + slope);
            go.transform.SetParent(parent, false);
            var rc = go.AddComponent<RunController>();
            rc.Build(slope, who);
            return rc;
        }

        void Build(SlopeId slope, CharacterId who)
        {
            Session = new RunSession(slope, who);
            var course = (PisteCourse)Session.Course;
            TerrainBuilder.Build(course, Session, transform);
            VillageBuilder.Build(course, transform);
            if (Session.Crowd != null) Kids = KidView.Create(Session.Crowd, course, transform);
            Rig = PlayerRig.Create(who, transform);
            Rig.Sync(Session.Sim, 0.016f);
            if (WorldLook.Instance != null) WorldLook.Instance.Follow = Rig.Cam.transform;
        }

        SkiInput ReadInput()
        {
            if (Autopilot != null) return Autopilot.Decide();
            var inp = new SkiInput();
            var kb = Keyboard.current;
            if (kb == null || !InputEnabled) return inp;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) inp.Steer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) inp.Steer += 1f;
            inp.Tuck = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            inp.Brake = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            inp.Jump = _jump; inp.Pole = _pole;
            if (kb.digit1Key.isPressed) inp.Trick = 1;
            else if (kb.digit2Key.isPressed) inp.Trick = 2;
            else if (kb.digit3Key.isPressed) inp.Trick = 3;
            else if (kb.digit4Key.isPressed) inp.Trick = 4;
            else if (kb.digit5Key.isPressed) inp.Trick = 5;
            return inp;
        }

        void Update()
        {
            if (Session == null) return;
            var kb = Keyboard.current;
            if (kb != null && InputEnabled && Autopilot == null)
            {
                if (kb.spaceKey.wasPressedThisFrame) _jump = true;
                if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame) _pole = true;
                if (kb.rKey.wasPressedThisFrame && !Paused) Session.Respawn();
                var mouse = Mouse.current;
                if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 d = mouse.delta.ReadValue() * MouseSensitivity;
                    Rig.Look.x = Mathf.Clamp(Rig.Look.x + d.x, -120f, 120f);
                    Rig.Look.y = Mathf.Clamp(Rig.Look.y + d.y, -78f, 55f);
                }
            }

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (!Paused)
            {
                _acc += dt;
                int guard = 0;
                while (_acc >= SkiSim.FixedDt && guard++ < 16)
                {
                    _acc -= SkiSim.FixedDt;
                    Session.Step(ReadInput(), SkiSim.FixedDt);
                    _jump = _pole = false; // edges are consumed by the first step
                }
                if (guard >= 16) _acc = 0f;
            }

            for (int i = 0; i < Session.Events.Count; i++)
            {
                var e = Session.Events[i];
                switch (e.Type)
                {
                    case RunEventType.PolePush: Rig.PolePush(); break;
                    case RunEventType.HeavyLanding: Rig.Kick(0.12f); break;
                    case RunEventType.Landed: Rig.Kick(0.05f); break;
                    case RunEventType.Crash: Rig.Kick(0.3f); break;
                }
                OnEvent?.Invoke(e);
            }
            Session.Events.Clear();

            Rig.Sync(Session.Sim, dt);
            if (Kids != null) Kids.Sync(dt);
        }
    }
}
