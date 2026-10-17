using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Third-person courier movement: camera-relative jog, stamina-limited sprint, a dive-slide that fits
    /// under low security shutters, gravity and grounded checks. Sprinting makes noise sentries can hear.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCourierController : MonoBehaviour
    {
        [Header("Movement")]
        public float jogSpeed = 4.7f;
        public float sprintSpeed = 7.8f;
        public float acceleration = 22f;
        public float turnSpeed = 760f;
        public float gravity = -24f;

        [Header("Slide")]
        public float slideSpeed = 10f;
        public float slideDuration = 0.75f;
        public float slideCooldown = 0.45f;
        public float slideStaminaCost = 12f;

        [Header("Stamina")]
        public float staminaMax = 100f;
        public float sprintDrain = 20f;
        public float staminaRegen = 24f;
        public float regenDelay = 0.7f;

        public float Stamina { get; private set; }
        public float StaminaFraction => Stamina / staminaMax;
        public bool IsSprinting { get; private set; }
        public bool IsSliding { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsCloaked { get; set; }
        public bool IsInteracting { get; set; }
        public bool Exhausted { get; private set; }
        public float PlanarSpeed => planarVelocity.magnitude;
        public Vector3 PlanarVelocity => planarVelocity;
        /// <summary>0 = silent, 1 = sprinting footfalls.</summary>
        public float NoiseLevel { get; private set; }

        public Transform CameraTarget { get; private set; }
        public Vector3 SensorTarget => transform.position + Vector3.up * (IsSliding ? 0.45f : 1.2f);
        public StickmanRig Rig { get; private set; }
        public CharacterController Controller => cc;
        public float CapsuleRadius => cc.radius;
        public float CapsuleHeight => cc.height;

        CharacterController cc;
        Vector3 planarVelocity;
        float verticalVelocity;
        float slideTimer, slideCooldownTimer, regenTimer;
        Vector3 slideDirection;
        const float StandHeight = 1.8f;
        const float SlideHeight = 0.85f;

        void Awake()
        {
            Game.Player = this;
            gameObject.layer = ExpoLayers.Player;
            cc = GetComponent<CharacterController>();
            cc.height = StandHeight;
            cc.center = new Vector3(0, StandHeight * 0.5f, 0);
            cc.radius = 0.3f;
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;
            Stamina = staminaMax;

            CameraTarget = new GameObject("CameraTarget").transform;
            CameraTarget.SetParent(transform, false);
            CameraTarget.localPosition = new Vector3(0, 1.55f, 0);

            Rig = gameObject.AddComponent<StickmanRig>();
            Rig.Build();
            Rig.Footstep += OnFootstep;
        }

        void OnFootstep()
        {
            Game.Audio?.PlayFootstep(transform.position, IsSprinting);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool playing = Game.State != null && (Game.State.Current == GameState.Playing || Game.State.Current == GameState.Cinematic || Game.State.Current == GameState.Caught);
            if (!playing && Game.State != null && Game.State.Current != GameState.Title && Game.State.Current != GameState.Briefing)
            {
                Rig.Animate(0f, sprintSpeed, true, false, false, dt);
                return;
            }

            bool canAct = Game.InputEnabled && !IsInteracting;
            Vector2 input = canAct ? PlayerInput.Move : Vector2.zero;

            Transform cam = Game.CameraRig != null ? Game.CameraRig.transform : (Camera.main != null ? Camera.main.transform : transform);
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.ProjectOnPlane(cam.up, Vector3.up);
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 wish = fwd * input.y + right * input.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // Sprint & stamina.
            bool wantsSprint = canAct && PlayerInput.Sprint && wish.sqrMagnitude > 0.1f && !IsSliding;
            if (Exhausted && Stamina > staminaMax * 0.3f) Exhausted = false;
            IsSprinting = wantsSprint && !Exhausted && Stamina > 0.5f;
            if (IsSprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - sprintDrain * dt);
                regenTimer = regenDelay;
                if (Stamina <= 0.5f) { Exhausted = true; GameEvents.RaiseHint("Stamina depleted · release SHIFT to recover", 2.5f); }
            }
            else if (!IsSliding)
            {
                regenTimer -= dt;
                if (regenTimer <= 0f) Stamina = Mathf.Min(staminaMax, Stamina + staminaRegen * dt);
            }

            // Slide.
            slideCooldownTimer -= dt;
            if (canAct && PlayerInput.SlidePressed && !IsSliding && slideCooldownTimer <= 0f && IsGrounded
                && planarVelocity.magnitude > 2.2f && Stamina >= slideStaminaCost)
            {
                StartSlide();
            }

            if (IsSliding)
            {
                slideTimer -= dt;
                float t = Mathf.Clamp01(1f - slideTimer / slideDuration);
                float speed = Mathf.Lerp(slideSpeed, jogSpeed * 0.55f, t * t);
                if (slideTimer <= 0f)
                {
                    if (HasHeadroom()) EndSlide();
                    else speed = 2.2f; // still under an obstacle: keep crawling through
                }
                planarVelocity = slideDirection * speed;
            }
            else
            {
                float targetSpeed = IsSprinting ? sprintSpeed : jogSpeed;
                Vector3 target = wish * targetSpeed;
                float accel = wish.sqrMagnitude > 0.01f ? acceleration : acceleration * 1.4f;
                planarVelocity = Vector3.MoveTowards(planarVelocity, target, accel * dt);
            }

            // Face the travel direction.
            Vector3 face = IsSliding ? slideDirection : planarVelocity;
            if (face.sqrMagnitude > 0.04f)
            {
                Quaternion targetRot = Quaternion.LookRotation(face.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * dt);
            }

            // Gravity.
            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -3f;
            verticalVelocity += gravity * dt;
            if (cc.enabled) cc.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            IsGrounded = cc.isGrounded || GroundProbe();

            // Safety net: never fall out of the world.
            if (transform.position.y < -10f) Game.Checkpoints?.RespawnPlayer(false);

            NoiseLevel = IsSprinting ? 1f : (IsSliding ? 0.55f : 0f);
            Rig.Animate(IsSliding ? slideSpeed * 0.5f : planarVelocity.magnitude, sprintSpeed, IsGrounded, IsSliding, IsInteracting, dt);
        }

        bool GroundProbe()
        {
            return Physics.SphereCast(transform.position + Vector3.up * 0.4f, 0.25f, Vector3.down, out _, 0.25f, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        void StartSlide()
        {
            IsSliding = true;
            slideTimer = slideDuration;
            slideDirection = planarVelocity.sqrMagnitude > 0.01f ? planarVelocity.normalized : transform.forward;
            Stamina -= slideStaminaCost;
            regenTimer = regenDelay;
            cc.height = SlideHeight;
            cc.center = new Vector3(0, SlideHeight * 0.5f, 0);
            CameraTarget.localPosition = new Vector3(0, 1.0f, 0);
            Game.Audio?.PlaySfxAt("slide", transform.position, 0.9f);
        }

        void EndSlide()
        {
            IsSliding = false;
            slideCooldownTimer = slideCooldown;
            cc.height = StandHeight;
            cc.center = new Vector3(0, StandHeight * 0.5f, 0);
            CameraTarget.localPosition = new Vector3(0, 1.55f, 0);
        }

        bool HasHeadroom()
        {
            Vector3 bottom = transform.position + Vector3.up * (cc.radius + 0.05f);
            return !Physics.SphereCast(bottom, cc.radius * 0.95f, Vector3.up, out _, StandHeight - cc.radius * 2f - 0.05f, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        public void Teleport(Vector3 position, float yaw)
        {
            if (IsSliding) EndSlide();
            cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            cc.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            Stamina = staminaMax;
            Exhausted = false;
            IsInteracting = false;
            IsCloaked = false;
        }

        /// <summary>Pushes the courier back (used by turnstiles when entered without a cloak).</summary>
        public void Knockback(Vector3 direction, float speed)
        {
            direction.y = 0f;
            planarVelocity = direction.normalized * speed;
        }
    }
}
