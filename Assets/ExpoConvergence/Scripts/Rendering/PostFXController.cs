using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ExpoConvergence
{
    /// <summary>
    /// Global post-processing: a filmic night grade (bloom, ACES, grade, vignette, grain) plus blended
    /// overlays for alerts, the Chrono Lock, the Mirage Cloak and being caught.
    /// </summary>
    public class PostFXController : MonoBehaviour
    {
        public enum Effect { Cloak, Chrono }

        Volume baseVol, alertVol, chronoVol, cloakVol, caughtVol;
        public Bloom BaseBloom { get; private set; }
        public FilmGrain Grain { get; private set; }
        public ChromaticAberration Chromatic { get; private set; }
        float cloakTimer, cloakDuration, chronoTimer, chronoDuration;

        void Awake() => Game.PostFX = this;

        public void Build()
        {
            baseVol = MakeVolume("PostFX_Base", 0, out var p);
            BaseBloom = p.Add<Bloom>(true);
            BaseBloom.threshold.Override(0.92f);
            BaseBloom.intensity.Override(1.2f);
            BaseBloom.scatter.Override(0.72f);
            BaseBloom.tint.Override(new Color(1f, 0.97f, 0.95f));
            BaseBloom.highQualityFiltering.Override(false);
            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(16f);
            ca.saturation.Override(14f);
            ca.colorFilter.Override(Color.white);
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(-8f);
            wb.tint.Override(4f);
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.97f, 1.1f, 0f));
            smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.95f, 0f));
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.01f, 0.01f, 0.03f));
            Chromatic = p.Add<ChromaticAberration>(true);
            Chromatic.intensity.Override(0.06f);
            Grain = p.Add<FilmGrain>(true);
            Grain.type.Override(FilmGrainLookup.Thin1);
            Grain.intensity.Override(0.16f);
            Grain.response.Override(0.85f);

            alertVol = MakeVolume("PostFX_Alert", 1, out p);
            var aca = p.Add<ColorAdjustments>(true);
            aca.colorFilter.Override(new Color(1f, 0.74f, 0.76f));
            aca.saturation.Override(-5f);
            var avig = p.Add<Vignette>(true);
            avig.intensity.Override(0.44f);
            avig.color.Override(new Color(0.45f, 0f, 0.02f));
            avig.smoothness.Override(0.55f);
            p.Add<ChromaticAberration>(true).intensity.Override(0.22f);

            chronoVol = MakeVolume("PostFX_Chrono", 2, out p);
            var cca = p.Add<ColorAdjustments>(true);
            cca.saturation.Override(-38f);
            cca.colorFilter.Override(new Color(0.82f, 1.05f, 0.9f));
            var cvig = p.Add<Vignette>(true);
            cvig.intensity.Override(0.4f);
            cvig.color.Override(new Color(0f, 0.18f, 0.08f));
            p.Add<ChromaticAberration>(true).intensity.Override(0.4f);
            var lens = p.Add<LensDistortion>(true);
            lens.intensity.Override(-0.12f);

            cloakVol = MakeVolume("PostFX_Cloak", 3, out p);
            var kca = p.Add<ColorAdjustments>(true);
            kca.colorFilter.Override(new Color(1.06f, 0.96f, 0.8f));
            kca.saturation.Override(-10f);
            var kvig = p.Add<Vignette>(true);
            kvig.intensity.Override(0.38f);
            kvig.color.Override(new Color(0.3f, 0.18f, 0.02f));
            var kb = p.Add<Bloom>(true);
            kb.intensity.Override(1.7f);
            kb.threshold.Override(0.85f);

            caughtVol = MakeVolume("PostFX_Caught", 4, out p);
            var xca = p.Add<ColorAdjustments>(true);
            xca.saturation.Override(-85f);
            xca.contrast.Override(35f);
            var xvig = p.Add<Vignette>(true);
            xvig.intensity.Override(0.55f);
            xvig.color.Override(new Color(0.5f, 0f, 0f));
            p.Add<ChromaticAberration>(true).intensity.Override(0.75f);
        }

        Volume MakeVolume(string name, int priority, out VolumeProfile profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            v.sharedProfile = profile;
            v.weight = priority == 0 ? 1f : 0f;
            return v;
        }

        public void Pulse(Effect effect, float duration)
        {
            if (effect == Effect.Cloak) { cloakTimer = duration; cloakDuration = duration; }
            else { chronoTimer = duration; chronoDuration = duration; }
        }

        void Update()
        {
            if (baseVol == null) return;
            float dt = Time.unscaledDeltaTime;
            bool paused = Game.State != null && Game.State.Current == GameState.Paused;

            float alertTarget = 0f;
            if (Game.Detection != null)
            {
                var lvl = Game.Detection.Level;
                if (lvl == AlertLevel.Alert) alertTarget = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f);
                else if (lvl == AlertLevel.Searching) alertTarget = 0.4f;
                else alertTarget = Game.Detection.Meter * 0.35f;
            }
            alertVol.weight = Mathf.MoveTowards(alertVol.weight, alertTarget, dt * 2.5f);

            if (!paused)
            {
                cloakTimer -= Time.deltaTime;
                chronoTimer -= Time.deltaTime;
            }
            cloakVol.weight = Envelope(cloakTimer, cloakDuration);
            chronoVol.weight = Envelope(chronoTimer, chronoDuration);

            bool caught = Game.State != null && Game.State.Current == GameState.Caught;
            caughtVol.weight = Mathf.MoveTowards(caughtVol.weight, caught ? 1f : 0f, dt * (caught ? 6f : 2f));
        }

        static float Envelope(float timer, float duration)
        {
            if (timer <= 0f || duration <= 0f) return 0f;
            float elapsed = duration - timer;
            return Mathf.Clamp01(Mathf.Min(elapsed / 0.25f, timer / 0.45f));
        }

        public void SetCheapMode(bool cheap)
        {
            if (Grain != null) Grain.active = !cheap;
            if (Chromatic != null) Chromatic.active = !cheap;
            if (BaseBloom != null) BaseBloom.downscale.Override(cheap ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
        }
    }
}
