using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Gold thermal turnstile: an energy field that physically blocks the courier unless the
    /// UAE Mirage Cloak is active. Pushing into it uncloaked spikes detection.
    /// </summary>
    public class ThermalTurnstile : MonoBehaviour, IResettable
    {
        public float width = 4f;
        public float height = 3f;

        BoxCollider blocker;
        Material fieldMat;
        float spikeCooldown, hintCooldown;
        bool passable;
        static readonly int RipplePos = Shader.PropertyToID("_RipplePos");
        static readonly int RippleTime = Shader.PropertyToID("_RippleTime");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        public void Build()
        {
            var frame = ExpoMaterials.Lit(new Color(0.16f, 0.13f, 0.09f), 0.7f, 0.85f);
            var gold = ExpoMaterials.Emissive(ExpoPalette.Gold, 3.4f);
            float hx = width * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.Mesh(transform, "Post", MeshGen.Box(new Vector3(0.32f, height + 0.4f, 0.5f)), frame, new Vector3((hx + 0.16f) * s, (height + 0.4f) * 0.5f, 0), Vector3.one);
                Kit.Mesh(transform, "PostLight", MeshGen.Box(new Vector3(0.06f, height, 0.52f)), gold, new Vector3((hx + 0.02f) * s, height * 0.5f + 0.1f, 0), Vector3.one);
            }
            Kit.Mesh(transform, "Lintel", MeshGen.Box(new Vector3(width + 0.64f, 0.3f, 0.6f)), frame, new Vector3(0, height + 0.25f, 0), Vector3.one);
            Kit.Mesh(transform, "LintelLight", MeshGen.Box(new Vector3(width, 0.05f, 0.62f)), gold, new Vector3(0, height + 0.08f, 0), Vector3.one);
            Kit.Mesh(transform, "Sill", MeshGen.Box(new Vector3(width + 0.64f, 0.06f, 0.7f)), gold, new Vector3(0, 0.03f, 0), Vector3.one);

            fieldMat = ExpoMaterials.Field(ExpoPalette.Gold, 0.5f, true);
            var field = Kit.Mesh(transform, "Field", MeshGen.Quad(width, height), fieldMat, new Vector3(0, 0.06f, 0), Vector3.one);
            field.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            field.gameObject.layer = ExpoLayers.Sensor;

            Kit.Text(transform, "THERMAL TURNSTILE  ·  UAE KEY", new Vector3(0, height + 0.62f, -0.31f), 0.22f, ExpoPalette.Gold);

            var b = new GameObject("Blocker");
            b.layer = ExpoLayers.Sensor;
            b.transform.SetParent(transform, false);
            blocker = b.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, height * 0.5f, 0);
            blocker.size = new Vector3(width, height, 0.4f);

            var lightGo = new GameObject("FieldLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = ExpoPalette.Gold;
            l.range = width * 1.3f;
            l.intensity = 2.2f;
            l.shadows = LightShadows.None;
        }

        void OnEnable() => SecurityRegistry.Register(this);
        void OnDisable() => SecurityRegistry.Unregister(this);

        public void ResetState() { spikeCooldown = 0f; }

        void Update()
        {
            var player = Game.Player;
            if (player == null || blocker == null) return;
            float dt = Time.deltaTime;
            spikeCooldown -= dt;
            hintCooldown -= dt;

            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            bool withinSpan = Mathf.Abs(local.x) < width * 0.5f + 0.4f;
            bool near = withinSpan && Mathf.Abs(local.z) < 2.6f;
            bool inside = withinSpan && Mathf.Abs(local.z) < 0.75f;

            // Passable while cloaked, and never re-solidify around a courier standing inside it.
            bool shouldPass = (player.IsCloaked && near) || (passable && inside);
            if (shouldPass != passable)
            {
                passable = shouldPass;
                blocker.enabled = !passable;
                if (passable)
                {
                    fieldMat.SetVector(RipplePos, player.transform.position + Vector3.up);
                    fieldMat.SetFloat(RippleTime, Time.timeSinceLevelLoad);
                    Game.Audio?.PlaySfxAt("field_pass", transform.position + Vector3.up * 1.5f, 0.8f);
                }
            }
            fieldMat.SetFloat(AlphaId, Mathf.Lerp(fieldMat.GetFloat(AlphaId), passable ? 0.18f : 0.5f, 1f - Mathf.Exp(-8f * dt)));

            // Pressing into the field uncloaked: buzz, push back, raise suspicion.
            if (!passable && Mathf.Abs(local.z) < 0.85f && withinSpan && Game.State != null && Game.State.Current == GameState.Playing)
            {
                if (spikeCooldown <= 0f)
                {
                    spikeCooldown = 1.2f;
                    Game.Detection?.Spike(0.3f, player.transform.position);
                    Game.Audio?.PlaySfxAt("deny", transform.position + Vector3.up * 1.5f, 1f);
                    fieldMat.SetVector(RipplePos, player.transform.position + Vector3.up);
                    fieldMat.SetFloat(RippleTime, Time.timeSinceLevelLoad);
                    Vector3 away = transform.TransformDirection(new Vector3(0, 0, Mathf.Sign(local.z == 0 ? -1 : local.z)));
                    player.Knockback(away, 5f);
                }
                if (hintCooldown <= 0f)
                {
                    hintCooldown = 6f;
                    GameEvents.RaiseHint("Thermal turnstile  ·  select UAE [1] and RIGHT-CLICK for MIRAGE CLOAK", 4f);
                }
            }
        }
    }
}
