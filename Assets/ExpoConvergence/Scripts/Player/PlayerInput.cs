using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ExpoConvergence
{
    /// <summary>
    /// Keyboard and mouse bindings, read straight from the Input System devices.
    /// Button presses are detected from per-frame state changes (sampled by <see cref="InputSampler"/>)
    /// as well as wasPressedThisFrame, so injected events (e.g. automated play-tests) register too.
    /// </summary>
    public static class PlayerInput
    {
        public static float MouseSensitivity = 0.11f;
        public static bool InvertY;

        static Keyboard Kb => Keyboard.current;
        static Mouse Ms => Mouse.current;

        static readonly Key[] Tracked =
        {
            Key.Enter, Key.NumpadEnter, Key.Space, Key.Escape, Key.E,
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Numpad1, Key.Numpad2, Key.Numpad3,
        };
        static readonly Dictionary<Key, bool> previous = new Dictionary<Key, bool>();
        static readonly HashSet<Key> pressed = new HashSet<Key>();
        static bool leftPrev, rightPrev, leftPressed, rightPressed;
        static int sampledFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            previous.Clear(); pressed.Clear();
            leftPrev = rightPrev = leftPressed = rightPressed = false;
            sampledFrame = -1;
        }

        /// <summary>Called once at the start of every frame.</summary>
        public static void Sample()
        {
            if (sampledFrame == Time.frameCount) return;
            sampledFrame = Time.frameCount;
            pressed.Clear();
            var kb = Kb;
            if (kb != null)
            {
                foreach (var key in Tracked)
                {
                    var control = kb[key];
                    bool now = control.isPressed;
                    previous.TryGetValue(key, out bool before);
                    if ((now && !before) || control.wasPressedThisFrame) pressed.Add(key);
                    previous[key] = now;
                }
            }
            var ms = Ms;
            if (ms != null)
            {
                bool l = ms.leftButton.isPressed, r = ms.rightButton.isPressed;
                leftPressed = (l && !leftPrev) || ms.leftButton.wasPressedThisFrame;
                rightPressed = (r && !rightPrev) || ms.rightButton.wasPressedThisFrame;
                leftPrev = l;
                rightPrev = r;
            }
        }

        static bool Pressed(Key k) { Sample(); return pressed.Contains(k); }

        public static Vector2 Move
        {
            get
            {
                if (!Game.InputEnabled || Kb == null) return Vector2.zero;
                Vector2 v = Vector2.zero;
                if (Kb.wKey.isPressed || Kb.upArrowKey.isPressed) v.y += 1f;
                if (Kb.sKey.isPressed || Kb.downArrowKey.isPressed) v.y -= 1f;
                if (Kb.dKey.isPressed || Kb.rightArrowKey.isPressed) v.x += 1f;
                if (Kb.aKey.isPressed || Kb.leftArrowKey.isPressed) v.x -= 1f;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static Vector2 Look
        {
            get
            {
                if (Ms == null) return Vector2.zero;
                Vector2 d = Ms.delta.ReadValue() * MouseSensitivity;
                if (InvertY) d.y = -d.y;
                return d;
            }
        }

        public static float Zoom => Ms == null ? 0f : Ms.scroll.ReadValue().y;
        public static bool Sprint => Game.InputEnabled && Kb != null && (Kb.leftShiftKey.isPressed || Kb.rightShiftKey.isPressed);
        public static bool SlidePressed => Game.InputEnabled && Pressed(Key.Space);
        public static bool InteractHeld => Game.InputEnabled && Kb != null && Kb.eKey.isPressed;
        public static bool InteractPressed => Game.InputEnabled && Pressed(Key.E);
        public static bool AbilityPressed { get { Sample(); return Game.InputEnabled && rightPressed; } }
        public static bool PausePressed => Pressed(Key.Escape);

        /// <summary>Returns 0, 1 or 2 when a passport key was pressed this frame, otherwise -1.</summary>
        public static int PassportPressed
        {
            get
            {
                if (!Game.InputEnabled) return -1;
                if (Pressed(Key.Digit1) || Pressed(Key.Numpad1)) return 0;
                if (Pressed(Key.Digit2) || Pressed(Key.Numpad2)) return 1;
                if (Pressed(Key.Digit3) || Pressed(Key.Numpad3)) return 2;
                return -1;
            }
        }

        public static bool AnyConfirmPressed
        {
            get
            {
                Sample();
                return pressed.Contains(Key.Enter) || pressed.Contains(Key.NumpadEnter) || pressed.Contains(Key.Space) || leftPressed;
            }
        }

        public static bool SkipPressed
        {
            get
            {
                Sample();
                return pressed.Contains(Key.Enter) || pressed.Contains(Key.NumpadEnter) || pressed.Contains(Key.Space) || pressed.Contains(Key.Escape) || leftPressed;
            }
        }
    }

    /// <summary>Samples input at the very start of each frame, before any gameplay script reads it.</summary>
    [DefaultExecutionOrder(-1000)]
    public class InputSampler : MonoBehaviour
    {
        void Update() => PlayerInput.Sample();
    }
}
