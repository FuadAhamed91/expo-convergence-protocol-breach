using System;
using UnityEngine;

namespace ExpoConvergence
{
    public enum PassportType { UAE = 0, France = 1, UK = 2 }

    public enum GameState { Boot, Title, Briefing, Playing, Paused, Cinematic, Caught, Failed, Victory }

    /// <summary>Thermal sensors are fooled by the Mirage Cloak; optical ones are not.</summary>
    public enum SensorKind { Thermal, Optical }

    public enum AlertLevel { Hidden, Suspicious, Alert, Searching }

    public static class ExpoPalette
    {
        public static readonly Color Gold = new Color(1.00f, 0.70f, 0.24f);
        public static readonly Color Blue = new Color(0.26f, 0.56f, 1.00f);
        public static readonly Color Emerald = new Color(0.18f, 1.00f, 0.58f);
        public static readonly Color Cyan = new Color(0.24f, 0.90f, 1.00f);
        public static readonly Color Suspicious = new Color(1.00f, 0.80f, 0.22f);
        public static readonly Color Alert = new Color(1.00f, 0.17f, 0.22f);
        public static readonly Color Disabled = new Color(0.55f, 0.75f, 1.00f);
        public static readonly Color Navy = new Color(0.025f, 0.04f, 0.085f);
        public static readonly Color Ink = new Color(0.04f, 0.06f, 0.11f);
        public static readonly Color Text = new Color(0.90f, 0.95f, 1.00f);
        public static readonly Color TextDim = new Color(0.55f, 0.64f, 0.78f);

        public static Color ForPassport(PassportType p)
        {
            switch (p)
            {
                case PassportType.UAE: return Gold;
                case PassportType.France: return Blue;
                default: return Emerald;
            }
        }

        public static string CountryName(PassportType p)
        {
            switch (p)
            {
                case PassportType.UAE: return "UAE";
                case PassportType.France: return "FRANCE";
                default: return "UNITED KINGDOM";
            }
        }

        public static string AbilityName(PassportType p)
        {
            switch (p)
            {
                case PassportType.UAE: return "MIRAGE CLOAK";
                case PassportType.France: return "PRISM PULSE";
                default: return "CHRONO LOCK";
            }
        }

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }

        public static Color Hdr(Color c, float intensity) => new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);
    }

    /// <summary>Physics layers created by the project setup (Expo/Setup Project).</summary>
    public static class ExpoLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Player = 8;
        public const int Sensor = 9;
        public const int Interactable = 10;

        public static int EnvironmentMask => (1 << Default);
        public static int SightMask => (1 << Default) | (1 << Player);
    }

    /// <summary>Global, decoupled game events. Cleared on every play session.</summary>
    public static class GameEvents
    {
        public static event Action<GameState, GameState> StateChanged;
        public static event Action<PassportType> PassportChanged;
        public static event Action<PassportType> PassportUnlocked;
        public static event Action<PassportType> AbilityActivated;
        public static event Action<AlertLevel> AlertLevelChanged;
        public static event Action<string, Color, float> Toast;
        public static event Action<string, string> ObjectiveChanged;
        public static event Action<int> LandmarkRestored;
        public static event Action PlayerCaught;
        public static event Action PlayerRespawned;
        public static event Action<string, float> Hint;

        public static void RaiseStateChanged(GameState from, GameState to) => StateChanged?.Invoke(from, to);
        public static void RaisePassportChanged(PassportType p) => PassportChanged?.Invoke(p);
        public static void RaisePassportUnlocked(PassportType p) => PassportUnlocked?.Invoke(p);
        public static void RaiseAbilityActivated(PassportType p) => AbilityActivated?.Invoke(p);
        public static void RaiseAlertLevelChanged(AlertLevel a) => AlertLevelChanged?.Invoke(a);
        public static void RaiseToast(string message, Color color, float seconds = 3f) => Toast?.Invoke(message, color, seconds);
        public static void RaiseObjective(string sector, string objective) => ObjectiveChanged?.Invoke(sector, objective);
        public static void RaiseLandmarkRestored(int index) => LandmarkRestored?.Invoke(index);
        public static void RaisePlayerCaught() => PlayerCaught?.Invoke();
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        public static void RaiseHint(string text, float seconds = 5f) => Hint?.Invoke(text, seconds);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            StateChanged = null; PassportChanged = null; PassportUnlocked = null; AbilityActivated = null;
            AlertLevelChanged = null; Toast = null; ObjectiveChanged = null; LandmarkRestored = null;
            PlayerCaught = null; PlayerRespawned = null; Hint = null;
        }
    }

    /// <summary>Service registry filled in by <see cref="GameBootstrap"/>.</summary>
    public static class Game
    {
        public static GameStateManager State;
        public static DetectionManager Detection;
        public static CountdownManager Countdown;
        public static SectorProgressionManager Progress;
        public static CheckpointSystem Checkpoints;
        public static PlayerCourierController Player;
        public static PassportManager Passports;
        public static ThirdPersonCameraController CameraRig;
        public static CinematicDirector Cinematics;
        public static UIManager UI;
        public static AudioManager Audio;
        public static PostFXController PostFX;
        public static ExpoWorld World;

        public static bool InputEnabled => State != null && State.Current == GameState.Playing && (Cinematics == null || !Cinematics.IsPlaying);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            State = null; Detection = null; Countdown = null; Progress = null; Checkpoints = null; Player = null;
            Passports = null; CameraRig = null; Cinematics = null; UI = null; Audio = null; PostFX = null; World = null;
        }
    }
}
