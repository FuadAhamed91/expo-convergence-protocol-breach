using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ExpoConvergence
{
    /// <summary>
    /// Keeps the game smooth on integrated GPUs: three presets (Quality / Balanced / Performance) and an
    /// automatic step-down when the frame rate stays under target during play.
    /// </summary>
    public class QualityAutoTuner : MonoBehaviour
    {
        public enum Preset { Quality = 0, Balanced = 1, Performance = 2 }
        public static readonly string[] PresetNames = { "QUALITY", "BALANCED", "PERFORMANCE" };

        public Preset Current { get; private set; } = Preset.Quality;
        public float SmoothedFps { get; private set; } = 60f;
        public bool AutoAdjust = true;

        UniversalRenderPipelineAsset asset;
        float originalScale, originalShadowDistance;
        UpscalingFilterSelection originalFilter;
        float playTime, sampleTime;
        int sampleFrames;
        int stepsTaken;

        public static QualityAutoTuner Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                originalScale = asset.renderScale;
                originalShadowDistance = asset.shadowDistance;
                originalFilter = asset.upscalingFilter;
            }
            Apply(Preset.Quality);
        }

        void OnDestroy()
        {
            // The pipeline asset is shared with the editor: put it back the way it was.
            if (asset != null)
            {
                asset.renderScale = originalScale;
                asset.shadowDistance = originalShadowDistance;
                asset.upscalingFilter = originalFilter;
            }
            if (Instance == this) Instance = null;
        }

        public void Apply(Preset p)
        {
            Current = p;
            if (asset != null)
            {
                switch (p)
                {
                    case Preset.Quality:
                        asset.renderScale = 1f;
                        asset.shadowDistance = 45f;
                        asset.upscalingFilter = UpscalingFilterSelection.Auto;
                        break;
                    case Preset.Balanced:
                        asset.renderScale = 0.85f;
                        asset.shadowDistance = 35f;
                        asset.upscalingFilter = UpscalingFilterSelection.FSR;
                        break;
                    case Preset.Performance:
                        asset.renderScale = 0.72f;
                        asset.shadowDistance = 25f;
                        asset.upscalingFilter = UpscalingFilterSelection.FSR;
                        break;
                }
            }
            Game.PostFX?.SetCheapMode(p != Preset.Quality);
        }

        public void Cycle()
        {
            AutoAdjust = false;
            Apply((Preset)(((int)Current + 1) % 3));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) SmoothedFps = Mathf.Lerp(SmoothedFps, 1f / dt, 0.05f);
            if (!AutoAdjust || Game.State == null || Game.State.Current != GameState.Playing) return;

            playTime += dt;
            if (playTime < 4f) return;   // let the scene settle
            sampleTime += dt;
            sampleFrames++;
            if (sampleTime < 3f) return;

            float fps = sampleFrames / sampleTime;
            sampleTime = 0f;
            sampleFrames = 0;
            if (fps < 50f && Current != Preset.Performance && stepsTaken < 2)
            {
                stepsTaken++;
                Apply(Current + 1);
                Debug.Log($"[QualityAutoTuner] {fps:F0} fps, switching to {PresetNames[(int)Current]}");
            }
        }
    }
}
