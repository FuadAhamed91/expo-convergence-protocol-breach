using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>The opening-ceremony countdown. Runs only while the game is in the Playing state.</summary>
    public class CountdownManager : MonoBehaviour
    {
        [Tooltip("Mission length in seconds.")]
        public float missionSeconds = 720f;

        public float Remaining { get; private set; }
        public float Total { get; private set; }
        public bool Running { get; private set; }
        public float Fraction => Total <= 0f ? 0f : Remaining / Total;

        float lastTickSecond = -1f;

        void Awake()
        {
            Game.Countdown = this;
            Remaining = missionSeconds;
            Total = missionSeconds;
        }

        public void Begin(float seconds)
        {
            Total = seconds;
            Remaining = seconds;
            Running = true;
        }

        public void Stop() => Running = false;
        public void Resume() => Running = true;
        public void SetRemaining(float seconds) => Remaining = Mathf.Clamp(seconds, 0f, Mathf.Max(Total, seconds));

        public void Penalize(float seconds)
        {
            Remaining = Mathf.Max(0f, Remaining - seconds);
            GameEvents.RaiseToast($"-{seconds:0} SECONDS  ·  CREDENTIAL PENALTY", ExpoPalette.Alert, 2.2f);
        }

        public void Bonus(float seconds)
        {
            Remaining += seconds;
            GameEvents.RaiseToast($"+{seconds:0} SECONDS", ExpoPalette.Emerald, 2f);
        }

        void Update()
        {
            if (!Running || Game.State == null || Game.State.Current != GameState.Playing) return;
            Remaining -= Time.deltaTime;

            // Clock ticks during the final 30 seconds.
            if (Remaining <= 30f)
            {
                float whole = Mathf.Ceil(Remaining);
                if (whole != lastTickSecond)
                {
                    lastTickSecond = whole;
                    Game.Audio?.PlayUI("tick");
                }
            }

            if (Remaining <= 0f)
            {
                Remaining = 0f;
                Running = false;
                Game.State.OnTimeExpired();
            }
        }

        public static string Format(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }
    }
}
