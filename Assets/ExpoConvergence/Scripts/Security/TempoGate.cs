using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Emerald tempo gate: shutters that slam shut on a fast cycle with an energy field between them.
    /// Too quick to pass normally; under the Chrono Lock the open window becomes wide enough.
    /// </summary>
    public class TempoGate : MonoBehaviour, IChronoAffectable, IResettable
    {
        public float width = 4f;
        public float height = 3.2f;
        public float cycle = 1.15f;
        [Range(0.05f, 0.9f)] public float openFraction = 0.28f;
        public float phaseOffset;

        public Vector3 ChronoPosition => transform.position;
        Transform doorL, doorR;
        Material fieldMat;
        Renderer field;
        float time, chronoScale = 1f;
        float openAmount;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Build()
        {
            var frame = ExpoMaterials.Lit(new Color(0.06f, 0.1f, 0.08f), 0.65f, 0.85f);
            var emerald = ExpoMaterials.Emissive(ExpoPalette.Emerald, 3f);
            float hx = width * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.Mesh(transform, "Pillar", MeshGen.Box(new Vector3(0.45f, height + 0.6f, 0.7f)), frame, new Vector3((hx + 0.22f) * s, (height + 0.6f) * 0.5f, 0), Vector3.one);
                Kit.Mesh(transform, "PillarRing", MeshGen.Torus(0.18f, 0.03f, 20, 5), emerald, new Vector3((hx + 0.22f) * s, height + 0.25f, -0.36f), Vector3.one, Quaternion.Euler(90, 0, 0));
            }
            Kit.Mesh(transform, "Top", MeshGen.Box(new Vector3(width + 0.9f, 0.35f, 0.75f)), frame, new Vector3(0, height + 0.42f, 0), Vector3.one);
            Kit.Mesh(transform, "TopLight", MeshGen.Box(new Vector3(width, 0.05f, 0.77f)), emerald, new Vector3(0, height + 0.24f, 0), Vector3.one);

            doorL = new GameObject("ShutterL").transform; doorL.SetParent(transform, false);
            doorR = new GameObject("ShutterR").transform; doorR.SetParent(transform, false);
            Kit.Mesh(doorL, "Panel", MeshGen.Box(new Vector3(hx, height, 0.12f)), frame, new Vector3(-hx * 0.5f, height * 0.5f, 0), Vector3.one);
            Kit.Mesh(doorL, "Edge", MeshGen.Box(new Vector3(0.05f, height, 0.14f)), emerald, new Vector3(-0.03f, height * 0.5f, 0), Vector3.one);
            Kit.Mesh(doorR, "Panel", MeshGen.Box(new Vector3(hx, height, 0.12f)), frame, new Vector3(hx * 0.5f, height * 0.5f, 0), Vector3.one);
            Kit.Mesh(doorR, "Edge", MeshGen.Box(new Vector3(0.05f, height, 0.14f)), emerald, new Vector3(0.03f, height * 0.5f, 0), Vector3.one);

            fieldMat = ExpoMaterials.Field(ExpoPalette.Emerald, 0.45f, true);
            field = Kit.Mesh(transform, "Field", MeshGen.Quad(width, height), fieldMat, Vector3.zero, Vector3.one);
            field.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Kit.Text(transform, "TEMPO GATE  ·  UK KEY", new Vector3(0, height + 0.85f, -0.4f), 0.22f, ExpoPalette.Emerald);
        }

        void OnEnable() => SecurityRegistry.Register(this);
        void OnDisable() => SecurityRegistry.Unregister(this);
        public void SetChronoScale(float scale) => chronoScale = scale;
        public void ResetState() { time = 0f; chronoScale = 1f; }

        void Update()
        {
            if (Game.State == null || Game.State.Current == GameState.Paused) return;
            time += Time.deltaTime * chronoScale;
            float t = Mathf.Repeat(time / cycle + phaseOffset, 1f);
            // Snap open, hold, then slam shut.
            float target = t < openFraction ? 1f : 0f;
            openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime * chronoScale * 9f);
            float hx = width * 0.5f;
            doorL.localPosition = new Vector3(-hx * openAmount, 0, 0);
            doorR.localPosition = new Vector3(hx * openAmount, 0, 0);
            bool closed = openAmount < 0.35f;
            fieldMat.SetFloat(AlphaId, closed ? 0.5f : 0.06f);
            fieldMat.SetColor(ColorId, ExpoPalette.Hdr(chronoScale < 0.99f ? Color.Lerp(ExpoPalette.Emerald, Color.white, 0.25f) : ExpoPalette.Emerald, 1.8f));

            if (closed && Game.State.Current == GameState.Playing && Game.Player != null)
            {
                Vector3 local = transform.InverseTransformPoint(Game.Player.transform.position);
                if (Mathf.Abs(local.x) < hx && Mathf.Abs(local.z) < 0.4f)
                {
                    Game.Audio?.PlaySfxAt("zap", Game.Player.transform.position, 1f);
                    VfxFactory.Sparks(Game.Player.transform.position + Vector3.up, ExpoPalette.Emerald, 40);
                    Game.Detection?.Trip("TEMPO GATE  ·  CAUGHT IN THE SHUTTERS");
                }
            }
        }
    }
}
