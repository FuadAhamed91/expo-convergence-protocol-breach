using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Falcon-inspired flying sentry. Patrols waypoints with a sweeping searchlight, turns towards
    /// anything suspicious, chases during an alert, searches the last known position, then returns.
    /// Thermal sentries cannot see a Mirage-cloaked courier; Prism Pulse freezes them; Chrono slows them.
    /// </summary>
    public class SentryAI : SecuritySensor
    {
        public enum Mode { Patrol, Suspicious, Chase, Search, Return }

        public Vector3[] waypoints;
        public float waitTime = 1.4f;
        public float hoverHeight = 3.3f;
        public float scanAngle = 38f;
        public float eyePitch = 30f;

        public Mode CurrentMode { get; private set; }
        Transform visual, eyeYaw, wingL, wingR, thrusterRing;
        int waypointIndex;
        float waitTimer, searchTimer, scanPhase, bobPhase;
        Vector3 velocity;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        Light searchlight;

        public void Configure(SensorProfile p, Vector3[] path, float height)
        {
            profile = p;
            waypoints = path;
            hoverHeight = height;
            displayName = p.kind == SensorKind.Thermal ? "THERMAL FALCON" : "OPTICAL FALCON";
            spawnPosition = path != null && path.Length > 0 ? new Vector3(path[0].x, height, path[0].z) : transform.position;
            transform.position = spawnPosition;
            spawnRotation = transform.rotation;
            BuildVisual();
            scanPhase = Random.value * 10f;
            bobPhase = Random.value * 10f;
        }

        void BuildVisual()
        {
            visual = new GameObject("FalconVisual").transform;
            visual.SetParent(transform, false);

            var pearl = ExpoMaterials.Lit(new Color(0.78f, 0.82f, 0.88f), 0.78f, 0.35f);
            var graphite = ExpoMaterials.Lit(new Color(0.08f, 0.09f, 0.12f), 0.6f, 0.6f);
            Color accent = profile.kind == SensorKind.Thermal ? new Color(1f, 0.55f, 0.2f) : ExpoPalette.Cyan;
            var accentMat = ExpoMaterials.Emissive(accent, 2.2f);

            var model = Resources.Load<GameObject>("Models/FalconDrone");
            Renderer lens;
            if (model != null)
            {
                var inst = Instantiate(model, visual);
                inst.name = "FalconModel";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                ExpoModelUtil.ApplyMaterials(inst, pearl, graphite, accentMat);
                lens = ExpoModelUtil.FindRenderer(inst, "Lens");
            }
            else lens = null;

            if (model == null)
            {
                Kit.Mesh(visual, "Fuselage", MeshGen.Sphere(0.32f, 18, 12), pearl, new Vector3(0, 0, 0), new Vector3(0.9f, 0.62f, 1.55f));
                Kit.Mesh(visual, "Spine", MeshGen.Box(new Vector3(0.08f, 0.05f, 0.8f)), accentMat, new Vector3(0, 0.19f, -0.05f), Vector3.one);
                Kit.Mesh(visual, "Belly", MeshGen.Sphere(0.2f, 14, 8), graphite, new Vector3(0, -0.12f, 0.05f), new Vector3(1f, 0.6f, 1.6f));
                wingL = Wing(visual, -1f, pearl, accentMat);
                wingR = Wing(visual, 1f, pearl, accentMat);
                Kit.Mesh(visual, "TailFin", MeshGen.TaperedBox(new Vector2(0.03f, 0.32f), new Vector2(0.02f, 0.12f), 0.24f), pearl, new Vector3(0, 0.1f, -0.42f), Vector3.one, Quaternion.Euler(-25f, 0, 0));
                Kit.Mesh(visual, "TailL", MeshGen.Box(new Vector3(0.36f, 0.02f, 0.14f)), pearl, new Vector3(-0.16f, 0, -0.48f), Vector3.one, Quaternion.Euler(0, -25f, -8f));
                Kit.Mesh(visual, "TailR", MeshGen.Box(new Vector3(0.36f, 0.02f, 0.14f)), pearl, new Vector3(0.16f, 0, -0.48f), Vector3.one, Quaternion.Euler(0, 25f, 8f));
                thrusterRing = Kit.Mesh(visual, "Thruster", MeshGen.Torus(0.28f, 0.025f, 28, 6), accentMat, new Vector3(0, -0.25f, 0), Vector3.one).transform;
            }

            // Sensor head: yaws to scan, pitches down at the ground.
            eyeYaw = new GameObject("EyeYaw").transform;
            eyeYaw.SetParent(transform, false);
            eyeYaw.localPosition = new Vector3(0, -0.05f, 0.42f);
            eye = new GameObject("Eye").transform;
            eye.SetParent(eyeYaw, false);
            eye.localRotation = Quaternion.Euler(eyePitch, 0, 0);
            if (lens == null)
                lens = Kit.Mesh(eye, "Lens", MeshGen.Sphere(0.11f, 14, 10), accentMat, new Vector3(0, 0, 0.02f), new Vector3(1f, 1f, 0.6f));
            Kit.Mesh(eye, "LensRing", MeshGen.Torus(0.11f, 0.018f, 20, 6), graphite, new Vector3(0, 0, 0.0f), Vector3.one, Quaternion.Euler(90, 0, 0));

            var coneGo = Kit.Mesh(eye, "VisionCone", MeshGen.Cone(profile.range, profile.fieldOfView * 0.5f, 18), accentMat, new Vector3(0, 0, 0.05f), Vector3.one);
            var lightGo = new GameObject("Searchlight");
            lightGo.transform.SetParent(eye, false);
            searchlight = lightGo.AddComponent<Light>();
            searchlight.type = LightType.Spot;
            searchlight.spotAngle = profile.fieldOfView * 0.9f;
            searchlight.innerSpotAngle = profile.fieldOfView * 0.45f;
            searchlight.range = profile.range * 1.15f;
            searchlight.intensity = 9f;
            searchlight.shadows = LightShadows.None;

            SetupVisuals(coneGo, new[] { lens }, searchlight, 0.16f);
            foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        static Transform Wing(Transform parent, float side, Material mat, Material edge)
        {
            var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0.18f * side, 0.06f, 0.0f);
            // Swept, tapered falcon wing with an emissive trailing edge.
            Kit.Mesh(pivot, "Panel", MeshGen.TaperedBox(new Vector2(0.75f, 0.34f), new Vector2(0.75f, 0.14f), 0.025f), mat,
                new Vector3(0.36f * side, 0, -0.08f), Vector3.one, Quaternion.Euler(0, -24f * side, 0));
            Kit.Mesh(pivot, "Edge", MeshGen.Box(new Vector3(0.72f, 0.02f, 0.025f)), edge,
                new Vector3(0.36f * side, 0.01f, -0.24f), Vector3.one, Quaternion.Euler(0, -24f * side, 0));
            Kit.Mesh(pivot, "Tip", MeshGen.Sphere(0.03f, 8, 6), edge, new Vector3(0.7f * side, 0.01f, -0.36f), Vector3.one);
            return pivot;
        }

        protected override void Tick(float scaledDt, float dt)
        {
            var detection = Game.Detection;
            var level = detection != null ? detection.Level : AlertLevel.Hidden;
            bool gameplay = Game.State != null && Game.State.Current == GameState.Playing;

            // Mode transitions.
            if (gameplay)
            {
                bool nearby = Game.Player != null && Vector3.Distance(Game.Player.transform.position, transform.position) < 38f;
                if (level == AlertLevel.Alert && nearby) CurrentMode = Mode.Chase;
                else if (CurrentMode == Mode.Chase) { CurrentMode = Mode.Search; searchTimer = 4f; }
                else if ((SeesPlayer || HearsPlayer) && CurrentMode == Mode.Patrol) CurrentMode = Mode.Suspicious;
                else if (CurrentMode == Mode.Suspicious && !SeesPlayer && !HearsPlayer) { CurrentMode = Mode.Search; searchTimer = 2.2f; }
            }
            else if (CurrentMode != Mode.Patrol) CurrentMode = Mode.Return;

            Vector3 goal = transform.position;
            float speed = profile.moveSpeed;
            Vector3? lookAt = null;

            switch (CurrentMode)
            {
                case Mode.Patrol:
                    if (waypoints == null || waypoints.Length == 0) break;
                    goal = Hover(waypoints[waypointIndex]);
                    if (Flat(goal - transform.position).magnitude < 0.4f)
                    {
                        waitTimer += scaledDt;
                        if (waitTimer >= waitTime) { waitTimer = 0f; waypointIndex = (waypointIndex + 1) % waypoints.Length; }
                    }
                    break;

                case Mode.Suspicious:
                    goal = transform.position;
                    lookAt = LastSeenPosition;
                    speed = 0f;
                    break;

                case Mode.Chase:
                    goal = Hover(detection.LastKnownPosition);
                    lookAt = detection.LastKnownPosition;
                    speed = profile.chaseSpeed;
                    break;

                case Mode.Search:
                    goal = Hover(LastSeenPosition == Vector3.zero ? transform.position : LastSeenPosition);
                    searchTimer -= scaledDt;
                    if (searchTimer <= 0f) CurrentMode = Mode.Return;
                    break;

                case Mode.Return:
                    if (waypoints == null || waypoints.Length == 0) { CurrentMode = Mode.Patrol; break; }
                    goal = Hover(waypoints[waypointIndex]);
                    speed = profile.moveSpeed * 1.2f;
                    if (Flat(goal - transform.position).magnitude < 0.6f) CurrentMode = Mode.Patrol;
                    break;
            }

            // Steering with simple wall avoidance.
            Vector3 toGoal = goal - transform.position;
            Vector3 desired = toGoal.magnitude > 0.05f ? toGoal.normalized * Mathf.Min(speed, toGoal.magnitude * 2.5f) : Vector3.zero;
            if (desired.sqrMagnitude > 0.01f && Physics.SphereCast(transform.position, 0.45f, desired.normalized, out var hit, 2.2f, ExpoLayers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                desired = Vector3.ProjectOnPlane(desired, hit.normal) + hit.normal * 1.2f;
            velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-3.2f * scaledDt));
            transform.position += velocity * scaledDt;

            // Hover bob and banking.
            bobPhase += scaledDt * 2.1f;
            if (visual != null)
            {
                visual.localPosition = new Vector3(0, Mathf.Sin(bobPhase) * 0.09f, 0);
                Vector3 localVel = transform.InverseTransformDirection(velocity);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(localVel.z * 4f, 0, -localVel.x * 9f), 1f - Mathf.Exp(-5f * dt));
            }
            if (wingL != null)
            {
                float flap = Mathf.Sin(bobPhase * 1.6f) * 6f;
                wingL.localRotation = Quaternion.Euler(0, 0, flap);
                wingR.localRotation = Quaternion.Euler(0, 0, -flap);
            }
            if (thrusterRing != null) thrusterRing.Rotate(0, 220f * scaledDt, 0, Space.Self);

            // Body yaw follows travel or the target.
            Vector3 faceDir = lookAt.HasValue ? Flat(lookAt.Value - transform.position) : Flat(velocity);
            if (faceDir.sqrMagnitude > 0.02f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(faceDir.normalized), 160f * scaledDt);

            // Sensor head scan.
            scanPhase += scaledDt * (CurrentMode == Mode.Search ? 1.8f : 0.9f);
            float scanYaw = (CurrentMode == Mode.Chase || CurrentMode == Mode.Suspicious) ? 0f : Mathf.Sin(scanPhase) * scanAngle;
            if (eyeYaw != null) eyeYaw.localRotation = Quaternion.Slerp(eyeYaw.localRotation, Quaternion.Euler(0, scanYaw, 0), 1f - Mathf.Exp(-4f * scaledDt));
            if (lookAt.HasValue && eye != null)
            {
                // Tilt the lens towards the target point.
                Vector3 l = lookAt.Value + Vector3.up * 1f - eye.position;
                float pitch = Mathf.Clamp(Mathf.Atan2(-l.y, Flat(l).magnitude) * Mathf.Rad2Deg, 5f, 70f);
                eye.localRotation = Quaternion.Slerp(eye.localRotation, Quaternion.Euler(pitch, 0, 0), 1f - Mathf.Exp(-6f * dt));
            }
            else if (eye != null) eye.localRotation = Quaternion.Slerp(eye.localRotation, Quaternion.Euler(eyePitch, 0, 0), 1f - Mathf.Exp(-3f * dt));
        }

        Vector3 Hover(Vector3 p) => new Vector3(p.x, hoverHeight, p.z);
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        protected override void OnDisabledByPrism()
        {
            base.OnDisabledByPrism();
            velocity = Vector3.zero;
        }

        public override void ResetState()
        {
            base.ResetState();
            CurrentMode = Mode.Patrol;
            waypointIndex = 0;
            waitTimer = 0f;
            velocity = Vector3.zero;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
    }
}
