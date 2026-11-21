using System;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

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
        /// <summary>Out of the tavern into the village: the run is long over, only the lift, the shop and the tavern are left.</summary>
        public bool VillageOnly { get; private set; }
        /// <summary>Set once the finish line is crossed and the result is on the board.</summary>
        public SubmitResult? Result { get; private set; }
        public Action OnLiftReached, OnTavernReached, OnShopReached;
        bool _inShop;

        ResultsPanel _results;
        LiveCrowd _live;
        readonly LiveRecorder _rec = new LiveRecorder();
        RunReplay _replay;
        Action _replayDone;
        float _fovBeforeReplay;
        VillageLife _village;
        readonly GhostTrack _track = new GhostTrack();
        SnowFx _snow;
        bool _left;
        public event Action<RunEvent> OnEvent;

        float _acc;
        bool _jump, _pole, _clickSwallowed, _botDrove;
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
            _live = new LiveCrowd(Session, transform);
            LiveRooms.Enter(slope.ToString().ToLowerInvariant());   // one shared world: this only says where we are
            _rec.Character = (int)who;
            _village = VillageLife.Create(course, transform);
            if (WorldLook.Instance != null) WorldLook.Instance.Follow = Rig.Cam.transform;
            if (Ui.Instance != null)
            {
                Hud = new Hud(this);
                Ui.Instance.ShowGoggles(!Rig.ThirdPerson);
            }
        }

        /// <summary>Puts the skier just outside the tavern door, the run already over.</summary>
        public void StartInVillage()
        {
            VillageOnly = true;
            Session.Phase = RunPhase.Finished;
            var c = Session.Course;
            Session.Sim.Teleport(c.TavernZone.X - 7f, c.TavernZone.Z - 1f, -Mathf.PI * 0.5f);   // facing the plaza
            Rig.Sync(Session.Sim, 0.016f);
        }

        void Finish()
        {
            if (Result.HasValue) return;
            var r = SaveData.Boards.Submit(Session.Slope, Session.Character, Session.ResultValue);
            Result = r;
            if (Ui.Instance != null) _results = new ResultsPanel(Session, r) { OnWatch = e => Watch(e) };
            // A personal best is kept as this device's ghost, and goes to the world board with its recording.
            string recording = _track.Encode();
            if (r.PersonalBest) SaveData.SetGhost(Session.Slope, recording);
            var panel = _results;
            // New Year's fireworks for a new best; the big show if it is the best in the world.
            bool improved = r.PersonalBest && !float.IsNaN(r.PreviousBest);
            if (improved) Celebrate(10f);
            // Only a person's own run goes to the world: never the autopilot, never a result no run could give.
            bool real = !_botDrove && Courses.Plausible(Session.Slope, Session.ResultValue);
            if (!_botDrove)
            {
                // What this run counts toward in the shop.
                var s = Session;
                SaveData.Record(s.Character, p =>
                {
                    p.runs++;
                    p.topSpeed = Mathf.Max(p.topSpeed, s.TopSpeed);
                    p.slopesFinished |= 1 << (int)s.Slope;
                    if (s.Slope == SlopeId.Green) { p.kidsHitOneRun = Mathf.Max(p.kidsHitOneRun, s.PeopleHit); p.kidsTotal += s.PeopleHit; }
                    if (s.Respawns == 0 && s.GatesMissed == 0 && s.Crashes == 0) p.cleanRuns++;
                    if (s.Slope == SlopeId.Blue) p.bestPark = Mathf.Max(p.bestPark, s.ResultValue);
                    if (s.Slope == SlopeId.Black && (p.bestBlack <= 0f || s.ResultValue < p.bestBlack)) p.bestBlack = s.ResultValue;
                    p.bestTrick = Mathf.Max(p.bestTrick, s.BestTrickPoints);
                    if (Courses.Plausible(s.Slope, s.ResultValue)) p.Offer(s.Slope, s.ResultValue, s.ScoreBased);
                });
            }
            if (!real) { OnlineBoards.Fetch(OnlineBoards.BoardId(Session.Slope), list => { if (Session != null && panel != null) panel.ShowWorld(list); }); return; }
            OnlineBoards.Submit(OnlineBoards.BoardId(Session.Slope), Session.ResultValue, recording, (int)Session.Character, list =>
            {
                if (Session == null || panel == null) return;
                if (panel.ShowWorld(list) && r.PersonalBest) Celebrate(20f);
            });
        }

        void Celebrate(float seconds)
        {
            var c = Session.Course;
            Vector3 origin = c.PointOnSurface(c.VillageX, c.FinishZ + Course.PlazaLength + 16f);
            Fireworks.Launch(transform, origin, 36f, seconds);
            AudioDirector.Sfx("cheer", 0.8f);
        }

        /// <summary>Removes the run's UI and sound, then destroys the run. Call instead of Destroy.</summary>
        public void Close()
        {
            StopWatching(false);
            _results?.Dispose();
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
            if (Autopilot != null) { _botDrove = true; return Autopilot.Decide(); }
            var inp = new SkiInput();
            var kb = Keyboard.current;
            if (kb == null || !InputEnabled) return inp;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) inp.Steer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) inp.Steer += 1f;
            inp.Tuck = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            inp.Brake = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            inp.Jump = _jump; inp.Pole = _pole;
            if (Result.HasValue) return inp;
            if (kb.qKey.isPressed) inp.Variant = 1;
            else if (kb.eKey.isPressed) inp.Variant = 2;
            if (kb.digit1Key.isPressed) inp.Trick = 1;
            else if (kb.digit2Key.isPressed) inp.Trick = 2;
            else if (kb.digit3Key.isPressed) inp.Trick = 3;
            else if (kb.digit4Key.isPressed) inp.Trick = 4;
            else if (kb.digit5Key.isPressed) inp.Trick = 5;
            return inp;
        }

        // ---- replays from the boards --------------------------------------------------------

        public bool Watching => _replay != null;

        /// <summary>Plays a board row's recorded run on this slope. <paramref name="done"/> runs when it ends (null: back to the run).</summary>
        public bool Watch(OnlineBoards.Entry e, Action done = null)
        {
            var track = GhostTrack.Decode(e.Ghost);
            if (track == null || Session == null) return false;
            StopWatching(false);
            var who = e.Character >= 0 && e.Character < 4 ? (CharacterId)e.Character : CharacterId.Erik;
            _replay = new RunReplay(transform, Session.Course, Session.Slope, track, e.Me ? "You" : e.Name, who);
            _replayDone = done;
            _fovBeforeReplay = Rig.Cam.fieldOfView;
            Rig.Body.gameObject.SetActive(false);
            if (Ui.Instance != null) { Ui.Instance.HudLayer.style.display = DisplayStyle.None; Ui.Instance.ShowGoggles(false); }
            AudioDirector.Click();
            return true;
        }

        /// <summary>Ends a replay and goes back to the run (or wherever the replay was started from).</summary>
        public void StopWatching(bool callBack = true)
        {
            if (_replay == null) return;
            _replay.Dispose();
            _replay = null;
            if (Rig != null)
            {
                Rig.Body.gameObject.SetActive(true);
                Rig.Cam.fieldOfView = _fovBeforeReplay;
            }
            if (Ui.Instance != null) { Ui.Instance.HudLayer.style.display = DisplayStyle.Flex; Ui.Instance.ShowGoggles(Rig != null && !Rig.ThirdPerson); }
            var done = _replayDone;
            _replayDone = null;
            if (callBack) done?.Invoke();
        }

        /// <summary>Tells the room where this player is (never the bot's runs, never from the start gate).</summary>
        void SendLive()
        {
            if (Autopilot != null || _botDrove || Session.Phase == RunPhase.Ready || Watching) return;
            var sim = Session.Sim;
            _rec.Mode = Result.HasValue || VillageOnly ? LiveMode.Finished : LiveMode.Skiing;
            _rec.Place = LivePlace.Of(Session.Slope);
            var flags = LiveFlags.None;
            if (sim.Tuck > 0.5f) flags |= LiveFlags.Tuck;
            if (sim.Plough > 0.4f) flags |= LiveFlags.Plough;
            if (sim.Crashed) flags |= LiveFlags.Crashed;
            var sample = new LiveSample { Pos = new Vector3(sim.Pos.x, sim.Contact ? 0f : sim.AirHeight, sim.Pos.z), Yaw = sim.Heading * Mathf.Rad2Deg, Flags = flags };
            LiveRooms.Send(_rec.Tick(LiveRooms.Now, sample));
        }

        void Update()
        {
            if (Session == null) return;
            if (_replay != null)
            {
                float rdt = Mathf.Min(Time.deltaTime, 0.1f);
                _replay.Tick(Rig.Cam, rdt);
                _live.Sync(Rig.Cam, rdt);
                // The kids carry on around the replayed skier, and go down if it runs into them.
                Vector3 rp = _replay.Position;
                if (Session.Crowd != null)
                {
                    Session.Crowd.Step(rdt, rp.z, rp.x);
                    var kid = _replay.Crashed ? null : Session.Crowd.Hit(rp.x, rp.z, Session.Sim.P.BodyRadius);
                    if (kid != null && !kid.Fallen)
                    {
                        // Both go down, as they would in a run.
                        Session.Crowd.Knock(kid);
                        _replay.Crash();
                        AudioDirector.Sfx("crash", 0.8f);
                    }
                }
                _village.Tick(rp.z, rdt);
                if (Kids != null) { Kids.ViewerZ = rp.z; Kids.Sync(rdt); }
                if (_replay.Done) StopWatching();
                return;
            }
            var kb = Keyboard.current;
            // On the results card: number keys watch the runs on the board.
            if (kb != null && Result.HasValue && _results != null && InputEnabled && !Paused && !Dialogs.Open)
                for (int i = 0; i < 9; i++)
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame && _results.Watch(i)) return;
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
            _live.Sync(Rig.Cam, dt);
            SendLive();
            VoiceChat.MyPos = Session.Sim.Pos; VoiceChat.Place = "slope"; VoiceChat.MyTable = -1;
            VoiceChat.MySlope = (int)Session.Slope; VoiceChat.MyFinishZ = Session.Course.FinishZ;
            _village.Tick(Session.Sim.Pos.z, dt);
            // People in the village are solid too.
            if (!Paused && !Session.Sim.Crashed && Session.Sim.Shield <= 0f && Session.Sim.Contact && Session.Sim.Speed > 1.2f && _village.Hit(Session.Sim.Pos, Session.Sim.Vel, Session.Sim.P.BodyRadius))
            {
                Session.Sim.Crash(CrashReason.Person);
                Rig.Kick(0.3f);
                AudioDirector.Sfx("crash", 0.8f);
            }
            if (!Paused) _snow.Tick(Session.Sim, dt);
            if (Kids != null) { Kids.ViewerZ = Session.Sim.Pos.z; Kids.Sync(dt); }
            AmbientSounds();

            // After the finish: ski to the lift for another run, or to the tavern door.
            if ((Result.HasValue || VillageOnly) && !_left && !Paused)
            {
                var c = Session.Course; var p = Session.Sim.Pos;
                // The shop opens on its pad, and again only after you have skied off it.
                bool shop = c.ShopZone.Contains(p.x, p.z);
                if (shop && !_inShop) OnShopReached?.Invoke();
                _inShop = shop;
                if (c.LiftZone.Contains(p.x, p.z)) { _left = true; OnLiftReached?.Invoke(); }
                else if (c.TavernZone.Contains(p.x, p.z)) { _left = true; OnTavernReached?.Invoke(); }
            }
        }
    }
}
