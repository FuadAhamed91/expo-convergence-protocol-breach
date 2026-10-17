using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Prism scanner pylon: a floating crystal that sweeps a tall, narrow scan blade around itself.
    /// Can be thermal (UAE district) or optical (France district).
    /// </summary>
    public class ScannerTurret : SecuritySensor
    {
        public float rotateSpeed = 48f;
        public float startAngle;
        public bool oscillate;
        public float oscillateArc = 150f;

        Transform rotor, crystal;
        float angle, phase;

        public void Configure(SensorProfile p, float speed, float start, bool swing = false, float arc = 150f)
        {
            profile = p;
            rotateSpeed = speed;
            startAngle = start;
            angle = start;
            oscillate = swing;
            oscillateArc = arc;
            displayName = p.kind == SensorKind.Thermal ? "THERMAL SCANNER" : "PRISM SCANNER";
            BuildVisual();
        }

        void BuildVisual()
        {
            var graphite = ExpoMaterials.Lit(new Color(0.09f, 0.1f, 0.14f), 0.7f, 0.75f);
            Color accent = profile.kind == SensorKind.Thermal ? new Color(1f, 0.55f, 0.2f) : ExpoPalette.Cyan;
            var trim = ExpoMaterials.Emissive(accent, 2.5f);

            Kit.Mesh(transform, "Base", MeshGen.Cylinder(0.55f, 0.42f, 0.25f, 6), graphite, Vector3.zero, Vector3.one);
            Kit.Mesh(transform, "BaseRing", MeshGen.Torus(0.47f, 0.025f, 24, 5), trim, new Vector3(0, 0.26f, 0), Vector3.one);
            Kit.Mesh(transform, "Mast", MeshGen.Cylinder(0.12f, 0.08f, 1.9f, 6), graphite, new Vector3(0, 0.25f, 0), Vector3.one);
            Kit.Mesh(transform, "MastStripe", MeshGen.Box(new Vector3(0.03f, 1.6f, 0.03f)), trim, new Vector3(0, 1.15f, 0.1f), Vector3.one);

            rotor = new GameObject("Rotor").transform;
            rotor.SetParent(transform, false);
            rotor.localPosition = new Vector3(0, 2.45f, 0);

            crystal = new GameObject("Crystal").transform;
            crystal.SetParent(rotor, false);
            var gem = Kit.Mesh(crystal, "Gem", MeshGen.Lathe("prism_gem", new[] { new Vector2(0, -0.32f), new Vector2(0.2f, 0f), new Vector2(0, 0.32f) }, 6), trim, Vector3.zero, Vector3.one);
            Kit.Mesh(rotor, "Halo", MeshGen.Torus(0.3f, 0.015f, 24, 4), graphite, Vector3.zero, Vector3.one, Quaternion.Euler(90, 0, 0));

            eye = new GameObject("Eye").transform;
            eye.SetParent(rotor, false);
            eye.localRotation = Quaternion.Euler(12f, 0, 0);
            var cone = Kit.Mesh(eye, "ScanBlade", MeshGen.Cone(profile.range, 22f, 4, 0.32f), trim, new Vector3(0, 0, 0.2f), Vector3.one);
            fovOverride = new Vector2(16f, 44f);

            var lightGo = new GameObject("GemLight");
            lightGo.transform.SetParent(rotor, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4f;
            l.intensity = 2f;
            l.shadows = LightShadows.None;

            SetupVisuals(cone, new[] { gem }, l, 0.2f);
            SetupGroundFan(0.8f, profile.range, 16f);
        }

        protected override void Tick(float scaledDt, float dt)
        {
            if (!SeesPlayer)
            {
                if (oscillate)
                {
                    phase += scaledDt * rotateSpeed * Mathf.Deg2Rad * 1.2f;
                    angle = startAngle + Mathf.Sin(phase) * oscillateArc * 0.5f;
                }
                else angle += rotateSpeed * scaledDt;
            }
            if (rotor != null) rotor.localRotation = Quaternion.Euler(0, angle, 0);
            if (crystal != null) crystal.localRotation = Quaternion.Euler(0, Time.time * 90f * Mathf.Max(0.1f, chronoScale), 0);
        }

        public override void ResetState()
        {
            base.ResetState();
            angle = startAngle;
            phase = 0f;
        }
    }
}
