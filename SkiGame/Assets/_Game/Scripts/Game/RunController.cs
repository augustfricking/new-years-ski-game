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
        public Hud Hud { get; private set; }
        /// <summary>When set, the bot plays instead of the keyboard (used for tests and attract mode).</summary>
        public Bot Autopilot;
        public bool Paused;
        public bool InputEnabled = true;
        /// <summary>Set once the finish line is crossed and the result is on the board.</summary>
        public SubmitResult? Result { get; private set; }
        public Action OnLiftReached, OnTavernReached;

        ResultsPanel _results;
        GhostCrowd _ghosts;
        readonly GhostTrack _track = new GhostTrack();
        SnowFx _snow;
        bool _left;
        public event Action<RunEvent> OnEvent;

        float _acc;
        bool _jump, _pole, _clickSwallowed;
        /// <summary>Call when a click was used for something else (resuming from pause), so it does not also push.</summary>
        public void SwallowClick() { _clickSwallowed = true; }
        readonly System.Collections.Generic.HashSet<int> _screamed = new System.Collections.Generic.HashSet<int>();

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
            Rig.SetThirdPerson(slope == SlopeId.Blue);   // the park is ridden from behind, so tricks can be seen
            Rig.Sync(Session.Sim, 0.016f);
            _snow = SnowFx.Create(transform);
            _ghosts = new GhostCrowd(Session, transform);
            if (WorldLook.Instance != null) WorldLook.Instance.Follow = Rig.Cam.transform;
            if (Ui.Instance != null)
            {
                Hud = new Hud(this);
                Ui.Instance.ShowGoggles(!Rig.ThirdPerson);
            }
        }

        void Finish()
        {
            if (Result.HasValue) return;
            var r = SaveData.Boards.Submit(Session.Slope, Session.Character, Session.ResultValue);
            Result = r;
            if (Ui.Instance != null) _results = new ResultsPanel(Session, r);
            // A personal best is kept as this device's ghost, and goes to the world board with its recording.
            string recording = _track.Encode();
            if (r.PersonalBest) SaveData.SetGhost(Session.Slope, recording);
            var panel = _results;
            OnlineBoards.Submit(OnlineBoards.BoardId(Session.Slope), Session.ResultValue, recording, (int)Session.Character, list => panel?.ShowWorld(list));
            // New Year's fireworks for a new best.
            bool improved = r.PersonalBest && !float.IsNaN(r.PreviousBest);
            if (r.TopOfBoard || improved)
            {
                var c = Session.Course;
                Vector3 origin = c.PointOnSurface(c.VillageX, c.FinishZ + Course.PlazaLength + 16f);
                Fireworks.Launch(transform, origin, 36f, r.TopOfBoard ? 20f : 10f);
                AudioDirector.Sfx("cheer", 0.8f);
            }
        }

        /// <summary>Removes the run's UI and sound, then destroys the run. Call instead of Destroy.</summary>
        public void Close()
        {
            _results?.Dispose();
            _ghosts?.Close();
            Hud?.Dispose();
            Hud = null;
            if (Ui.Instance != null) Ui.Instance.ShowGoggles(false);
            if (AudioDirector.I != null) { AudioDirector.I.Silence(); AudioDirector.I.Paused = false; }
            Session = null;
            Destroy(gameObject);
        }

        void Sound(RunEvent e)
        {
            switch (e.Type)
            {
                case RunEventType.PolePush: AudioDirector.Sfx((PoleQuality)(int)e.Value == PoleQuality.Perfect ? "pole_perfect" : "pole", 0.7f); break;
                case RunEventType.Jump: AudioDirector.Sfx("jump", 0.55f); break;
                case RunEventType.Landed: AudioDirector.Sfx("land", 0.7f); break;
                case RunEventType.HeavyLanding: AudioDirector.Sfx("land_heavy", 0.8f); break;
                case RunEventType.Crash: AudioDirector.Sfx("crash", 0.9f); break;
                case RunEventType.GatePassed: AudioDirector.Sfx("gate", 0.5f, 1f + (e.Value % 2) * 0.12f); break;
                case RunEventType.GateMissed: AudioDirector.Sfx("gate_miss", 0.7f); break;
                case RunEventType.TrickScored: AudioDirector.Sfx(e.Trick.Points >= 600 ? "trick_big" : "trick", 0.7f); break;
                case RunEventType.PersonHit: AudioDirector.Sfx("penalty", 0.6f); AudioDirector.Sfx("scream", 0.4f, 1.15f); break;
                case RunEventType.DeadEnd: AudioDirector.Sfx("penalty", 0.6f); break;
                case RunEventType.Finished: AudioDirector.Sfx("finish", 0.8f); break;
                case RunEventType.Started: AudioDirector.Sfx("gate", 0.5f, 1.4f); break;
            }
        }

        void AmbientSounds()
        {
            var a = AudioDirector.I;
            if (a == null) return;
            var sim = Session.Sim;
            a.Paused = Paused;
            // The scrape follows the edge itself (OverLean), so it starts the instant a carve tips into a skid.
            float skid = Mathf.Max(sim.OverLean, sim.Plough, sim.SkidAmount * 0.7f);
            a.SkiState(sim.Speed, skid, sim.CarveQuality, sim.Contact && !sim.Crashed, WorldLook.Instance != null ? WorldLook.Instance.Storm : 0f);
            // Bombers scream as they come past.
            if (Session.Crowd != null)
                foreach (var k in Session.Crowd.Kids)
                {
                    if (k.Kind != KidKind.Bomber || !k.Active || _screamed.Contains(k.Id)) continue;
                    float dz = k.Z - sim.Pos.z, dx = k.X - sim.Pos.x;
                    if (dz > -16f && dz < 10f && Mathf.Abs(dx) < 18f)
                    {
                        _screamed.Add(k.Id);
                        a.Play("scream", 0.3f, 0.9f + (k.Id % 5) * 0.06f, Mathf.Clamp(dx / 10f, -1f, 1f));
                    }
                }
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
                var mouse = Mouse.current;
                // Pole push: left mouse button (Shift still works).
                if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame) _pole = true;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !Paused && !Dialogs.Open && !_clickSwallowed) _pole = true;
                _clickSwallowed = false;
                if (kb.rKey.wasPressedThisFrame && !Paused) Session.Respawn();
                if (kb.cKey.wasPressedThisFrame && !Paused)
                {
                    Rig.SetThirdPerson(!Rig.ThirdPerson);
                    if (Ui.Instance != null) Ui.Instance.ShowGoggles(!Rig.ThirdPerson);
                }
                // The mouse always looks around while skiing: no click needed.
                if (mouse != null && !Paused && !Dialogs.Open)
                {
                    Vector2 d = mouse.delta.ReadValue() * SaveData.MouseSensitivity;
                    Rig.Look.x = Mathf.Clamp(Rig.Look.x + d.x, -120f, 120f);
                    Rig.Look.y = Mathf.Clamp(Rig.Look.y + (SaveData.InvertLook ? -d.y : d.y), -78f, 55f);
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
                    if (Session.Phase != RunPhase.Ready)
                    {
                        if (Session.Phase == RunPhase.Running) _track.Record(Session.Time, Session.Sim.Pos.x, Session.Sim.Pos.z, Session.Sim.Contact ? 0f : Session.Sim.AirHeight);
                        _ghosts.Step(SkiSim.FixedDt);
                    }
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
                    case RunEventType.HeavyLanding: Rig.Kick(0.12f); _snow.Puff(Session.Sim.Pos, Session.Sim.Vel, 40, 4f); break;
                    case RunEventType.Landed: Rig.Kick(0.05f); _snow.Puff(Session.Sim.Pos, Session.Sim.Vel, 22, 3f); break;
                    case RunEventType.Crash: Rig.Kick(0.3f); _snow.Puff(Session.Sim.Pos, Session.Sim.Vel, 70, 6f); break;
                }
                Sound(e);
                if (e.Type == RunEventType.Finished) Finish();
                OnEvent?.Invoke(e);
            }
            Session.Events.Clear();

            Rig.Sync(Session.Sim, dt);
            _ghosts.Sync(Rig.Cam, dt);
            if (!Paused) _snow.Tick(Session.Sim, dt);
            if (Kids != null) { Kids.ViewerZ = Session.Sim.Pos.z; Kids.Sync(dt); }
            AmbientSounds();

            // After the finish: ski to the lift for another run, or to the tavern door.
            if (Result.HasValue && !_left && !Paused)
            {
                var c = Session.Course; var p = Session.Sim.Pos;
                if (c.LiftZone.Contains(p.x, p.z)) { _left = true; OnLiftReached?.Invoke(); }
                else if (c.TavernZone.Contains(p.x, p.z)) { _left = true; OnTavernReached?.Invoke(); }
            }
        }
    }
}
