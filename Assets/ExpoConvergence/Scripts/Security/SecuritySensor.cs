using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Base for every security device that can see the courier: vision cone + line of sight,
    /// hearing for sprint noise, Prism disabling, Chrono slowing and state-coloured visuals.
    /// </summary>
    public abstract class SecuritySensor : MonoBehaviour, IPrismTarget, IChronoAffectable, IResettable
    {
        public SensorProfile profile;
        public Transform eye;
        public string displayName = "SENSOR";
        public bool prismDisableable = true;
        [Tooltip("Optional separate horizontal/vertical field of view (degrees). Zero uses the profile FOV.")]
        public Vector2 fovOverride;

        // Visuals
        public Renderer coneRenderer;
        public Renderer[] statusRenderers;
        public Light statusLight;
        Material coneMat;
        Material[] statusMats;
        Color currentColor;
        float coneAlpha = 0.2f;

        protected float disabledTimer;
        protected float chronoScale = 1f;
        float frozenFlash;

        public bool IsDisabled => disabledTimer > 0f;
        public bool SeesPlayer { get; private set; }
        public bool HearsPlayer { get; private set; }
        public float SeeStrength { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public float ChronoScale => chronoScale;
        public SensorKind Kind => profile != null ? profile.kind : SensorKind.Optical;

        public Vector3 TargetPoint => eye != null ? eye.position : transform.position;
        public bool IsPrismDisableable => prismDisableable;
        public string DisplayName => displayName;
        public Vector3 ChronoPosition => transform.position;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        protected virtual void OnEnable() => SecurityRegistry.Register(this);
        protected virtual void OnDisable() => SecurityRegistry.Unregister(this);

        public void SetupVisuals(Renderer cone, Renderer[] status, Light light, float alpha = 0.2f)
        {
            coneRenderer = cone;
            statusRenderers = status;
            statusLight = light;
            coneAlpha = alpha;
            if (coneRenderer != null)
            {
                coneMat = ExpoMaterials.Cone(ExpoPalette.Cyan, alpha);
                coneRenderer.sharedMaterial = coneMat;
                coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (statusRenderers != null)
            {
                statusMats = new Material[statusRenderers.Length];
                for (int i = 0; i < statusRenderers.Length; i++)
                {
                    statusMats[i] = ExpoMaterials.EmissiveInstance(ExpoPalette.Cyan, 3f);
                    if (statusRenderers[i] != null) statusRenderers[i].sharedMaterial = statusMats[i];
                }
            }
            ApplyColor(IdleColor, 1f);
        }

        protected virtual Color IdleColor => Kind == SensorKind.Thermal ? new Color(1f, 0.55f, 0.2f) : ExpoPalette.Cyan;

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            bool active = Game.State != null && (Game.State.Current == GameState.Playing || Game.State.Current == GameState.Title || Game.State.Current == GameState.Briefing);
            if (!active) return;
            bool gameplay = Game.State.Current == GameState.Playing;

            if (disabledTimer > 0f)
            {
                disabledTimer -= dt;
                SeesPlayer = false;
                HearsPlayer = false;
                frozenFlash += dt;
                if (disabledTimer <= 0f) OnReenabled();
                UpdateVisuals(dt);
                return;
            }

            Tick(dt * chronoScale, dt);
            if (gameplay) Sense(dt);
            else { SeesPlayer = false; HearsPlayer = false; }
            UpdateVisuals(dt);
        }

        /// <summary>Movement/scanning update. scaledDt includes the Chrono slow-down.</summary>
        protected abstract void Tick(float scaledDt, float dt);

        void Sense(float dt)
        {
            SeesPlayer = false;
            HearsPlayer = false;
            SeeStrength = 0f;
            var player = Game.Player;
            if (player == null || profile == null || eye == null) return;

            Vector3 target = player.SensorTarget;
            Vector3 to = target - eye.position;
            float dist = to.magnitude;
            bool blindToPlayer = profile.kind == SensorKind.Thermal && player.IsCloaked;

            if (!blindToPlayer && dist <= profile.range && dist > 0.01f && InFieldOfView(to) && HasLineOfSight(target, dist))
            {
                SeesPlayer = true;
                float distFactor = Mathf.Lerp(1.8f, 0.5f, dist / profile.range);
                SeeStrength = distFactor * (player.IsSliding ? 0.75f : 1f);
            }
            else if (!blindToPlayer && profile.hearingRadius > 0f && player.NoiseLevel > 0.01f
                     && dist <= profile.hearingRadius * player.NoiseLevel && HasLineOfSight(target, dist))
            {
                HearsPlayer = true;
                SeeStrength = 0.55f;
            }

            if (SeesPlayer || HearsPlayer)
            {
                LastSeenPosition = player.transform.position;
                Game.Detection?.Report(this, SeeStrength * profile.detectionRate * dt * Mathf.Max(0.25f, chronoScale));
            }
        }

        protected virtual bool InFieldOfView(Vector3 toTarget)
        {
            if (fovOverride.x > 0f)
            {
                Vector3 local = eye.InverseTransformDirection(toTarget);
                if (local.z <= 0f) return false;
                float h = Mathf.Atan2(Mathf.Abs(local.x), local.z) * Mathf.Rad2Deg;
                float v = Mathf.Atan2(Mathf.Abs(local.y), local.z) * Mathf.Rad2Deg;
                return h <= fovOverride.x * 0.5f && v <= fovOverride.y * 0.5f;
            }
            return Vector3.Angle(eye.forward, toTarget) <= profile.fieldOfView * 0.5f;
        }

        protected bool HasLineOfSight(Vector3 target, float dist)
        {
            Vector3 dir = (target - eye.position) / dist;
            float start = Mathf.Min(0.35f, dist * 0.5f);
            if (Physics.Raycast(eye.position + dir * start, dir, out var hit, dist - start + 0.2f, ExpoLayers.SightMask, QueryTriggerInteraction.Ignore))
                return hit.collider.gameObject.layer == ExpoLayers.Player;
            return true;
        }

        void UpdateVisuals(float dt)
        {
            Color c;
            float intensity = 1f;
            var level = Game.Detection != null ? Game.Detection.Level : AlertLevel.Hidden;
            if (IsDisabled)
            {
                c = ExpoPalette.Disabled;
                intensity = 0.25f + 0.2f * Mathf.Sin(frozenFlash * 18f) * Mathf.Clamp01(disabledTimer);
            }
            else if (SeesPlayer && level == AlertLevel.Alert) c = ExpoPalette.Alert;
            else if (SeesPlayer || HearsPlayer) c = ExpoPalette.Suspicious;
            else if (level == AlertLevel.Alert || level == AlertLevel.Searching) c = Color.Lerp(IdleColor, ExpoPalette.Alert, 0.65f);
            else c = IdleColor;

            if (chronoScale < 0.99f) c = Color.Lerp(c, ExpoPalette.Emerald, 0.55f);
            currentColor = Color.Lerp(currentColor, c, 1f - Mathf.Exp(-10f * dt));
            ApplyColor(currentColor, intensity);
        }

        void ApplyColor(Color c, float intensity)
        {
            if (coneMat != null)
            {
                coneMat.SetColor(ColorId, ExpoPalette.Hdr(c, 1.4f));
                coneMat.SetFloat(AlphaId, IsDisabled ? coneAlpha * 0.15f : coneAlpha * (SeesPlayer ? 1.6f : 1f));
            }
            if (statusMats != null)
                for (int i = 0; i < statusMats.Length; i++) ExpoMaterials.SetEmission(statusMats[i], c, 3.2f * intensity);
            if (statusLight != null)
            {
                statusLight.color = c;
                statusLight.enabled = !IsDisabled;
            }
        }

        public virtual void PrismDisable(float seconds)
        {
            if (!prismDisableable) return;
            bool wasDisabled = IsDisabled;
            disabledTimer = Mathf.Max(disabledTimer, seconds);
            frozenFlash = 0f;
            if (!wasDisabled) OnDisabledByPrism();
        }

        protected virtual void OnDisabledByPrism()
        {
            Game.Audio?.PlaySfxAt("disable", TargetPoint, 1f);
            VfxFactory.Sparks(TargetPoint, ExpoPalette.Blue, 26);
        }

        protected virtual void OnReenabled()
        {
            Game.Audio?.PlaySfxAt("reboot", TargetPoint, 0.6f);
        }

        public virtual void SetChronoScale(float scale) => chronoScale = scale;

        public virtual void ResetState()
        {
            disabledTimer = 0f;
            chronoScale = 1f;
            SeesPlayer = false;
            HearsPlayer = false;
        }
    }
}
