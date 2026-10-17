using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ExpoConvergence
{
    /// <summary>
    /// Entry point. Drop this on one GameObject in an empty scene: it builds the managers, camera,
    /// Expo City, the courier, the UI and post-processing, then shows the title screen.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public float missionSeconds = 720f;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Time.timeScale = 1f;

            // Remove template objects if this runs in a scene that still has them.
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.transform.root != transform) Destroy(l.gameObject);
            foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)) Destroy(v.gameObject);

#if UNITY_EDITOR
            // In the editor, deliver input to the game even when the Game view panel is not the focused window.
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            gameObject.AddComponent<InputSampler>();

            var managers = new GameObject("Managers").transform;
            managers.SetParent(transform, false);
            T Add<T>(string n) where T : Component
            {
                var go = new GameObject(n);
                go.transform.SetParent(managers, false);
                return go.AddComponent<T>();
            }
            Add<GameStateManager>("GameState");
            Add<DetectionManager>("Detection");
            var countdown = Add<CountdownManager>("Countdown");
            countdown.missionSeconds = missionSeconds;
            Add<SectorProgressionManager>("Progression");
            Add<CheckpointSystem>("Checkpoints");
            Add<AudioManager>("Audio");
            var postfx = Add<PostFXController>("PostFX");
            var cinematics = Add<CinematicDirector>("Cinematics");
            Add<QualityAutoTuner>("Quality");

            // Camera.
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 650f;
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.Medium;
            // Dithering hides 8-bit banding in the dark sky gradients and the moon halo.
            camData.dithering = true;
            camData.renderShadows = true;
            var rig = cam.GetComponent<ThirdPersonCameraController>();
            if (rig == null) rig = cam.gameObject.AddComponent<ThirdPersonCameraController>();
            cinematics.Bind(cam);

            // World.
            var world = new GameObject("ExpoCity").AddComponent<ExpoWorld>();
            world.Build();

            // Courier.
            var playerGo = new GameObject("Tariq");
            playerGo.transform.SetPositionAndRotation(world.PlayerStart, Quaternion.Euler(0, world.PlayerStartYaw, 0));
            playerGo.AddComponent<CharacterController>();
            var player = playerGo.AddComponent<PlayerCourierController>();
            playerGo.AddComponent<PassportManager>();
            rig.target = player.CameraTarget;
            rig.SnapBehindPlayer();

            // UI and post.
            new GameObject("UI").AddComponent<UIManager>().Build();
            postfx.Build();
        }

        IEnumerator Start()
        {
            // Let one frame render so lights and emissive materials are live before capturing reflections.
            yield return null;
            Game.World?.RenderReflectionProbes();
            Game.State?.ShowTitle();
        }
    }
}
