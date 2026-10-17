using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Drives the main camera for cutscenes: the title orbit, the intro flyover, landmark activations
    /// and the finale. Blends back to the gameplay camera pose when a sequence ends.
    /// </summary>
    public class CinematicDirector : MonoBehaviour
    {
        public struct Shot
        {
            public Vector3 fromPos, toPos;
            public Vector3 fromLook, toLook;
            public float duration;
            public float fromFov, toFov;
            // Orbit shots sweep an angle around a centre instead of moving in a straight line.
            public bool orbit;
            public Vector3 center;
            public float radius, height, fromDeg, toDeg;

            public static Shot Move(Vector3 fromPos, Vector3 toPos, Vector3 fromLook, Vector3 toLook, float duration, float fov = 55f, float toFov = -1f)
            {
                return new Shot { fromPos = fromPos, toPos = toPos, fromLook = fromLook, toLook = toLook, duration = duration, fromFov = fov, toFov = toFov < 0 ? fov : toFov };
            }

            public static Shot Static(Vector3 pos, Vector3 look, float duration, float fov = 55f) => Move(pos, pos, look, look, duration, fov);
        }

        public bool IsPlaying { get; private set; }
        Transform cam;
        Camera camComponent;
        Coroutine routine;
        bool titleOrbit;
        float orbitAngle;
        public Vector3 orbitCenter = new Vector3(0, 16f, 0);
        public float orbitRadius = 66f;
        public float orbitHeight = 20f;

        void Awake() => Game.Cinematics = this;

        public void Bind(Camera camera)
        {
            camComponent = camera;
            cam = camera.transform;
        }

        // ---------------------------------------------------------------- title orbit

        public void StartTitleOrbit()
        {
            titleOrbit = true;
            IsPlaying = true;
            orbitAngle = 205f;
        }

        public void StopTitleOrbit()
        {
            titleOrbit = false;
            IsPlaying = false;
        }

        void LateUpdate()
        {
            if (!titleOrbit || cam == null) return;
            orbitAngle += Time.unscaledDeltaTime * 3.2f;
            float a = orbitAngle * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(a) * orbitRadius, orbitHeight + Mathf.Sin(a * 0.5f) * 4f, Mathf.Sin(a) * orbitRadius);
            cam.position = pos;
            cam.rotation = Quaternion.LookRotation(orbitCenter - pos, Vector3.up);
            if (camComponent) camComponent.fieldOfView = 50f;
        }

        // ---------------------------------------------------------------- sequences

        public void Play(IList<Shot> shots, Action onComplete = null, bool letterbox = true, bool blendBack = true, bool pauseGameplay = true)
        {
            if (cam == null) { onComplete?.Invoke(); return; }
            if (routine != null) StopCoroutine(routine);
            titleOrbit = false;
            routine = StartCoroutine(Run(new List<Shot>(shots), onComplete, letterbox, blendBack, pauseGameplay));
        }

        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            IsPlaying = false;
            titleOrbit = false;
            Game.UI?.SetLetterbox(false);
        }

        IEnumerator Run(List<Shot> shots, Action onComplete, bool letterbox, bool blendBack, bool pauseGameplay)
        {
            IsPlaying = true;
            if (pauseGameplay) Game.State?.EnterCinematic();
            if (letterbox) Game.UI?.SetLetterbox(true);

            foreach (var shot in shots)
            {
                float t = 0f;
                while (t < shot.duration)
                {
                    t += Time.deltaTime;
                    float k = Smooth(Mathf.Clamp01(t / shot.duration));
                    Vector3 p;
                    if (shot.orbit)
                    {
                        float a = Mathf.Lerp(shot.fromDeg, shot.toDeg, k) * Mathf.Deg2Rad;
                        p = shot.center + new Vector3(Mathf.Cos(a) * shot.radius, shot.height, Mathf.Sin(a) * shot.radius);
                    }
                    else p = Vector3.Lerp(shot.fromPos, shot.toPos, k);
                    Vector3 l = Vector3.Lerp(shot.fromLook, shot.toLook, k);
                    cam.position = p;
                    cam.rotation = Quaternion.LookRotation((l - p).normalized, Vector3.up);
                    if (camComponent) camComponent.fieldOfView = Mathf.Lerp(shot.fromFov, shot.toFov, k);
                    yield return null;
                }
            }

            if (blendBack && Game.CameraRig != null)
            {
                Vector3 startPos = cam.position;
                Quaternion startRot = cam.rotation;
                float startFov = camComponent ? camComponent.fieldOfView : 60f;
                float t = 0f;
                const float blend = 0.9f;
                while (t < blend)
                {
                    t += Time.deltaTime;
                    float k = Smooth(Mathf.Clamp01(t / blend));
                    Game.CameraRig.ComputePose(out var gp, out var gr, true);
                    cam.position = Vector3.Lerp(startPos, gp, k);
                    cam.rotation = Quaternion.Slerp(startRot, gr, k);
                    if (camComponent) camComponent.fieldOfView = Mathf.Lerp(startFov, Game.CameraRig.baseFov, k);
                    yield return null;
                }
            }

            if (letterbox) Game.UI?.SetLetterbox(false);
            IsPlaying = false;
            routine = null;
            if (pauseGameplay) Game.State?.ExitCinematic();
            onComplete?.Invoke();
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>Opening flyover over the dark district towards the courier (plays under the briefing text).</summary>
        public void PlayIntroFlyover()
        {
            var world = Game.World;
            Vector3 start = world != null ? world.PlayerStart : Vector3.zero;
            var shots = new List<Shot>
            {
                Shot.Move(new Vector3(-70, 34, -95), new Vector3(-20, 22, -78), new Vector3(0, 8, 0), new Vector3(0, 6, 0), 5.5f, 52f, 50f),
                Shot.Move(new Vector3(18, 9, -66), start + new Vector3(2.5f, 2.2f, -4.5f), start + new Vector3(0, 6, 30), start + Vector3.up * 1.4f, 5.5f, 50f, 58f),
            };
            Play(shots, null, true, false, false);
        }

        /// <summary>One smooth orbit shot around a point (landmark activations and the finale).</summary>
        public static Shot Orbit(Vector3 center, float radius, float height, float fromDeg, float toDeg, float duration, Vector3 lookFrom, Vector3 lookTo, float fov = 50f, float toFov = -1f)
        {
            return new Shot
            {
                orbit = true, center = center, radius = radius, height = height, fromDeg = fromDeg, toDeg = toDeg,
                fromLook = lookFrom, toLook = lookTo, duration = duration, fromFov = fov, toFov = toFov < 0 ? fov : toFov,
            };
        }
    }
}
