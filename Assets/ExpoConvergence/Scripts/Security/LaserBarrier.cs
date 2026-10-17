using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Laser grid. Patterns: static, pulsing (on/off with a warning flicker), sweeping (a moving gantry)
    /// and rotating (Big Ben clock hands). Touching an active beam trips the grid instantly.
    /// Low static beams can be passed with a slide; the Chrono Lock slows every pattern.
    /// </summary>
    public class LaserBarrier : MonoBehaviour, IChronoAffectable, IResettable
    {
        public enum Pattern { Static, Pulse, Sweep, Rotate }

        public Pattern pattern = Pattern.Static;
        public float width = 6f;
        public float[] heights = { 0.5f, 1.1f, 1.7f };
        public float sweepDistance = 8f;
        public float sweepPeriod = 5f;
        public float rotateSpeed = 40f;
        public float radius = 6f;
        public int arms = 2;
        public float onTime = 1.6f;
        public float offTime = 1.4f;
        public Color color = ExpoPalette.Alert;

        public Vector3 ChronoPosition => transform.position;
        public bool IsActive { get; private set; } = true;

        readonly List<LineRenderer> beams = new List<LineRenderer>();
        readonly List<Vector3> localA = new List<Vector3>();
        readonly List<Vector3> localB = new List<Vector3>();
        Transform gantry;
        Material beamMat;
        float time, chronoScale = 1f;
        Light glow;

        public void Build()
        {
            gantry = new GameObject("Gantry").transform;
            gantry.SetParent(transform, false);
            beamMat = ExpoMaterials.Beam(color, 4f, true);
            var graphite = ExpoMaterials.Lit(new Color(0.08f, 0.085f, 0.11f), 0.6f, 0.7f);
            var slot = ExpoMaterials.Emissive(color, 3f);

            if (pattern == Pattern.Rotate)
            {
                Kit.Mesh(transform, "Hub", MeshGen.Cylinder(0.55f, 0.45f, 0.5f, 12), graphite, Vector3.zero, Vector3.one);
                Kit.Mesh(transform, "HubCore", MeshGen.Cylinder(0.25f, 0.25f, 0.62f, 12), slot, Vector3.zero, Vector3.one);
                for (int a = 0; a < arms; a++)
                {
                    float yaw = 360f * a / arms;
                    foreach (float h in heights)
                    {
                        Vector3 dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        AddBeam(dir * 0.5f + Vector3.up * h, dir * radius + Vector3.up * h);
                    }
                    Vector3 tipDir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    Kit.Mesh(gantry, "ArmTip", MeshGen.Cylinder(0.12f, 0.12f, heights[heights.Length - 1] + 0.25f, 8), graphite, tipDir * (radius + 0.12f), Vector3.one);
                }
            }
            else
            {
                float hx = width * 0.5f;
                float top = 0f;
                foreach (float h in heights) top = Mathf.Max(top, h);
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = Kit.Mesh(gantry, side < 0 ? "EmitterL" : "EmitterR", MeshGen.Box(new Vector3(0.22f, top + 0.45f, 0.22f)), graphite,
                        new Vector3(hx * side + 0.11f * side, (top + 0.45f) * 0.5f, 0), Vector3.one);
                    foreach (float h in heights)
                        Kit.Mesh(post.transform, "Slot", MeshGen.Box(new Vector3(0.05f, 0.08f, 0.24f)), slot, new Vector3(-0.1f * side, h - (top + 0.45f) * 0.5f, 0), Vector3.one);
                    Kit.Mesh(post.transform, "Cap", MeshGen.Box(new Vector3(0.28f, 0.06f, 0.28f)), slot, new Vector3(0, (top + 0.45f) * 0.5f, 0), Vector3.one);
                }
                foreach (float h in heights) AddBeam(new Vector3(-hx, h, 0), new Vector3(hx, h, 0));
            }

            var lightGo = new GameObject("LaserGlow");
            lightGo.transform.SetParent(gantry, false);
            lightGo.transform.localPosition = new Vector3(0, 1f, 0);
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = pattern == Pattern.Rotate ? radius * 1.2f : width * 0.8f;
            glow.intensity = 1.6f;
            glow.shadows = LightShadows.None;

            UpdateBeams(true);
        }

        void AddBeam(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Beam");
            go.transform.SetParent(gantry, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.widthMultiplier = 0.075f;
            lr.textureMode = LineTextureMode.Stretch;
            lr.sharedMaterial = beamMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 2;
            beams.Add(lr);
            localA.Add(a);
            localB.Add(b);
        }

        void OnEnable() => SecurityRegistry.Register(this);
        void OnDisable() => SecurityRegistry.Unregister(this);

        public void SetChronoScale(float scale) => chronoScale = scale;

        public void ResetState()
        {
            time = 0f;
            chronoScale = 1f;
            UpdateBeams(true);
        }

        void Update()
        {
            if (Game.State == null) return;
            var s = Game.State.Current;
            if (s == GameState.Paused || s == GameState.Victory) return;
            time += Time.deltaTime * chronoScale;
            UpdateBeams(false);
            if (s == GameState.Playing && IsActive) CheckPlayer();
        }

        void UpdateBeams(bool force)
        {
            float alpha = 1f;
            switch (pattern)
            {
                case Pattern.Pulse:
                {
                    float cycle = onTime + offTime;
                    float t = time % cycle;
                    IsActive = t < onTime;
                    // Warning flicker during the last 0.35 s before switching on.
                    if (!IsActive) alpha = t > cycle - 0.35f ? (Mathf.Repeat(time * 20f, 1f) > 0.5f ? 0.35f : 0.05f) : 0.03f;
                    break;
                }
                case Pattern.Sweep:
                    gantry.localPosition = new Vector3(0, 0, Mathf.Sin(time / sweepPeriod * Mathf.PI * 2f) * sweepDistance * 0.5f);
                    break;
                case Pattern.Rotate:
                    gantry.localRotation = Quaternion.Euler(0, time * rotateSpeed, 0);
                    break;
            }
            beamMat.SetFloat("_Alpha", alpha);
            if (glow != null) glow.intensity = IsActive ? 1.6f : 0.2f;
            float tint = chronoScale < 0.99f ? 0.55f : 0f;
            beamMat.SetColor("_Color", ExpoPalette.Hdr(Color.Lerp(color, ExpoPalette.Emerald, tint), 4f));
        }

        void CheckPlayer()
        {
            var player = Game.Player;
            if (player == null) return;
            Vector3 p0 = player.transform.position + Vector3.up * player.CapsuleRadius;
            Vector3 p1 = player.transform.position + Vector3.up * Mathf.Max(player.CapsuleRadius, player.CapsuleHeight - player.CapsuleRadius);
            float r = player.CapsuleRadius * 0.9f + 0.04f;
            for (int i = 0; i < beams.Count; i++)
            {
                Vector3 a = gantry.TransformPoint(localA[i]);
                Vector3 b = gantry.TransformPoint(localB[i]);
                if (SegmentDistance(p0, p1, a, b) < r)
                {
                    Game.Audio?.PlaySfxAt("zap", player.transform.position, 1f);
                    VfxFactory.Sparks(player.transform.position + Vector3.up, color, 40);
                    Game.Detection?.Trip("LASER GRID TRIPPED");
                    return;
                }
            }
        }

        /// <summary>Shortest distance between segments p1-q1 and p2-q2.</summary>
        public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-6f && e <= 1e-6f) return r.magnitude;
            if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
        }
    }
}
