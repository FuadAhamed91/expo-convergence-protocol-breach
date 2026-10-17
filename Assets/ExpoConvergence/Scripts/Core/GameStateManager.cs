using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ExpoConvergence
{
    /// <summary>Owns the high-level flow: title, briefing, play, pause, caught, failure and victory.</summary>
    public class GameStateManager : MonoBehaviour
    {
        public GameState Current { get; private set; } = GameState.Boot;

        public int TimesCaught { get; private set; }
        public int AlertsTriggered { get; private set; }
        public float MissionStartTime { get; private set; }
        public float MissionElapsed => Current == GameState.Victory ? finishTime - MissionStartTime : Time.time - MissionStartTime;

        GameState stateBeforePause = GameState.Playing;
        float finishTime;
        bool respawning;

        void Awake()
        {
            Game.State = this;
        }

        void OnEnable() => GameEvents.AlertLevelChanged += OnAlertLevelChanged;
        void OnDisable() => GameEvents.AlertLevelChanged -= OnAlertLevelChanged;

        void OnAlertLevelChanged(AlertLevel level)
        {
            if (level == AlertLevel.Alert) AlertsTriggered++;
        }

        public void SetState(GameState next)
        {
            if (next == Current) return;
            var previous = Current;
            Current = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            ApplyCursor();
            GameEvents.RaiseStateChanged(previous, next);
        }

        void ApplyCursor()
        {
            bool locked = Current == GameState.Playing || Current == GameState.Cinematic || Current == GameState.Caught;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (PlayerInput.PausePressed)
            {
                if (Current == GameState.Playing) Pause();
                else if (Current == GameState.Paused) Resume();
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) ApplyCursor();
        }

        public void Pause()
        {
            if (Current != GameState.Playing) return;
            stateBeforePause = Current;
            SetState(GameState.Paused);
            Game.Audio?.PlayUI("ui_click");
        }

        public void Resume()
        {
            if (Current != GameState.Paused) return;
            SetState(stateBeforePause);
            Game.Audio?.PlayUI("ui_click");
        }

        // ---------------------------------------------------------------- flow

        public void ShowTitle()
        {
            SetState(GameState.Title);
            Game.Cinematics?.StartTitleOrbit();
            Game.Audio?.PlayMusic(MusicMood.Explore);
        }

        public void BeginBriefing()
        {
            if (Current != GameState.Title) return;
            SetState(GameState.Briefing);
            Game.Audio?.PlayUI("ui_confirm");
            Game.Cinematics?.StopTitleOrbit();
            StartCoroutine(BriefingRoutine());
        }

        IEnumerator BriefingRoutine()
        {
            Game.UI?.ShowBriefing(true);
            Game.Audio?.PlayVoice("vo_briefing");
            Game.Cinematics?.PlayIntroFlyover();
            // Skip a frame so the key press that started the briefing doesn't also skip it.
            yield return null;
            float t = 0f;
            const float briefingLength = 11f;
            while (t < briefingLength && !PlayerInput.SkipPressed)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Game.UI?.ShowBriefing(false);
            Game.Cinematics?.Stop();
            BeginPlay();
        }

        public void BeginPlay()
        {
            TimesCaught = 0;
            AlertsTriggered = 0;
            MissionStartTime = Time.time;
            Game.Checkpoints?.RespawnPlayer(false);
            Game.CameraRig?.SnapBehindPlayer();
            Game.Countdown?.Begin(Game.Countdown.missionSeconds);
            Game.Progress?.EnterSector(0);
            SetState(GameState.Playing);
            Game.Audio?.PlayMusic(MusicMood.Explore);
            GameEvents.RaiseHint("WASD move  ·  MOUSE look  ·  SHIFT sprint  ·  SPACE slide", 7f);
        }

        public void EnterCinematic() { if (Current == GameState.Playing) SetState(GameState.Cinematic); }
        public void ExitCinematic() { if (Current == GameState.Cinematic) SetState(GameState.Playing); }

        public void OnPlayerCaught(string reason)
        {
            if (Current != GameState.Playing || respawning) return;
            StartCoroutine(CaughtRoutine(reason));
        }

        IEnumerator CaughtRoutine(string reason)
        {
            respawning = true;
            TimesCaught++;
            SetState(GameState.Caught);
            GameEvents.RaisePlayerCaught();
            Game.Audio?.PlaySfx("caught");
            Game.UI?.ShowCaught(reason);
            Game.Countdown?.Penalize(15f);
            yield return new WaitForSeconds(1.6f);
            Game.UI?.Fade(1f, 0.25f);
            yield return new WaitForSeconds(0.3f);
            Game.Checkpoints?.RespawnPlayer(true);
            Game.UI?.HideCaught();
            Game.UI?.Fade(0f, 0.45f);
            respawning = false;
            if (Game.Countdown != null && Game.Countdown.Remaining <= 0f) { OnTimeExpired(); yield break; }
            SetState(GameState.Playing);
        }

        public void OnTimeExpired()
        {
            if (Current == GameState.Victory || Current == GameState.Failed) return;
            SetState(GameState.Failed);
            Game.Audio?.PlaySfx("fail");
            Game.Audio?.PlayMusic(MusicMood.None);
            Game.UI?.ShowFailed();
        }

        /// <summary>Retry after the countdown expired: back to the last checkpoint with two minutes on the clock.</summary>
        public void RetryFromCheckpoint()
        {
            Game.UI?.HideFailed();
            Game.Countdown?.SetRemaining(Mathf.Max(Game.Countdown.Remaining, 120f));
            Game.Countdown?.Resume();
            Game.Checkpoints?.RespawnPlayer(true);
            Game.CameraRig?.SnapBehindPlayer();
            SetState(GameState.Playing);
            Game.Audio?.PlayMusic(MusicMood.Explore);
        }

        public void RestartFromCheckpointFromPause()
        {
            if (Current != GameState.Paused) return;
            SetState(GameState.Playing);
            Game.Checkpoints?.RespawnPlayer(true);
            Game.CameraRig?.SnapBehindPlayer();
        }

        public void RestartMission()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void OnVictory()
        {
            if (Current == GameState.Victory) return;
            finishTime = Time.time;
            Game.Countdown?.Stop();
            SetState(GameState.Victory);
            Game.UI?.ShowVictory();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
