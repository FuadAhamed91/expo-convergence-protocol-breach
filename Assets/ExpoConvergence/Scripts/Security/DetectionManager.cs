using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// The security grid's shared awareness of the courier.
    /// Sensors add to a 0..1 detection meter; when it fills the grid goes on ALERT. During an alert,
    /// staying in sight fills the lockdown bar (or a sentry closing in) and the courier is caught.
    /// </summary>
    public class DetectionManager : MonoBehaviour
    {
        public float decayDelay = 0.9f;
        public float decayRate = 0.42f;
        public float alertCooldown = 4f;
        public float lockdownTime = 3.6f;
        public float captureRadius = 1.6f;

        public float Meter { get; private set; }
        public float Lockdown { get; private set; }
        public AlertLevel Level { get; private set; } = AlertLevel.Hidden;
        public Vector3 LastKnownPosition { get; private set; }
        public float TimeSinceSeen => unseenTimer;
        public bool SeenThisFrame => seenLastFrame;

        /// <summary>Sensors that saw or heard the courier during the last frame (for HUD threat arrows).</summary>
        public readonly List<SecuritySensor> Watchers = new List<SecuritySensor>();
        readonly List<SecuritySensor> pending = new List<SecuritySensor>();

        float unseenTimer = 99f;
        bool seenThisFrame, seenLastFrame;

        void Awake() => Game.Detection = this;

        public void Report(SecuritySensor sensor, float amount)
        {
            if (Game.State == null || Game.State.Current != GameState.Playing) return;
            Meter = Mathf.Min(1f, Meter + amount);
            seenThisFrame = true;
            if (Game.Player != null) LastKnownPosition = Game.Player.transform.position;
            if (sensor != null && !pending.Contains(sensor)) pending.Add(sensor);
        }

        /// <summary>Instant spike (e.g. walking into a thermal turnstile or showing the wrong passport).</summary>
        public void Spike(float amount, Vector3 at)
        {
            if (Game.State == null || Game.State.Current != GameState.Playing) return;
            Meter = Mathf.Min(1f, Meter + amount);
            LastKnownPosition = at;
            seenThisFrame = true;
        }

        /// <summary>A tripped laser grid or tempo gate: immediate capture.</summary>
        public void Trip(string reason)
        {
            if (Game.State == null || Game.State.Current != GameState.Playing) return;
            Meter = 1f;
            SetLevel(AlertLevel.Alert);
            Game.CameraRig?.AddShake(1f);
            Game.State.OnPlayerCaught(reason);
        }

        void LateUpdate()
        {
            Watchers.Clear();
            Watchers.AddRange(pending);
            pending.Clear();
            seenLastFrame = seenThisFrame;

            if (Game.State == null || Game.State.Current != GameState.Playing)
            {
                seenThisFrame = false;
                return;
            }

            float dt = Time.deltaTime;
            if (seenThisFrame) unseenTimer = 0f;
            else unseenTimer += dt;

            switch (Level)
            {
                case AlertLevel.Hidden:
                case AlertLevel.Suspicious:
                    if (Meter >= 1f) SetLevel(AlertLevel.Alert);
                    else
                    {
                        if (!seenThisFrame && unseenTimer > decayDelay) Meter = Mathf.Max(0f, Meter - decayRate * dt);
                        SetLevel(Meter > 0.15f ? AlertLevel.Suspicious : AlertLevel.Hidden);
                    }
                    break;

                case AlertLevel.Alert:
                    Meter = 1f;
                    if (seenThisFrame)
                    {
                        Lockdown += dt / lockdownTime;
                        if (Lockdown >= 1f)
                        {
                            Lockdown = 0f;
                            Game.State.OnPlayerCaught("SECURITY LOCKDOWN  ·  CREDENTIALS FLAGGED");
                            break;
                        }
                    }
                    else Lockdown = Mathf.Max(0f, Lockdown - dt * 0.35f);
                    if (unseenTimer > alertCooldown) SetLevel(AlertLevel.Searching);
                    CheckSentryCapture();
                    break;

                case AlertLevel.Searching:
                    Lockdown = Mathf.Max(0f, Lockdown - dt);
                    if (seenThisFrame && unseenTimer <= 0f && Meter > 0.6f) { SetLevel(AlertLevel.Alert); break; }
                    Meter = Mathf.Max(0f, Meter - decayRate * 0.8f * dt);
                    if (Meter <= 0.02f) SetLevel(AlertLevel.Hidden);
                    break;
            }
            seenThisFrame = false;
        }

        void CheckSentryCapture()
        {
            var player = Game.Player;
            if (player == null) return;
            Vector3 p = player.transform.position;
            foreach (var s in SecurityRegistry.Sensors)
            {
                if (!(s is SentryAI sentry) || sentry.IsDisabled) continue;
                Vector3 d = sentry.transform.position - p;
                d.y = 0f;
                if (d.magnitude < captureRadius && sentry.transform.position.y - p.y < 4.5f)
                {
                    Game.State.OnPlayerCaught("INTERCEPTED BY FALCON SENTRY");
                    return;
                }
            }
        }

        void SetLevel(AlertLevel next)
        {
            if (Level == next) return;
            var previous = Level;
            Level = next;
            if (next == AlertLevel.Alert)
            {
                Lockdown = 0f;
                Game.Audio?.SetAlarm(true);
                Game.Audio?.PlayMusic(MusicMood.Tension);
                GameEvents.RaiseToast("ALERT  ·  SECURITY GRID ENGAGED", ExpoPalette.Alert, 2.2f);
            }
            else if (previous == AlertLevel.Alert)
            {
                Game.Audio?.SetAlarm(false);
            }
            if (next == AlertLevel.Hidden && (previous == AlertLevel.Searching || previous == AlertLevel.Alert))
            {
                Game.Audio?.PlayMusic(MusicMood.Explore);
                GameEvents.RaiseToast("GRID STANDING DOWN", ExpoPalette.Cyan, 1.8f);
            }
            GameEvents.RaiseAlertLevelChanged(next);
        }

        public void ResetAll()
        {
            Meter = 0f;
            Lockdown = 0f;
            unseenTimer = 99f;
            Watchers.Clear();
            pending.Clear();
            if (Level != AlertLevel.Hidden)
            {
                Game.Audio?.SetAlarm(false);
                Game.Audio?.PlayMusic(MusicMood.Explore);
                Level = AlertLevel.Hidden;
                GameEvents.RaiseAlertLevelChanged(Level);
            }
        }
    }
}
