using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// Tariq, the consular courier: a stylized articulated stickman built from procedural meshes,
    /// with a courier harness, a glowing Sovereign Tri-Drive backpack and a wrist module.
    /// All animation is procedural (gait cycle, sprint lean, slide, interact and ability poses).
    /// </summary>
    public class StickmanRig : MonoBehaviour
    {
        public enum Gesture { None, Interact, Cloak, Prism, Chrono }

        // Joints
        Transform hips, chest, neck, head;
        Transform shoulderL, shoulderR, elbowL, elbowR;
        Transform hipL, hipR, kneeL, kneeR, ankleL, ankleR;
        Transform bodyRoot;

        readonly List<Renderer> bodyRenderers = new List<Renderer>();
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        Material visorMat, wristMat, harnessMat, cellMatUAE, cellMatFR, cellMatUK, jointMat;
        Light courierLight;
        Material cloakMat;

        float phase;
        float slideWeight, interactWeight, gestureWeight;
        Gesture gesture;
        float gestureTimer;
        float breathe;
        bool cloaked;

        public Transform WristPoint { get; private set; }
        public Transform BackpackPoint { get; private set; }
        public float StepPhase => phase;
        public event System.Action Footstep;
        float lastStepSign = 1f;

        const float HipHeight = 0.98f;

        public void Build()
        {
            var body = ExpoMaterials.Lit(new Color(0.11f, 0.125f, 0.17f), 0.62f, 0.45f);
            var dark = ExpoMaterials.Lit(new Color(0.05f, 0.055f, 0.075f), 0.5f, 0.3f);
            jointMat = ExpoMaterials.Emissive(new Color(0.75f, 0.92f, 1f), 1.6f);
            visorMat = ExpoMaterials.EmissiveInstance(ExpoPalette.Gold, 3.2f);
            wristMat = ExpoMaterials.EmissiveInstance(ExpoPalette.Gold, 3.5f);
            harnessMat = ExpoMaterials.EmissiveInstance(ExpoPalette.Gold, 2.2f);
            cellMatUAE = ExpoMaterials.EmissiveInstance(ExpoPalette.Gold, 4f);
            cellMatFR = ExpoMaterials.EmissiveInstance(ExpoPalette.Blue, 0.4f);
            cellMatUK = ExpoMaterials.EmissiveInstance(ExpoPalette.Emerald, 0.4f);
            cloakMat = ExpoMaterials.Holo(ExpoPalette.Gold, 0.55f, true);

            bodyRoot = new GameObject("Body").transform;
            bodyRoot.SetParent(transform, false);

            hips = Joint("Hips", bodyRoot, new Vector3(0, HipHeight, 0));
            Part("Pelvis", hips, MeshGen.Sphere(0.12f, 14, 8), body, new Vector3(0, 0.0f, 0), new Vector3(1.25f, 0.7f, 0.9f));
            Part("Belt", hips, MeshGen.Torus(0.125f, 0.018f, 24, 6), harnessMat, new Vector3(0, 0.05f, 0), Vector3.one);

            chest = Joint("Chest", hips, new Vector3(0, 0.08f, 0));
            Part("Torso", chest, MeshGen.UpCapsule(0.34f, 0.095f, 0.145f, 16), body, Vector3.zero, new Vector3(1.15f, 1f, 0.78f));
            // Courier harness: chest cross straps and a collar ring.
            Part("StrapA", chest, MeshGen.Box(new Vector3(0.03f, 0.42f, 0.02f)), harnessMat, new Vector3(0, 0.22f, 0.112f), Vector3.one, Quaternion.Euler(0, 0, 32f));
            Part("StrapB", chest, MeshGen.Box(new Vector3(0.03f, 0.42f, 0.02f)), harnessMat, new Vector3(0, 0.22f, 0.112f), Vector3.one, Quaternion.Euler(0, 0, -32f));
            Part("Collar", chest, MeshGen.Torus(0.085f, 0.016f, 20, 6), dark, new Vector3(0, 0.47f, 0), Vector3.one);
            Part("ChestEmblem", chest, MeshGen.Cylinder(0.035f, 0.035f, 0.012f, 6), jointMat, new Vector3(0, 0.25f, 0.122f), Vector3.one, Quaternion.Euler(90, 0, 0));

            // Sovereign Tri-Drive backpack with three key cells.
            var pack = Joint("TriDrive", chest, new Vector3(0, 0.25f, -0.15f));
            BackpackPoint = pack;
            Part("Shell", pack, MeshGen.Box(new Vector3(0.26f, 0.34f, 0.1f)), dark, Vector3.zero, Vector3.one);
            Part("Frame", pack, MeshGen.Box(new Vector3(0.28f, 0.36f, 0.04f)), body, new Vector3(0, 0, 0.03f), Vector3.one);
            Part("CellUAE", pack, MeshGen.Cylinder(0.032f, 0.032f, 0.26f, 10), cellMatUAE, new Vector3(-0.075f, -0.13f, -0.055f), Vector3.one);
            Part("CellFR", pack, MeshGen.Cylinder(0.032f, 0.032f, 0.26f, 10), cellMatFR, new Vector3(0f, -0.13f, -0.055f), Vector3.one);
            Part("CellUK", pack, MeshGen.Cylinder(0.032f, 0.032f, 0.26f, 10), cellMatUK, new Vector3(0.075f, -0.13f, -0.055f), Vector3.one);
            Part("PackSpine", pack, MeshGen.Box(new Vector3(0.012f, 0.3f, 0.012f)), harnessMat, new Vector3(0, 0, -0.06f), Vector3.one);

            var lightGo = new GameObject("CourierLight");
            lightGo.transform.SetParent(pack, false);
            lightGo.transform.localPosition = new Vector3(0, 0.05f, -0.35f);
            courierLight = lightGo.AddComponent<Light>();
            courierLight.type = LightType.Point;
            courierLight.range = 3.2f;
            courierLight.intensity = 1.1f;
            courierLight.color = ExpoPalette.Gold;
            courierLight.shadows = LightShadows.None;

            neck = Joint("Neck", chest, new Vector3(0, 0.46f, 0.0f));
            Part("NeckBar", neck, MeshGen.UpCapsule(0.07f, 0.035f, 0.035f, 10), body, Vector3.zero, Vector3.one);
            head = Joint("Head", neck, new Vector3(0, 0.11f, 0.01f));
            Part("Skull", head, MeshGen.Sphere(0.13f, 18, 12), body, new Vector3(0, 0.08f, 0), new Vector3(0.92f, 1.05f, 1f));
            Part("Visor", head, MeshGen.Box(new Vector3(0.2f, 0.055f, 0.06f)), visorMat, new Vector3(0, 0.1f, 0.095f), Vector3.one);
            Part("Antenna", head, MeshGen.Cylinder(0.006f, 0.004f, 0.14f, 6), dark, new Vector3(0.07f, 0.17f, -0.03f), Vector3.one, Quaternion.Euler(0, 0, -18f));
            Part("AntennaTip", head, MeshGen.Sphere(0.014f, 8, 6), jointMat, new Vector3(0.112f, 0.3f, -0.03f), Vector3.one);

            // Arms
            BuildArm(true, body, dark, out shoulderL, out elbowL);
            BuildArm(false, body, dark, out shoulderR, out elbowR);

            // Legs
            BuildLeg(true, body, dark, out hipL, out kneeL, out ankleL);
            BuildLeg(false, body, dark, out hipR, out kneeR, out ankleR);

            foreach (var r in bodyRoot.GetComponentsInChildren<Renderer>())
            {
                bodyRenderers.Add(r);
                originalMaterials[r] = r.sharedMaterials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        void BuildArm(bool left, Material body, Material dark, out Transform shoulder, out Transform elbow)
        {
            float side = left ? -1f : 1f;
            shoulder = Joint(left ? "ShoulderL" : "ShoulderR", chest, new Vector3(0.2f * side, 0.39f, 0));
            Part("ShoulderJoint", shoulder, MeshGen.Sphere(0.05f, 12, 8), jointMat, Vector3.zero, Vector3.one);
            Part("UpperArm", shoulder, MeshGen.Limb(0.29f, 0.042f, 0.034f), body, Vector3.zero, Vector3.one);
            elbow = Joint(left ? "ElbowL" : "ElbowR", shoulder, new Vector3(0, -0.29f, 0));
            Part("ElbowRing", elbow, MeshGen.Torus(0.036f, 0.009f, 16, 5), jointMat, Vector3.zero, Vector3.one, Quaternion.Euler(0, 0, 90));
            Part("Forearm", elbow, MeshGen.Limb(0.27f, 0.034f, 0.028f), body, Vector3.zero, Vector3.one);
            Part("Hand", elbow, MeshGen.Sphere(0.04f, 10, 8), dark, new Vector3(0, -0.3f, 0), new Vector3(0.85f, 1.15f, 0.7f));
            if (left)
            {
                // Wrist module that shows the selected passport.
                Part("WristModule", elbow, MeshGen.Box(new Vector3(0.07f, 0.09f, 0.075f)), dark, new Vector3(0, -0.18f, 0.005f), Vector3.one);
                Part("WristScreen", elbow, MeshGen.Box(new Vector3(0.05f, 0.065f, 0.012f)), wristMat, new Vector3(0, -0.18f, 0.044f), Vector3.one);
                var wp = new GameObject("WristPoint").transform;
                wp.SetParent(elbow, false);
                wp.localPosition = new Vector3(0, -0.2f, 0.08f);
                WristPoint = wp;
            }
        }

        void BuildLeg(bool left, Material body, Material dark, out Transform hip, out Transform knee, out Transform ankle)
        {
            float side = left ? -1f : 1f;
            hip = Joint(left ? "HipL" : "HipR", hips, new Vector3(0.095f * side, -0.03f, 0));
            Part("HipJoint", hip, MeshGen.Sphere(0.055f, 12, 8), jointMat, Vector3.zero, Vector3.one);
            Part("Thigh", hip, MeshGen.Limb(0.45f, 0.055f, 0.042f), body, Vector3.zero, Vector3.one);
            knee = Joint(left ? "KneeL" : "KneeR", hip, new Vector3(0, -0.45f, 0));
            Part("KneeRing", knee, MeshGen.Torus(0.045f, 0.01f, 16, 5), jointMat, Vector3.zero, Vector3.one, Quaternion.Euler(0, 0, 90));
            Part("Shin", knee, MeshGen.Limb(0.45f, 0.042f, 0.032f), body, Vector3.zero, Vector3.one);
            Part("ShinStripe", knee, MeshGen.Box(new Vector3(0.012f, 0.22f, 0.012f)), harnessMat, new Vector3(0, -0.2f, 0.04f), Vector3.one);
            ankle = Joint(left ? "AnkleL" : "AnkleR", knee, new Vector3(0, -0.45f, 0));
            Part("Foot", ankle, MeshGen.TaperedBox(new Vector2(0.09f, 0.22f), new Vector2(0.07f, 0.17f), 0.06f), dark, new Vector3(0, -0.04f, 0.05f), Vector3.one);
            Part("Sole", ankle, MeshGen.Box(new Vector3(0.092f, 0.012f, 0.222f)), harnessMat, new Vector3(0, -0.035f, 0.05f), Vector3.one);
        }

        static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static void Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.layer = ExpoLayers.Player;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ appearance

        public void SetPassport(PassportType active, bool[] unlocked)
        {
            Color c = ExpoPalette.ForPassport(active);
            ExpoMaterials.SetEmission(visorMat, c, 3.2f);
            ExpoMaterials.SetEmission(wristMat, c, 3.8f);
            ExpoMaterials.SetEmission(harnessMat, c, 2.0f);
            ExpoMaterials.SetEmission(cellMatUAE, ExpoPalette.Gold, CellIntensity(PassportType.UAE, active, unlocked));
            ExpoMaterials.SetEmission(cellMatFR, ExpoPalette.Blue, CellIntensity(PassportType.France, active, unlocked));
            ExpoMaterials.SetEmission(cellMatUK, ExpoPalette.Emerald, CellIntensity(PassportType.UK, active, unlocked));
            if (courierLight) courierLight.color = Color.Lerp(c, Color.white, 0.25f);
        }

        static float CellIntensity(PassportType cell, PassportType active, bool[] unlocked)
        {
            if (unlocked == null || !unlocked[(int)cell]) return 0.25f;
            return cell == active ? 5f : 1.4f;
        }

        public void SetCloaked(bool on)
        {
            if (cloaked == on) return;
            cloaked = on;
            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                if (on)
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = cloakMat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    r.sharedMaterials = originalMaterials[r];
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
            if (courierLight) courierLight.intensity = on ? 0.35f : 1.1f;
        }

        /// <summary>Spawns a fading hologram copy of the current pose (Mirage afterimage).</summary>
        public void SpawnAfterimage(Material mat, float lifetime)
        {
            var ghost = new GameObject("Afterimage");
            ghost.transform.SetPositionAndRotation(bodyRoot.position, bodyRoot.rotation);
            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null) continue;
                var g = new GameObject("g");
                g.transform.SetParent(ghost.transform, false);
                g.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                g.transform.localScale = r.transform.lossyScale;
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var fade = ghost.AddComponent<FadeAndDestroy>();
            fade.Init(mat, lifetime);
        }

        public void PlayGesture(Gesture g, float seconds)
        {
            gesture = g;
            gestureTimer = seconds;
        }

        // ------------------------------------------------------------------ animation

        public void Animate(float speed, float sprintSpeed, bool grounded, bool sliding, bool interacting, float dt)
        {
            if (hips == null) return;

            float runBlend = Mathf.Clamp01(speed / 4.5f);
            float sprintBlend = Mathf.Clamp01((speed - 4.8f) / Mathf.Max(0.1f, sprintSpeed - 4.8f));
            float stride = Mathf.Lerp(1.1f, 1.9f, sprintBlend);
            phase += speed * dt / stride;
            float cyc = phase * Mathf.PI * 2f;

            // Footstep events at each half cycle.
            float s = Mathf.Sin(cyc);
            if (speed > 0.6f && grounded && !sliding && Mathf.Sign(s) != lastStepSign)
            {
                lastStepSign = Mathf.Sign(s);
                Footstep?.Invoke();
            }

            slideWeight = Mathf.MoveTowards(slideWeight, sliding ? 1f : 0f, dt * (sliding ? 9f : 4f));
            interactWeight = Mathf.MoveTowards(interactWeight, interacting ? 1f : 0f, dt * 6f);
            gestureTimer -= dt;
            gestureWeight = Mathf.MoveTowards(gestureWeight, gestureTimer > 0f ? 1f : 0f, dt * 8f);
            breathe += dt;

            float legAmp = Mathf.Lerp(0f, 34f, runBlend) + sprintBlend * 16f;
            float kneeAmp = Mathf.Lerp(0f, 55f, runBlend) + sprintBlend * 30f;
            float armAmp = Mathf.Lerp(4f, 30f, runBlend) + sprintBlend * 20f;
            float lean = Mathf.Lerp(0f, 7f, runBlend) + sprintBlend * 11f;
            float bob = Mathf.Abs(Mathf.Cos(cyc)) * Mathf.Lerp(0.0f, 0.045f, runBlend);
            float idle = 1f - runBlend;
            float breath = Mathf.Sin(breathe * 1.8f) * idle;

            float sL = Mathf.Sin(cyc), sR = Mathf.Sin(cyc + Mathf.PI);
            float cL = Mathf.Cos(cyc), cR = Mathf.Cos(cyc + Mathf.PI);

            // Base gait pose (positive X on downward limbs swings them back).
            Quaternion qHipL = Quaternion.Euler(-sL * legAmp, 0, 0);
            Quaternion qHipR = Quaternion.Euler(-sR * legAmp, 0, 0);
            Quaternion qKneeL = Quaternion.Euler(4f + Mathf.Max(0f, cL) * kneeAmp, 0, 0);
            Quaternion qKneeR = Quaternion.Euler(4f + Mathf.Max(0f, cR) * kneeAmp, 0, 0);
            Quaternion qAnkleL = Quaternion.Euler(-Mathf.Max(0f, cL) * kneeAmp * 0.25f + sL * 8f * runBlend, 0, 0);
            Quaternion qAnkleR = Quaternion.Euler(-Mathf.Max(0f, cR) * kneeAmp * 0.25f + sR * 8f * runBlend, 0, 0);
            Quaternion qShL = Quaternion.Euler(sL * armAmp, 0, -6f - idle * 4f + breath * 1.5f);
            Quaternion qShR = Quaternion.Euler(sR * armAmp, 0, 6f + idle * 4f - breath * 1.5f);
            Quaternion qElL = Quaternion.Euler(-(14f + runBlend * 40f + sprintBlend * 30f), 0, 0);
            Quaternion qElR = Quaternion.Euler(-(14f + runBlend * 40f + sprintBlend * 30f), 0, 0);
            Quaternion qChest = Quaternion.Euler(lean + breath * 1.2f, -Mathf.Sin(cyc) * 8f * runBlend, 0);
            Quaternion qHips = Quaternion.Euler(0, Mathf.Sin(cyc) * 7f * runBlend, Mathf.Cos(cyc) * 2.5f * runBlend);
            Quaternion qHead = Quaternion.Euler(-lean * 0.6f + breath * 0.8f, 0, 0);
            Quaternion qRoot = Quaternion.identity;
            float hipY = HipHeight - bob;

            // Slide: low, leaning back, lead leg extended.
            if (slideWeight > 0.001f)
            {
                float w = slideWeight;
                qRoot = Quaternion.Slerp(qRoot, Quaternion.Euler(-16f, 0, 0), w);
                hipY = Mathf.Lerp(hipY, 0.42f, w);
                qHips = Quaternion.Slerp(qHips, Quaternion.identity, w);
                qChest = Quaternion.Slerp(qChest, Quaternion.Euler(-32f, 0, 0), w);
                qHead = Quaternion.Slerp(qHead, Quaternion.Euler(28f, 0, 0), w);
                qHipL = Quaternion.Slerp(qHipL, Quaternion.Euler(-82f, 0, -4f), w);
                qKneeL = Quaternion.Slerp(qKneeL, Quaternion.Euler(6f, 0, 0), w);
                qHipR = Quaternion.Slerp(qHipR, Quaternion.Euler(-30f, 0, 6f), w);
                qKneeR = Quaternion.Slerp(qKneeR, Quaternion.Euler(95f, 0, 0), w);
                qShL = Quaternion.Slerp(qShL, Quaternion.Euler(-40f, 0, -55f), w);
                qShR = Quaternion.Slerp(qShR, Quaternion.Euler(30f, 0, 40f), w);
                qElL = Quaternion.Slerp(qElL, Quaternion.Euler(-30f, 0, 0), w);
                qElR = Quaternion.Slerp(qElR, Quaternion.Euler(-20f, 0, 0), w);
            }

            // Interact: right hand reaches into the terminal.
            if (interactWeight > 0.001f)
            {
                float w = interactWeight;
                qShR = Quaternion.Slerp(qShR, Quaternion.Euler(-78f, 0, 8f), w);
                qElR = Quaternion.Slerp(qElR, Quaternion.Euler(-28f, 0, 0), w);
                qShL = Quaternion.Slerp(qShL, Quaternion.Euler(-35f, 0, -10f), w);
                qElL = Quaternion.Slerp(qElL, Quaternion.Euler(-95f, 0, 0), w);
                qHead = Quaternion.Slerp(qHead, Quaternion.Euler(10f, 0, 0), w);
            }

            // Ability gestures.
            if (gestureWeight > 0.001f && gesture != Gesture.None)
            {
                float w = gestureWeight;
                switch (gesture)
                {
                    case Gesture.Prism:
                        qShL = Quaternion.Slerp(qShL, Quaternion.Euler(-88f, 6f, 0f), w);
                        qElL = Quaternion.Slerp(qElL, Quaternion.Euler(-4f, 0, 0), w);
                        qChest = Quaternion.Slerp(qChest, Quaternion.Euler(lean, 18f, 0), w);
                        break;
                    case Gesture.Cloak:
                        qShL = Quaternion.Slerp(qShL, Quaternion.Euler(-60f, 0, -70f), w);
                        qShR = Quaternion.Slerp(qShR, Quaternion.Euler(-60f, 0, 70f), w);
                        qElL = Quaternion.Slerp(qElL, Quaternion.Euler(-70f, 0, 0), w);
                        qElR = Quaternion.Slerp(qElR, Quaternion.Euler(-70f, 0, 0), w);
                        break;
                    case Gesture.Chrono:
                        qShL = Quaternion.Slerp(qShL, Quaternion.Euler(-150f, 0, -25f), w);
                        qShR = Quaternion.Slerp(qShR, Quaternion.Euler(-150f, 0, 25f), w);
                        qElL = Quaternion.Slerp(qElL, Quaternion.Euler(-12f, 0, 0), w);
                        qElR = Quaternion.Slerp(qElR, Quaternion.Euler(-12f, 0, 0), w);
                        qHead = Quaternion.Slerp(qHead, Quaternion.Euler(-14f, 0, 0), w);
                        break;
                }
            }

            float k = 1f - Mathf.Exp(-22f * dt);
            bodyRoot.localRotation = Quaternion.Slerp(bodyRoot.localRotation, qRoot, k);
            var hp = hips.localPosition; hp.y = Mathf.Lerp(hp.y, hipY, k); hips.localPosition = hp;
            hips.localRotation = Quaternion.Slerp(hips.localRotation, qHips, k);
            chest.localRotation = Quaternion.Slerp(chest.localRotation, qChest, k);
            head.localRotation = Quaternion.Slerp(head.localRotation, qHead, k);
            hipL.localRotation = Quaternion.Slerp(hipL.localRotation, qHipL, k);
            hipR.localRotation = Quaternion.Slerp(hipR.localRotation, qHipR, k);
            kneeL.localRotation = Quaternion.Slerp(kneeL.localRotation, qKneeL, k);
            kneeR.localRotation = Quaternion.Slerp(kneeR.localRotation, qKneeR, k);
            ankleL.localRotation = Quaternion.Slerp(ankleL.localRotation, qAnkleL, k);
            ankleR.localRotation = Quaternion.Slerp(ankleR.localRotation, qAnkleR, k);
            shoulderL.localRotation = Quaternion.Slerp(shoulderL.localRotation, qShL, k);
            shoulderR.localRotation = Quaternion.Slerp(shoulderR.localRotation, qShR, k);
            elbowL.localRotation = Quaternion.Slerp(elbowL.localRotation, qElL, k);
            elbowR.localRotation = Quaternion.Slerp(elbowR.localRotation, qElR, k);
        }
    }

    /// <summary>Fades a hologram material copy and destroys the object.</summary>
    public class FadeAndDestroy : MonoBehaviour
    {
        Material mat;
        float life, age, startAlpha;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        public void Init(Material source, float lifetime)
        {
            mat = new Material(source);
            startAlpha = mat.HasProperty(AlphaId) ? mat.GetFloat(AlphaId) : 0.5f;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            life = lifetime;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            if (mat != null) mat.SetFloat(AlphaId, startAlpha * (1f - t) * (1f - t));
            if (t >= 1f)
            {
                Destroy(gameObject);
                if (mat != null) Destroy(mat);
            }
        }
    }
}
