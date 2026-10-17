using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// A consular terminal. Hold E while presenting the required passport to authenticate.
    /// Presenting the wrong passport is rejected and flagged to the security grid.
    /// </summary>
    public class ConsularGateTerminal : MonoBehaviour
    {
        public static readonly List<ConsularGateTerminal> All = new List<ConsularGateTerminal>();
        public static ConsularGateTerminal Focused { get; private set; }

        public PassportType required = PassportType.UAE;
        public bool anyPassport;
        public string title = "CONSULAR GATE";
        public string action = "AUTHENTICATE";
        public float holdTime = 1.2f;
        public float radius = 2.4f;
        public Action onComplete;

        public bool Done { get; private set; }
        public float Progress { get; private set; }
        public Color AccentColor => anyPassport ? Color.white : ExpoPalette.ForPassport(required);
        public string PromptText => anyPassport ? $"[E]  {action}" : $"[E]  {action}  ·  {ExpoPalette.CountryName(required)} PASSPORT";

        Material screenMat;
        Transform holo;
        TextMeshPro label;
        bool interactingHere;
        float denyCooldown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); Focused = null; }

        void OnEnable() => All.Add(this);
        void OnDisable()
        {
            All.Remove(this);
            if (Focused == this) Focused = null;
        }

        public void Build(string heading)
        {
            title = heading;
            Color c = AccentColor;
            var body = ExpoMaterials.Lit(new Color(0.1f, 0.11f, 0.15f), 0.7f, 0.75f);
            var trim = ExpoMaterials.Emissive(c, 2.6f);
            screenMat = ExpoMaterials.EmissiveInstance(c, 1.8f);

            Kit.Block(transform, "Plinth", new Vector3(0, 0.08f, 0), new Vector3(1.3f, 0.16f, 1.0f), body);
            Kit.Block(transform, "Column", new Vector3(0, 0.6f, 0.05f), new Vector3(0.55f, 1.0f, 0.45f), body);
            Kit.Mesh(transform, "ColumnLight", MeshGen.Box(new Vector3(0.05f, 0.9f, 0.47f)), trim, new Vector3(0.29f, 0.6f, 0.05f), Vector3.one);
            Kit.Mesh(transform, "ColumnLight2", MeshGen.Box(new Vector3(0.05f, 0.9f, 0.47f)), trim, new Vector3(-0.29f, 0.6f, 0.05f), Vector3.one);
            var head = Kit.Mesh(transform, "Console", MeshGen.Box(new Vector3(0.9f, 0.08f, 0.6f)), body, new Vector3(0, 1.17f, -0.05f), Vector3.one, Quaternion.Euler(-28f, 0, 0));
            Kit.Mesh(head.transform, "Screen", MeshGen.Box(new Vector3(0.78f, 0.02f, 0.48f)), screenMat, new Vector3(0, 0.045f, 0), Vector3.one);
            Kit.Mesh(transform, "Scanner", MeshGen.Torus(0.2f, 0.02f, 24, 5), trim, new Vector3(0, 1.25f, 0.32f), Vector3.one, Quaternion.Euler(62f, 0, 0));

            holo = new GameObject("PassportHolo").transform;
            holo.SetParent(transform, false);
            holo.localPosition = new Vector3(0, 2.15f, 0);
            var holoMat = ExpoMaterials.Holo(c, 0.7f);
            Kit.Mesh(holo, "Card", MeshGen.Box(new Vector3(0.5f, 0.68f, 0.03f)), holoMat, Vector3.zero, Vector3.one);
            Kit.Mesh(holo, "Seal", MeshGen.Torus(0.13f, 0.02f, 24, 5), holoMat, new Vector3(0, 0.08f, 0.02f), Vector3.one, Quaternion.Euler(90, 0, 0));
            Kit.Mesh(holo, "Line1", MeshGen.Box(new Vector3(0.32f, 0.025f, 0.035f)), holoMat, new Vector3(0, -0.15f, 0.0f), Vector3.one);
            Kit.Mesh(holo, "Line2", MeshGen.Box(new Vector3(0.22f, 0.025f, 0.035f)), holoMat, new Vector3(0, -0.22f, 0.0f), Vector3.one);

            label = Kit.Text(transform, title, new Vector3(0, 2.75f, 0), 0.24f, c);
            Kit.PointLight(transform, new Vector3(0, 1.7f, -0.6f), c, 4f, 1.8f);
        }

        void Update()
        {
            if (holo != null)
            {
                holo.localRotation = Quaternion.Euler(0, Time.time * 45f, 0);
                holo.localPosition = new Vector3(0, 2.15f + Mathf.Sin(Time.time * 1.6f) * 0.06f, 0);
            }
            if (label != null && Game.CameraRig != null)
            {
                Vector3 toCam = label.transform.position - Game.CameraRig.transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.01f) label.transform.rotation = Quaternion.LookRotation(toCam);
            }
            denyCooldown -= Time.deltaTime;

            var player = Game.Player;
            if (Done || player == null || Game.State == null || Game.State.Current != GameState.Playing)
            {
                ReleaseFocus(player);
                return;
            }

            float dist = Vector3.Distance(player.transform.position, transform.position);
            bool inRange = dist <= radius && IsClosest(dist);
            if (!inRange) { ReleaseFocus(player); return; }

            Focused = this;
            bool correct = anyPassport || (Game.Passports != null && Game.Passports.Active == required);

            if (PlayerInput.InteractHeld)
            {
                if (!correct)
                {
                    if (PlayerInput.InteractPressed && denyCooldown <= 0f)
                    {
                        denyCooldown = 1f;
                        Game.Audio?.PlaySfxAt("deny", transform.position, 1f);
                        Game.Detection?.Spike(0.18f, player.transform.position);
                        GameEvents.RaiseToast($"PASSPORT MISMATCH  ·  PRESENT {ExpoPalette.CountryName(required)} [{(int)required + 1}]", ExpoPalette.Alert, 2.2f);
                    }
                    Progress = 0f;
                }
                else
                {
                    if (!interactingHere) Game.Audio?.StartLoop("terminal", transform.position);
                    interactingHere = true;
                    player.IsInteracting = true;
                    Vector3 face = transform.position - player.transform.position; face.y = 0f;
                    if (face.sqrMagnitude > 0.01f)
                        player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(face), 540f * Time.deltaTime);
                    Progress += Time.deltaTime / holdTime;
                    ExpoMaterials.SetEmission(screenMat, AccentColor, 1.8f + Progress * 4f);
                    if (Progress >= 1f) Complete();
                }
            }
            else
            {
                Progress = Mathf.MoveTowards(Progress, 0f, Time.deltaTime * 2f);
                if (interactingHere) { interactingHere = false; player.IsInteracting = false; Game.Audio?.StopLoop("terminal"); }
            }
        }

        bool IsClosest(float myDist)
        {
            var player = Game.Player;
            foreach (var t in All)
            {
                if (t == this || t.Done) continue;
                float d = Vector3.Distance(player.transform.position, t.transform.position);
                if (d < myDist && d <= t.radius) return false;
            }
            return true;
        }

        void ReleaseFocus(PlayerCourierController player)
        {
            if (Focused == this) Focused = null;
            if (interactingHere)
            {
                interactingHere = false;
                if (player != null) player.IsInteracting = false;
                Game.Audio?.StopLoop("terminal");
            }
        }

        void Complete()
        {
            Done = true;
            Progress = 1f;
            ReleaseFocus(Game.Player);
            ExpoMaterials.SetEmission(screenMat, AccentColor, 6f);
            if (label != null) label.text = title + "  ·  AUTHENTICATED";
            Game.Audio?.PlaySfxAt("authenticate", transform.position, 1f);
            VfxFactory.Ring(transform.position + Vector3.up * 0.05f, AccentColor, 4f, 0.7f);
            VfxFactory.Burst(transform.position + Vector3.up * 2.1f, AccentColor, 50, 4f);
            onComplete?.Invoke();
        }
    }

    /// <summary>Sliding double door across a corridor. Blocks until opened.</summary>
    public class SciFiDoor : MonoBehaviour
    {
        public float width = 6f;
        public float height = 4.5f;
        public Color color = Color.white;
        public bool IsOpen { get; private set; }

        Transform left, right;
        BoxCollider blocker;
        Material fieldMat;
        float open;
        static readonly int OpenId = Shader.PropertyToID("_Open");

        public void Build(string caption)
        {
            var frame = ExpoMaterials.Lit(new Color(0.09f, 0.1f, 0.13f), 0.65f, 0.85f);
            var panelMat = ExpoMaterials.Lit(new Color(0.16f, 0.18f, 0.23f), 0.75f, 0.6f);
            var trim = ExpoMaterials.Emissive(color, 2.6f);
            float hx = width * 0.5f;
            for (int s = -1; s <= 1; s += 2)
                Kit.Block(transform, "Jamb", new Vector3((hx + 0.35f) * s, (height + 0.8f) * 0.5f, 0), new Vector3(0.7f, height + 0.8f, 1.1f), frame);
            Kit.Block(transform, "Header", new Vector3(0, height + 0.5f, 0), new Vector3(width + 1.4f, 1.0f, 1.1f), frame);
            Kit.Mesh(transform, "HeaderLight", MeshGen.Box(new Vector3(width, 0.06f, 1.12f)), trim, new Vector3(0, height + 0.05f, 0), Vector3.one);

            left = new GameObject("DoorL").transform; left.SetParent(transform, false);
            right = new GameObject("DoorR").transform; right.SetParent(transform, false);
            Kit.Mesh(left, "Panel", MeshGen.Box(new Vector3(hx, height, 0.3f)), panelMat, new Vector3(-hx * 0.5f, height * 0.5f, 0), Vector3.one);
            Kit.Mesh(left, "Stripe", MeshGen.Box(new Vector3(0.08f, height * 0.8f, 0.32f)), trim, new Vector3(-0.25f, height * 0.5f, 0), Vector3.one);
            Kit.Mesh(right, "Panel", MeshGen.Box(new Vector3(hx, height, 0.3f)), panelMat, new Vector3(hx * 0.5f, height * 0.5f, 0), Vector3.one);
            Kit.Mesh(right, "Stripe", MeshGen.Box(new Vector3(0.08f, height * 0.8f, 0.32f)), trim, new Vector3(0.25f, height * 0.5f, 0), Vector3.one);

            fieldMat = ExpoMaterials.Field(color, 0.35f, true);
            var f = Kit.Mesh(transform, "Field", MeshGen.Quad(width, height), fieldMat, new Vector3(0, 0, -0.2f), Vector3.one);
            f.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Caption on both faces (text reads from its -Z side).
            Kit.Text(transform, caption, new Vector3(0, height + 1.35f, -0.6f), 0.3f, color);
            Kit.Text(transform, caption, new Vector3(0, height + 1.35f, 0.6f), 0.3f, color).transform.localRotation = Quaternion.Euler(0, 180f, 0);

            var b = new GameObject("Blocker");
            b.transform.SetParent(transform, false);
            blocker = b.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, height * 0.5f, 0);
            blocker.size = new Vector3(width, height, 0.6f);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            blocker.enabled = false;
            Game.Audio?.PlaySfxAt("door", transform.position + Vector3.up * 2f, 1f);
        }

        void Update()
        {
            if (left == null) return;
            open = Mathf.MoveTowards(open, IsOpen ? 1f : 0f, Time.deltaTime * 0.9f);
            float e = Mathf.SmoothStep(0f, 1f, open);
            float hx = width * 0.5f;
            left.localPosition = new Vector3(-hx * 0.95f * e, 0, 0);
            right.localPosition = new Vector3(hx * 0.95f * e, 0, 0);
            fieldMat.SetFloat(OpenId, e);
        }
    }
}
