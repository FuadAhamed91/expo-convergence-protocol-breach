using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>Base for the three passport abilities.</summary>
    public abstract class PassportAbilityBase : MonoBehaviour
    {
        protected PassportDefinition def;
        protected PassportManager manager;
        protected PlayerCourierController player;
        protected StickmanRig rig;
        protected float runTimer;

        public bool IsRunning => runTimer > 0f;
        public float RunFraction => def == null || def.duration <= 0f ? 0f : Mathf.Clamp01(runTimer / def.duration);

        public void Init(PassportDefinition definition, PassportManager owner)
        {
            def = definition;
            manager = owner;
            player = GetComponent<PlayerCourierController>();
            rig = GetComponent<StickmanRig>();
        }

        /// <summary>Returns false when the ability could not be used (no cooldown is spent).</summary>
        public abstract bool Activate();

        public virtual void Cancel() { runTimer = 0f; }
    }

    // ===================================================================== UAE

    /// <summary>
    /// UAE · Mirage Cloak: ~3 s of thermal-signature disruption. Thermal sentries, thermal scanners and
    /// gold turnstiles ignore the courier. Walls still block. Golden shimmer, sand and afterimages.
    /// </summary>
    public class MirageCloakAbility : PassportAbilityBase
    {
        Material afterimageMat;
        float afterimageTimer;
        ParticleSystem sand;

        public override bool Activate()
        {
            runTimer = def.duration;
            player.IsCloaked = true;
            rig.SetCloaked(true);
            rig.PlayGesture(StickmanRig.Gesture.Cloak, 0.45f);
            afterimageMat ??= ExpoMaterials.Holo(ExpoPalette.Gold, 0.4f, true);
            afterimageTimer = 0f;
            if (sand == null) sand = VfxFactory.SandAura(transform);
            sand.Play();
            VfxFactory.Ring(transform.position + Vector3.up * 0.1f, ExpoPalette.Gold, 3.2f, 0.6f);
            Game.Audio?.PlaySfxAt("cloak_on", transform.position, 1f);
            Game.PostFX?.Pulse(PostFXController.Effect.Cloak, def.duration);
            return true;
        }

        void Update()
        {
            if (runTimer <= 0f) return;
            if (Game.State != null && Game.State.Current == GameState.Paused) return;
            runTimer -= Time.deltaTime;
            afterimageTimer -= Time.deltaTime;
            if (afterimageTimer <= 0f && player.PlanarSpeed > 0.5f)
            {
                afterimageTimer = 0.16f;
                rig.SpawnAfterimage(afterimageMat, 0.5f);
            }
            if (runTimer <= 0f) End(true);
        }

        void End(bool playSound)
        {
            runTimer = 0f;
            player.IsCloaked = false;
            rig.SetCloaked(false);
            if (sand != null) sand.Stop();
            if (playSound)
            {
                Game.Audio?.PlaySfxAt("cloak_off", transform.position, 0.8f);
                VfxFactory.Ring(transform.position + Vector3.up * 0.1f, ExpoPalette.Gold, 1.8f, 0.4f);
            }
        }

        public override void Cancel()
        {
            if (runTimer > 0f) End(false);
            base.Cancel();
        }
    }

    // ===================================================================== FRANCE

    /// <summary>
    /// France · Prism Pulse: a photonic bolt from the wrist module. Aim assist locks the device nearest
    /// the crosshair; the impact freezes it and other cameras/scanners nearby for ~4 s.
    /// </summary>
    public class PrismPulseAbility : PassportAbilityBase
    {
        public const float AssistAngle = 11f;
        public const float SplashRadius = 5f;

        /// <summary>The device the reticle is locked onto this frame (null when none).</summary>
        public IPrismTarget LockTarget { get; private set; }

        void Update()
        {
            LockTarget = null;
            if (manager == null || manager.Active != PassportType.France || !manager.IsUnlocked(PassportType.France)) return;
            if (Game.CameraRig == null) return;
            LockTarget = FindTarget();
        }

        IPrismTarget FindTarget()
        {
            Transform cam = Game.CameraRig.transform;
            IPrismTarget best = null;
            float bestAngle = AssistAngle;
            foreach (var t in SecurityRegistry.PrismTargets)
            {
                if (t == null || !t.IsPrismDisableable) continue;
                Vector3 to = t.TargetPoint - cam.position;
                float dist = Vector3.Distance(t.TargetPoint, transform.position);
                if (dist > def.range) continue;
                float angle = Vector3.Angle(cam.forward, to);
                if (angle >= bestAngle) continue;
                // Must be visible from the courier's wrist.
                Vector3 from = transform.position + Vector3.up * 1.4f;
                Vector3 dir = t.TargetPoint - from;
                if (Physics.Raycast(from, dir.normalized, dir.magnitude - 0.5f, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore)) continue;
                best = t;
                bestAngle = angle;
            }
            return best;
        }

        public override bool Activate()
        {
            Vector3 origin = rig.WristPoint != null ? rig.WristPoint.position : transform.position + Vector3.up * 1.3f;
            var target = LockTarget ?? FindTarget();
            Vector3 aim = target != null ? target.TargetPoint : Game.CameraRig.AimPoint(def.range, out _);

            // Face the shot.
            Vector3 flat = aim - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized);
            rig.PlayGesture(StickmanRig.Gesture.Prism, 0.4f);

            var bolt = new GameObject("PrismBolt").AddComponent<PrismBolt>();
            bolt.Launch(origin, aim, target, def.duration, SplashRadius);
            Game.Audio?.PlaySfxAt("prism_fire", origin, 1f);
            Game.CameraRig?.AddShake(0.15f);
            return true;
        }
    }

    /// <summary>The travelling Prism Pulse projectile.</summary>
    public class PrismBolt : MonoBehaviour
    {
        Vector3 target;
        IPrismTarget lockTarget;
        float freezeSeconds, splash, speed = 42f, life = 2f;

        public void Launch(Vector3 from, Vector3 to, IPrismTarget locked, float freeze, float splashRadius)
        {
            transform.position = from;
            target = to;
            lockTarget = locked;
            freezeSeconds = freeze;
            splash = splashRadius;

            Kit.Mesh(transform, "Core", MeshGen.Sphere(0.12f, 12, 8), ExpoMaterials.Emissive(ExpoPalette.Blue, 8f), Vector3.zero, Vector3.one);
            Kit.Mesh(transform, "Shell", MeshGen.Sphere(0.24f, 12, 8), ExpoMaterials.Holo(ExpoPalette.Blue, 0.5f), Vector3.zero, Vector3.one);
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.widthMultiplier = 0.22f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.sharedMaterial = ExpoMaterials.Beam(ExpoPalette.Blue, 5f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = ExpoPalette.Blue;
            l.range = 5f;
            l.intensity = 4f;
            l.shadows = LightShadows.None;
        }

        void Update()
        {
            if (lockTarget != null) target = lockTarget.TargetPoint;
            Vector3 to = target - transform.position;
            float step = speed * Time.deltaTime;
            life -= Time.deltaTime;
            if (to.magnitude <= step || life <= 0f)
            {
                Impact(target);
                return;
            }
            Vector3 dir = to.normalized;
            if (Physics.Raycast(transform.position, dir, out var hit, step, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore) && lockTarget == null)
            {
                Impact(hit.point);
                return;
            }
            transform.position += dir * step;
        }

        void Impact(Vector3 point)
        {
            int frozen = 0;
            if (lockTarget != null && !lockTarget.IsDisabled) { lockTarget.PrismDisable(freezeSeconds); frozen++; }
            foreach (var t in SecurityRegistry.PrismTargets.ToArray())
            {
                if (t == null || t == lockTarget || !t.IsPrismDisableable) continue;
                if (Vector3.Distance(t.TargetPoint, point) <= splash) { t.PrismDisable(freezeSeconds); frozen++; }
            }
            VfxFactory.Burst(point, ExpoPalette.Blue, 60, 7f);
            VfxFactory.Ring(point, ExpoPalette.Blue, splash, 0.5f, true);
            Game.Audio?.PlaySfxAt("prism_hit", point, 1f);
            if (frozen > 0) GameEvents.RaiseToast(frozen == 1 ? "DEVICE FROZEN  ·  4 SECONDS" : $"{frozen} DEVICES FROZEN  ·  4 SECONDS", ExpoPalette.Blue, 1.6f);
            Destroy(gameObject);
        }
    }

    // ===================================================================== UK

    /// <summary>
    /// UK · Chrono Lock: a Big Ben time-field around the courier. Lasers, tempo gates, scanners and sentries
    /// inside it run at a fraction of their speed for a few seconds.
    /// </summary>
    public class ChronoLockAbility : PassportAbilityBase
    {
        public const float SlowScale = 0.14f;
        Transform field;
        Material shellMat, ringMat;
        Transform ring, handA, handB;
        float visualWeight;

        public override bool Activate()
        {
            runTimer = def.duration;
            rig.PlayGesture(StickmanRig.Gesture.Chrono, 0.55f);
            if (field == null) BuildField();
            field.gameObject.SetActive(true);
            visualWeight = 0f;
            VfxFactory.Ring(transform.position + Vector3.up * 0.1f, ExpoPalette.Emerald, def.range, 0.7f, true);
            Game.Audio?.PlaySfxAt("chrono_on", transform.position, 1f);
            Game.PostFX?.Pulse(PostFXController.Effect.Chrono, def.duration);
            return true;
        }

        void BuildField()
        {
            field = new GameObject("ChronoField").transform;
            shellMat = ExpoMaterials.Holo(ExpoPalette.Emerald, 0.35f, true);
            ringMat = ExpoMaterials.Holo(ExpoPalette.Emerald, 0.8f, true);
            Kit.Mesh(field, "Shell", MeshGen.Hemisphere(1f, 32, 12), shellMat, Vector3.zero, Vector3.one);
            ring = new GameObject("ClockFace").transform;
            ring.SetParent(field, false);
            ring.localPosition = Vector3.up * 0.04f;
            Kit.Mesh(ring, "Rim", MeshGen.Annulus(0.93f, 1f, 64), ringMat, Vector3.zero, Vector3.one);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                Kit.Mesh(ring, "Tick", MeshGen.Box(new Vector3(0.02f, 0.01f, i % 3 == 0 ? 0.14f : 0.07f)), ringMat,
                    Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 0.85f), Vector3.one, Quaternion.Euler(0, a, 0));
            }
            handA = new GameObject("HourHand").transform; handA.SetParent(ring, false);
            handB = new GameObject("MinuteHand").transform; handB.SetParent(ring, false);
            Kit.Mesh(handA, "Bar", MeshGen.Box(new Vector3(0.03f, 0.01f, 0.45f)), ringMat, new Vector3(0, 0, 0.22f), Vector3.one);
            Kit.Mesh(handB, "Bar", MeshGen.Box(new Vector3(0.02f, 0.01f, 0.7f)), ringMat, new Vector3(0, 0, 0.35f), Vector3.one);
            foreach (var r in field.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = field.gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = ExpoPalette.Emerald;
            l.range = def.range * 1.2f;
            l.intensity = 2.5f;
            l.shadows = LightShadows.None;
            field.gameObject.SetActive(false);
        }

        void Update()
        {
            bool paused = Game.State != null && Game.State.Current == GameState.Paused;
            if (runTimer > 0f && !paused)
            {
                runTimer -= Time.deltaTime;
                if (runTimer <= 0f) { Release(); Game.Audio?.PlaySfxAt("chrono_off", transform.position, 0.8f); }
            }
            if (field == null) return;

            visualWeight = Mathf.MoveTowards(visualWeight, runTimer > 0f ? 1f : 0f, Time.deltaTime * (runTimer > 0f ? 4f : 3f));
            if (visualWeight <= 0f && runTimer <= 0f) { field.gameObject.SetActive(false); return; }

            float radius = def.range * Mathf.SmoothStep(0f, 1f, visualWeight);
            field.position = transform.position;
            field.localScale = new Vector3(radius, radius * 0.55f, radius);
            handA.localRotation = Quaternion.Euler(0, Time.time * 30f, 0);
            handB.localRotation = Quaternion.Euler(0, Time.time * 360f * 0.1f * 4f, 0);
            shellMat.SetFloat("_Alpha", 0.3f * visualWeight);
            ringMat.SetFloat("_Alpha", 0.85f * visualWeight);

            if (runTimer > 0f)
            {
                foreach (var c in SecurityRegistry.ChronoTargets)
                {
                    if (c == null) continue;
                    Vector3 d = c.ChronoPosition - transform.position; d.y *= 0.4f;
                    c.SetChronoScale(d.magnitude <= def.range + 1.5f ? SlowScale : 1f);
                }
            }
        }

        void Release()
        {
            runTimer = 0f;
            foreach (var c in SecurityRegistry.ChronoTargets) c?.SetChronoScale(1f);
        }

        public override void Cancel()
        {
            Release();
            visualWeight = 0f;
            if (field != null) field.gameObject.SetActive(false);
            base.Cancel();
        }
    }
}
