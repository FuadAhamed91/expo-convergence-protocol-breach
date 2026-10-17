using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Over-the-shoulder orbit camera: mouse yaw/pitch with limits, wheel zoom, smoothed follow,
    /// sphere-cast collision so it never sits inside walls, and a sprint FOV kick.
    /// The <see cref="CinematicDirector"/> takes over this transform during cutscenes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCameraController : MonoBehaviour
    {
        public Transform target;
        public float distance = 4.4f;
        public float minDistance = 2.2f;
        public float maxDistance = 7.0f;
        public Vector2 shoulderOffset = new Vector2(0.6f, 0.12f);
        public float minPitch = -28f;
        public float maxPitch = 62f;
        public float collisionRadius = 0.24f;
        public float baseFov = 60f;
        public float sprintFov = 67f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 14f;
        public Camera Cam { get; private set; }

        float targetDistance;
        float currentDistance;
        Vector3 smoothedPivot;
        Vector3 pivotVelocity;
        float shake;

        void Awake()
        {
            Game.CameraRig = this;
            Cam = GetComponent<Camera>();
            targetDistance = distance;
            currentDistance = distance;
        }

        public void SnapBehindPlayer()
        {
            if (target == null) return;
            Yaw = target.root.eulerAngles.y;
            Pitch = 14f;
            smoothedPivot = target.position;
            pivotVelocity = Vector3.zero;
            currentDistance = targetDistance;
            ComputePose(out var pos, out var rot, true);
            transform.SetPositionAndRotation(pos, rot);
        }

        public void AddShake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            if (target == null) return;
            if (Game.Cinematics != null && Game.Cinematics.IsPlaying) return;

            float dt = Time.deltaTime;
            if (Game.InputEnabled)
            {
                Vector2 look = PlayerInput.Look;
                Yaw += look.x;
                Pitch = Mathf.Clamp(Pitch - look.y, minPitch, maxPitch);
                float zoom = PlayerInput.Zoom;
                if (Mathf.Abs(zoom) > 0.01f) targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(zoom) * 0.45f, minDistance, maxDistance);
            }

            smoothedPivot = Vector3.SmoothDamp(smoothedPivot, target.position, ref pivotVelocity, 0.05f, Mathf.Infinity, dt);
            ComputePose(out var position, out var rotation, false);

            if (shake > 0f)
            {
                position += Random.insideUnitSphere * shake * 0.12f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            }
            transform.SetPositionAndRotation(position, rotation);

            var player = Game.Player;
            float fov = player != null && player.IsSprinting ? sprintFov : baseFov;
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fov, 1f - Mathf.Exp(-6f * dt));
        }

        /// <summary>Computes the collision-resolved gameplay pose (also used to blend out of cinematics).</summary>
        public void ComputePose(out Vector3 position, out Quaternion rotation, bool instant)
        {
            rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 pivot = (instant || smoothedPivot == Vector3.zero) && target != null ? target.position : smoothedPivot;
            Vector3 shoulder = Quaternion.Euler(0f, Yaw, 0f) * new Vector3(shoulderOffset.x, shoulderOffset.y, 0f);

            // Keep the shoulder offset itself out of walls.
            Vector3 shoulderPoint = pivot + shoulder;
            if (Physics.SphereCast(pivot, collisionRadius, shoulder.normalized, out var sh, shoulder.magnitude, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                shoulderPoint = pivot + shoulder.normalized * Mathf.Max(0f, sh.distance - 0.02f);

            Vector3 back = rotation * Vector3.back;
            float allowed = targetDistance;
            if (Physics.SphereCast(shoulderPoint, collisionRadius, back, out var hit, targetDistance, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.35f, hit.distance - 0.05f);

            if (instant) currentDistance = allowed;
            else if (allowed < currentDistance) currentDistance = allowed;                 // pull in immediately
            else currentDistance = Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-3.5f * Time.deltaTime)); // ease back out

            position = shoulderPoint + back * currentDistance;
        }

        /// <summary>Point under the screen centre, for aiming the Prism Pulse.</summary>
        public Vector3 AimPoint(float maxRange, out Collider hitCollider)
        {
            var ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out var hit, maxRange + currentDistance, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                hitCollider = hit.collider;
                return hit.point;
            }
            hitCollider = null;
            return ray.GetPoint(maxRange + currentDistance);
        }
    }
}
