using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Wall- or pole-mounted optical camera that sweeps between two angles, pausing at each end.
    /// Optical: the Mirage Cloak does not fool it, but the Prism Pulse blinds it.
    /// </summary>
    public class SecurityCamera : SecuritySensor
    {
        public float sweepAngle = 70f;
        public float sweepSpeed = 22f;
        public float pauseTime = 1.1f;
        public float pitch = 24f;

        Transform head;
        float baseYaw, yaw, pauseTimer;
        int direction = 1;

        public void Configure(SensorProfile p, float sweep, float speed, float tilt)
        {
            profile = p;
            sweepAngle = sweep;
            sweepSpeed = speed;
            pitch = tilt;
            displayName = "OPTICAL CAMERA";
            BuildVisual();
        }

        void BuildVisual()
        {
            var graphite = ExpoMaterials.Lit(new Color(0.12f, 0.13f, 0.17f), 0.65f, 0.7f);
            var white = ExpoMaterials.Lit(new Color(0.8f, 0.84f, 0.9f), 0.7f, 0.3f);

            Kit.Mesh(transform, "Mount", MeshGen.Box(new Vector3(0.18f, 0.18f, 0.12f)), graphite, new Vector3(0, 0, -0.06f), Vector3.one);
            Kit.Mesh(transform, "Arm", MeshGen.Cylinder(0.035f, 0.035f, 0.28f, 8), graphite, new Vector3(0, 0, 0), Vector3.one, Quaternion.Euler(90, 0, 0));

            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0, -0.05f, 0.28f);
            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(head, false);
            tilt.localRotation = Quaternion.Euler(pitch, 0, 0);
            eye = tilt;

            Kit.Mesh(tilt, "Housing", MeshGen.Box(new Vector3(0.2f, 0.18f, 0.42f)), white, new Vector3(0, 0, 0.05f), Vector3.one);
            Kit.Mesh(tilt, "Hood", MeshGen.Box(new Vector3(0.24f, 0.03f, 0.46f)), graphite, new Vector3(0, 0.105f, 0.07f), Vector3.one);
            var lens = Kit.Mesh(tilt, "Lens", MeshGen.Cylinder(0.06f, 0.06f, 0.03f, 16), white, new Vector3(0, 0, 0.27f), Vector3.one, Quaternion.Euler(90, 0, 0));
            var led = Kit.Mesh(tilt, "Led", MeshGen.Sphere(0.02f, 8, 6), white, new Vector3(0.07f, 0.06f, 0.27f), Vector3.one);
            var cone = Kit.Mesh(tilt, "VisionCone", MeshGen.Cone(profile.range, profile.fieldOfView * 0.5f, 4, 1.35f), white, new Vector3(0, 0, 0.29f), Vector3.one);

            var lightGo = new GameObject("CamLight");
            lightGo.transform.SetParent(tilt, false);
            lightGo.transform.localPosition = new Vector3(0, 0, 0.3f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = profile.fieldOfView;
            l.range = profile.range;
            l.intensity = 4f;
            l.shadows = LightShadows.None;

            fovOverride = new Vector2(profile.fieldOfView * 1.35f, profile.fieldOfView);
            SetupVisuals(cone, new[] { lens, led }, l, 0.14f);
            baseYaw = 0f;
        }

        protected override void Tick(float scaledDt, float dt)
        {
            var level = Game.Detection != null ? Game.Detection.Level : AlertLevel.Hidden;
            if (SeesPlayer && level == AlertLevel.Alert && Game.Player != null)
            {
                // Track the courier while the grid is on alert.
                Vector3 local = transform.InverseTransformPoint(Game.Player.transform.position);
                float targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -sweepAngle * 0.5f - 15f, sweepAngle * 0.5f + 15f);
                yaw = Mathf.MoveTowards(yaw, targetYaw, sweepSpeed * 3f * scaledDt);
            }
            else if (SeesPlayer || HearsPlayer)
            {
                // Hold on whatever caught its attention.
            }
            else if (pauseTimer > 0f) pauseTimer -= scaledDt;
            else
            {
                yaw += direction * sweepSpeed * scaledDt;
                if (Mathf.Abs(yaw) >= sweepAngle * 0.5f)
                {
                    yaw = Mathf.Sign(yaw) * sweepAngle * 0.5f;
                    direction = -direction;
                    pauseTimer = pauseTime;
                }
            }
            if (head != null) head.localRotation = Quaternion.Euler(0, baseYaw + yaw, 0);
        }

        public override void ResetState()
        {
            base.ResetState();
            yaw = 0f;
            direction = 1;
            pauseTimer = 0f;
        }
    }
}
