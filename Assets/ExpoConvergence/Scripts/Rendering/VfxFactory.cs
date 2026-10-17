using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>Runtime-built particle and shockwave effects (all additive, bloom-friendly).</summary>
    public static class VfxFactory
    {
        static ParticleSystem MakeSystem(string name, Vector3 pos, Color color, float lifetime, float speed, float size, int burst, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(burst * 2, 64);
            main.stopAction = loop ? ParticleSystemStopAction.None : ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.1f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ExpoMaterials.ParticleAdditive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        public static void Sparks(Vector3 pos, Color color, int count)
        {
            var ps = MakeSystem("Sparks", pos, color, 0.6f, 7f, 0.07f, count);
            var main = ps.main;
            main.gravityModifier = 1.6f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1.2f;
            ps.Play();
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed)
        {
            var ps = MakeSystem("Burst", pos, color, 0.8f, speed, 0.18f, count);
            var main = ps.main;
            main.gravityModifier = 0.15f;
            ps.Play();
        }

        /// <summary>A coloured fireworks shell: a bright core flash plus trailing stars falling under gravity.</summary>
        public static void Firework(Vector3 pos, Color color)
        {
            var ps = MakeSystem("Firework", pos, color, 2.2f, 11f, 0.28f, 140);
            var main = ps.main;
            main.gravityModifier = 0.45f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.08f;
            limit.limit = 3f;
            var trails = ps.trails;
            trails.enabled = true;
            trails.lifetime = 0.25f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.trailMaterial = ExpoMaterials.ParticleAdditive;
            ps.Play();
            // No per-shell light: bloom carries the glow, and many large lights stall integrated GPUs.
        }

        /// <summary>Gold sand sparkles orbiting the courier while the Mirage Cloak is active.</summary>
        public static ParticleSystem SandAura(Transform parent)
        {
            var ps = MakeSystem("MirageSand", parent.position, ExpoPalette.Gold, 1.1f, 0.6f, 0.06f, 0, true);
            ps.transform.SetParent(parent, false);
            ps.transform.localPosition = new Vector3(0, 0.9f, 0);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            main.duration = 1f;
            var emission = ps.emission;
            emission.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.45f;
            shape.rotation = new Vector3(-90, 0, 0);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(2.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0.6f);
            return ps;
        }

        /// <summary>Slow floating motes for atmosphere around a region.</summary>
        public static ParticleSystem AmbientMotes(Vector3 center, Vector3 size, Color color, float rate)
        {
            var ps = MakeSystem("AmbientMotes", center, color, 7f, 0.15f, 0.07f, 0, true);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.18f);
            main.maxParticles = 400;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            ps.Play();
            return ps;
        }

        /// <summary>Expanding hologram ring (flat on the ground, or a sphere shell when spherical).</summary>
        public static void Ring(Vector3 pos, Color color, float radius, float duration, bool spherical = false)
        {
            var go = new GameObject("RingPulse");
            go.transform.position = pos;
            var mat = ExpoMaterials.Holo(color, 0.9f, true);
            Kit.Mesh(go.transform, "Ring", spherical ? MeshGen.Sphere(1f, 24, 14) : MeshGen.Annulus(0.88f, 1f, 64), mat, Vector3.zero, Vector3.one)
                .shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<RingPulse>().Init(mat, radius, duration);
        }
    }

    public class RingPulse : MonoBehaviour
    {
        Material mat;
        float radius, duration, age;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        public void Init(Material m, float r, float d)
        {
            mat = m; radius = r; duration = d;
            transform.localScale = Vector3.one * 0.1f;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            float e = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(0.1f, radius, e);
            mat.SetFloat(AlphaId, 0.9f * (1f - t));
            if (t >= 1f) { Destroy(gameObject); Destroy(mat); }
        }
    }

    public class LightFade : MonoBehaviour
    {
        Light l;
        float duration, age, start;

        public void Init(float d)
        {
            l = GetComponent<Light>();
            duration = d;
            start = l.intensity;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (l != null) l.intensity = start * Mathf.Clamp01(1f - age / duration);
            if (age >= duration) Destroy(gameObject);
        }
    }
}
