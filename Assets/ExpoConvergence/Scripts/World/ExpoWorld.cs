using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExpoConvergence
{
    /// <summary>
    /// Builds Expo City at night: a ring of three pavilion districts (UAE west, France north, UK east)
    /// around a lagoon and the Al Wasl Dome, plus the arrival concourse and the convergence walk.
    /// Route: arrival → UAE (north) → France (east) → UK (south) → convergence walk (west) → bridge → dome.
    /// Every district is built in a local frame: corridor along +Z (0..80), outer facade at x = -9,
    /// lagoon railing at x = +9.
    /// </summary>
    public partial class ExpoWorld : MonoBehaviour
    {
        public const float Inner = 40f;   // inner square half-size (lagoon edge)
        public const float Outer = 58f;   // outer wall half-size
        public const float Half = 9f;     // corridor half-width
        public const float Length = 80f;  // district corridor length

        public Vector3 PlayerStart { get; private set; } = new Vector3(0f, 0f, -52f);
        public float PlayerStartYaw { get; private set; }

        Transform staticRoot, dynamicRoot;
        readonly LandmarkController[] landmarks = new LandmarkController[3];
        SciFiDoor doorUAE, doorFrance, doorUK, doorWalk;
        AlWaslDome dome;
        readonly List<Renderer> waterOrbs = new List<Renderer>();
        readonly SortedList<int, ConsularGateTerminal> objectiveChain = new SortedList<int, ConsularGateTerminal>();

        /// <summary>The next terminal the courier should reach (drives the HUD waypoint).</summary>
        public ConsularGateTerminal CurrentObjective
        {
            get
            {
                foreach (var kv in objectiveChain) if (kv.Value != null && !kv.Value.Done) return kv.Value;
                return null;
            }
        }

        void RegisterObjective(int order, ConsularGateTerminal t) => objectiveChain[order] = t;
        Material waterMat;

        // Shared materials
        Material mFloor, mFloorDark, mPath, mWall, mMetal, mDarkMetal, mGlass, mWhite;
        Material mSand, mSandDark, mLime, mSlate, mUkStone, mHedge, mTrunk, mLeaf;
        Material mMashrabiya, mWindows, mClock;

        // District frames: static (batched) and dynamic (gameplay) share the same transform.
        struct Frame { public Transform s, d; public Vector3 P(float x, float y, float z) => s.TransformPoint(new Vector3(x, y, z)); }

        public void Build()
        {
            Game.World = this;
            staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(transform, false);
            dynamicRoot = new GameObject("Dynamic").transform;
            dynamicRoot.SetParent(transform, false);

            LoadMaterials();
            BuildEnvironment();
            BuildGround();
            BuildBoundaries();
            BuildStartPlaza();
            BuildTutorialPath();
            BuildUAEDistrict();
            BuildFranceDistrict();
            BuildUKDistrict();
            BuildConvergenceWalk();
            BuildCornerPlazas();
            BuildDome();
            BuildLandmarks();
            BuildSkyline();
            BuildAtmosphere();
            RegisterCheckpoints();

            ExtractScriptedObjects();
            StaticBatchingUtility.Combine(staticRoot.gameObject);
        }

        /// <summary>
        /// Moves every scripted object (animated props, camera-facing text) from the static root to the
        /// dynamic root before static batching, keeping hierarchies intact.
        /// </summary>
        void ExtractScriptedObjects()
        {
            var behaviours = staticRoot.GetComponentsInChildren<MonoBehaviour>(true);
            var toMove = new List<Transform>();
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                bool ancestorScripted = false;
                for (var p = mb.transform.parent; p != null && p != staticRoot; p = p.parent)
                    if (p.GetComponent<MonoBehaviour>() != null) { ancestorScripted = true; break; }
                if (!ancestorScripted && !toMove.Contains(mb.transform)) toMove.Add(mb.transform);
            }
            foreach (var t in toMove) t.SetParent(dynamicRoot, true);
        }

        void LoadMaterials()
        {
            mFloor = Mat("M_Floor", new Color(0.09f, 0.11f, 0.16f), 0.8f, 0.15f);
            mFloorDark = Mat("M_FloorDark", new Color(0.05f, 0.06f, 0.09f), 0.85f, 0.2f);
            mPath = Mat("M_Path", new Color(0.13f, 0.15f, 0.2f), 0.7f, 0.1f);
            mWall = Mat("M_Wall", new Color(0.08f, 0.1f, 0.15f), 0.35f, 0.25f);
            mMetal = Mat("M_Metal", new Color(0.22f, 0.25f, 0.31f), 0.75f, 0.85f);
            mDarkMetal = Mat("M_DarkMetal", new Color(0.06f, 0.065f, 0.085f), 0.65f, 0.8f);
            mWhite = Mat("M_White", new Color(0.72f, 0.76f, 0.82f), 0.6f, 0.2f);
            mGlass = ExpoMaterials.Get("M_Glass");
            mSand = Mat("M_Sandstone", new Color(0.45f, 0.33f, 0.2f), 0.25f, 0f);
            mSandDark = Mat("M_SandstoneDark", new Color(0.24f, 0.17f, 0.1f), 0.3f, 0f);
            mLime = Mat("M_Limestone", new Color(0.52f, 0.52f, 0.55f), 0.35f, 0f);
            mSlate = Mat("M_Slate", new Color(0.07f, 0.08f, 0.11f), 0.55f, 0.4f);
            mUkStone = Mat("M_UKStone", new Color(0.07f, 0.09f, 0.08f), 0.45f, 0.2f);
            mHedge = Mat("M_Hedge", new Color(0.03f, 0.1f, 0.05f), 0.15f, 0f);
            mTrunk = Mat("M_PalmTrunk", new Color(0.2f, 0.14f, 0.09f), 0.2f, 0f);
            mLeaf = Mat("M_PalmLeaf", new Color(0.05f, 0.16f, 0.08f), 0.35f, 0f);
            mMashrabiya = ExpoMaterials.Get("M_Mashrabiya");
            mWindows = ExpoMaterials.Get("M_Windows");
            mClock = ExpoMaterials.Get("M_ClockFace");
            // Per-session instance so the scrolling ripples never dirty the material asset.
            waterMat = new Material(ExpoMaterials.Get("M_Water"));
            gameObject.AddComponent<WaterAnimator>().material = waterMat;
        }

        static Material Mat(string name, Color fallback, float smooth, float metal)
        {
            var m = Resources.Load<Material>("ExpoMaterials/" + name);
            return m != null ? m : ExpoMaterials.Lit(fallback, smooth, metal);
        }

        Frame MakeFrame(string name, Vector3 origin, float yaw)
        {
            var f = new Frame
            {
                s = Kit.Group(staticRoot, name, origin, yaw),
                d = Kit.Group(dynamicRoot, name, origin, yaw),
            };
            return f;
        }

        // ================================================================ environment

        void BuildEnvironment()
        {
            // Moonlight: cool, soft, the only shadow-casting light.
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(transform, false);
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.66f, 1f);
            moon.intensity = 0.42f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.75f;
            moon.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = moon;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.085f, 0.11f, 0.2f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.075f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.025f, 0.035f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0088f;
            RenderSettings.fogColor = new Color(0.035f, 0.05f, 0.095f);
            var sky = ExpoMaterials.Sky;
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Realtime reflection probes rendered once after the world is built (box projected).</summary>
        public void RenderReflectionProbes()
        {
            AddProbe(new Vector3(0, 6, -49), new Vector3(120, 30, 24));
            AddProbe(new Vector3(-49, 6, 0), new Vector3(24, 30, 120));
            AddProbe(new Vector3(0, 6, 49), new Vector3(120, 30, 24));
            AddProbe(new Vector3(49, 6, 0), new Vector3(24, 30, 120));
            AddProbe(new Vector3(0, 8, 0), new Vector3(82, 40, 82));
        }

        void AddProbe(Vector3 center, Vector3 size)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.resolution = 128;
            p.size = size;
            p.boxProjection = true;
            p.hdr = true;
            p.shadowDistance = 0f;
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            p.intensity = 1.1f;
            p.RenderProbe();
        }

        // ================================================================ ground

        void BuildGround()
        {
            var g = Kit.Group(staticRoot, "Ground", Vector3.zero);
            // Ring corridor floors (south and north span the corners).
            FloorSlab(g, new Vector3(0, 0, -(Inner + Outer) * 0.5f), new Vector2(Outer * 2f, Outer - Inner), mFloor);
            FloorSlab(g, new Vector3(0, 0, (Inner + Outer) * 0.5f), new Vector2(Outer * 2f, Outer - Inner), mFloor);
            FloorSlab(g, new Vector3(-(Inner + Outer) * 0.5f, 0, 0), new Vector2(Outer - Inner, Inner * 2f), mFloor);
            FloorSlab(g, new Vector3((Inner + Outer) * 0.5f, 0, 0), new Vector2(Outer - Inner, Inner * 2f), mFloor);

            // Lagoon water and the stone ledge around it.
            var water = Kit.Mesh(g, "Lagoon", MeshGen.FloorQuad(Inner * 2f, Inner * 2f, true), waterMat, new Vector3(0, -0.75f, 0), Vector3.one);
            water.shadowCastingMode = ShadowCastingMode.Off;
            Kit.Block(g, "LagoonBed", new Vector3(0, -2.2f, 0), new Vector3(Inner * 2f, 0.4f, Inner * 2f), mFloorDark);
            Kit.Block(g, "LedgeS", new Vector3(0, -0.5f, -Inner + 0.25f), new Vector3(Inner * 2f, 1f, 0.5f), mWall);
            Kit.Block(g, "LedgeN", new Vector3(0, -0.5f, Inner - 0.25f), new Vector3(Inner * 2f, 1f, 0.5f), mWall);
            Kit.Block(g, "LedgeW", new Vector3(-Inner + 0.25f, -0.5f, 0), new Vector3(0.5f, 1f, Inner * 2f), mWall);
            Kit.Block(g, "LedgeE", new Vector3(Inner - 0.25f, -0.5f, 0), new Vector3(0.5f, 1f, Inner * 2f), mWall);

            // Al Wasl plaza: a stone disc rising out of the lagoon.
            var plaza = Kit.Mesh(g, "WaslPlaza", MeshGen.Cylinder(24f, 24f, 1.4f, 72), mFloor, new Vector3(0, -1.4f, 0), Vector3.one);
            var mc = plaza.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = plaza.GetComponent<MeshFilter>().sharedMesh;
            plaza.gameObject.isStatic = true;
            Kit.Mesh(g, "PlazaRim", MeshGen.Torus(24f, 0.08f, 96, 5), ExpoMaterials.Emissive(ExpoPalette.Cyan, 2.2f), new Vector3(0, 0.02f, 0), Vector3.one);
            foreach (float r in new[] { 17.2f, 20.5f })
                Kit.Mesh(g, "PlazaRing", MeshGen.Annulus(r, r + 0.12f, 96), ExpoMaterials.Emissive(new Color(0.6f, 0.75f, 1f), 1.1f), new Vector3(0, 0.012f, 0), Vector3.one);
            // Three coloured convergence lines from the plaza edge to the dome.
            for (int i = 0; i < 3; i++)
            {
                float ang = i == 0 ? 180f : (i == 1 ? 90f : 0f);
                Vector3 dir = Quaternion.Euler(0, -ang + 90f, 0) * Vector3.forward;
                Vector3 mid = dir * 19.5f;
                Kit.Mesh(g, "ConvergenceLine", MeshGen.Box(new Vector3(0.18f, 0.02f, 8.5f)), ExpoMaterials.Emissive(ExpoPalette.ForPassport((PassportType)i), 2.4f),
                    mid + Vector3.up * 0.01f, Vector3.one, Quaternion.LookRotation(dir));
            }

            // Bridge from the arrival concourse to the plaza.
            Kit.Block(g, "Bridge", new Vector3(0, -0.25f, -31.75f), new Vector3(6f, 0.5f, 17.5f), mFloor);
            Kit.Block(g, "BridgeUnder", new Vector3(0, -0.9f, -31.75f), new Vector3(5f, 0.8f, 17.5f), mDarkMetal, 0f, false);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.6f + i * 0.6f;
                Kit.Mesh(g, "BridgeLine", MeshGen.Box(new Vector3(0.08f, 0.02f, 17f)), ExpoMaterials.Emissive(ExpoPalette.ForPassport((PassportType)i), 2.6f), new Vector3(x, 0.011f, -31.75f), Vector3.one);
            }
            Railing(g, new Vector3(-3f, 0, -40f), new Vector3(-3f, 0, -23.2f), ExpoPalette.Cyan);
            Railing(g, new Vector3(3f, 0, -40f), new Vector3(3f, 0, -23.2f), ExpoPalette.Cyan);
        }

        void FloorSlab(Transform parent, Vector3 center, Vector2 size, Material mat)
        {
            var top = Kit.Mesh(parent, "Floor", MeshGen.FloorQuad(size.x, size.y, true), mat, center, Vector3.one);
            top.gameObject.isStatic = true;
            var col = new GameObject("FloorCollider");
            col.transform.SetParent(parent, false);
            col.transform.localPosition = center + Vector3.down * 0.25f;
            col.AddComponent<BoxCollider>().size = new Vector3(size.x, 0.5f, size.y);
        }

        // ================================================================ boundaries

        void BuildBoundaries()
        {
            var g = Kit.Group(staticRoot, "Boundaries", Vector3.zero);
            // Lagoon railings (the south edge leaves the bridge opening).
            Railing(g, new Vector3(-Inner, 0, -Inner), new Vector3(-Inner, 0, Inner), ExpoPalette.Gold);
            Railing(g, new Vector3(-Inner, 0, Inner), new Vector3(Inner, 0, Inner), ExpoPalette.Blue);
            Railing(g, new Vector3(Inner, 0, Inner), new Vector3(Inner, 0, -Inner), ExpoPalette.Emerald);
            Railing(g, new Vector3(Inner, 0, -Inner), new Vector3(3f, 0, -Inner), ExpoPalette.Cyan);
            Railing(g, new Vector3(-3f, 0, -Inner), new Vector3(-Inner, 0, -Inner), ExpoPalette.Cyan);

            // Invisible containment just inside the outer facades.
            InvisibleWall(g, new Vector3(0, 6, -Outer + 0.1f), new Vector3(Outer * 2f, 12f, 0.4f));
            InvisibleWall(g, new Vector3(0, 6, Outer - 0.1f), new Vector3(Outer * 2f, 12f, 0.4f));
            InvisibleWall(g, new Vector3(-Outer + 0.1f, 6, 0), new Vector3(0.4f, 12f, Outer * 2f));
            InvisibleWall(g, new Vector3(Outer - 0.1f, 6, 0), new Vector3(0.4f, 12f, Outer * 2f));
        }

        /// <summary>Glass lagoon railing with a glowing handrail; its collider blocks only the courier.</summary>
        void Railing(Transform parent, Vector3 a, Vector3 b, Color accent)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Vector3 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(d.normalized);
            var glass = Kit.Mesh(parent, "RailGlass", MeshGen.Box(new Vector3(0.05f, 1.05f, len)), mGlass, mid + Vector3.up * 0.58f, Vector3.one, rot);
            glass.shadowCastingMode = ShadowCastingMode.Off;
            Kit.Mesh(parent, "RailTop", MeshGen.Box(new Vector3(0.09f, 0.05f, len)), ExpoMaterials.Emissive(Color.Lerp(accent, Color.white, 0.55f), 1.8f), mid + Vector3.up * 1.13f, Vector3.one, rot);
            Kit.Mesh(parent, "RailBase", MeshGen.Box(new Vector3(0.16f, 0.08f, len)), mDarkMetal, mid + Vector3.up * 0.04f, Vector3.one, rot);
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2.5f));
            var postMesh = MeshGen.Box(new Vector3(0.07f, 1.1f, 0.07f));
            for (int i = 0; i <= posts; i++)
                Kit.Mesh(parent, "RailPost", postMesh, mMetal, Vector3.Lerp(a, b, (float)i / posts) + Vector3.up * 0.55f, Vector3.one, rot);
            InvisibleWall(parent, mid + Vector3.up * 1.6f, new Vector3(0.3f, 3.2f, len), rot);
        }

        static void InvisibleWall(Transform parent, Vector3 center, Vector3 size, Quaternion? rot = null)
        {
            var go = new GameObject("Containment") { layer = ExpoLayers.Sensor };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ================================================================ shared props

        /// <summary>Blocking wall across (or along) a corridor in frame space, from x0 to x1 at depth z.</summary>
        void CrossWall(Frame f, float x0, float x1, float z, float height, Material mat, Color trim)
        {
            if (x1 - x0 < 0.05f) return;
            float w = x1 - x0;
            Kit.Block(f.s, "CrossWall", new Vector3((x0 + x1) * 0.5f, height * 0.5f, z), new Vector3(w, height, 0.8f), mat);
            Kit.Mesh(f.s, "CrossWallTrim", MeshGen.Box(new Vector3(w, 0.06f, 0.82f)), ExpoMaterials.Emissive(trim, 2f), new Vector3((x0 + x1) * 0.5f, height - 0.35f, z), Vector3.one);
            Kit.Mesh(f.s, "CrossWallCap", MeshGen.Box(new Vector3(w + 0.1f, 0.12f, 0.95f)), mDarkMetal, new Vector3((x0 + x1) * 0.5f, height + 0.06f, z), Vector3.one);
        }

        void LampPost(Transform parent, Vector3 pos, Color color, int style)
        {
            var g = Kit.Group(parent, "Lamp", pos);
            if (style == 1)
            {
                // Parisian: slim black post with a lantern.
                Kit.Mesh(g, "Post", MeshGen.Cylinder(0.09f, 0.06f, 4.2f, 8), mDarkMetal, Vector3.zero, Vector3.one);
                Kit.Mesh(g, "Arm", MeshGen.Box(new Vector3(0.05f, 0.05f, 0.8f)), mDarkMetal, new Vector3(0, 4.1f, -0.35f), Vector3.one);
                Kit.Mesh(g, "Lantern", MeshGen.TaperedBox(new Vector2(0.28f, 0.28f), new Vector2(0.4f, 0.4f), 0.45f), ExpoMaterials.Emissive(color, 3f), new Vector3(0, 3.6f, -0.7f), Vector3.one);
                Kit.Mesh(g, "Cap", MeshGen.TaperedBox(new Vector2(0.48f, 0.48f), new Vector2(0.05f, 0.05f), 0.25f), mDarkMetal, new Vector3(0, 4.05f, -0.7f), Vector3.one);
                Kit.PointLight(g, new Vector3(0, 3.6f, -0.7f), color, 9f, 1.5f);
            }
            else if (style == 2)
            {
                // UK: Victorian twin-ring post.
                Kit.Mesh(g, "Post", MeshGen.Cylinder(0.11f, 0.07f, 4.4f, 8), mDarkMetal, Vector3.zero, Vector3.one);
                Kit.Mesh(g, "Ring", MeshGen.Torus(0.32f, 0.03f, 20, 5), ExpoMaterials.Emissive(color, 3f), new Vector3(0, 4.3f, 0), Vector3.one, Quaternion.Euler(90, 0, 0));
                Kit.Mesh(g, "Orb", MeshGen.Sphere(0.16f, 12, 8), ExpoMaterials.Emissive(Color.Lerp(color, Color.white, 0.4f), 3.5f), new Vector3(0, 4.3f, 0), Vector3.one);
                Kit.PointLight(g, new Vector3(0, 4.1f, 0), color, 9f, 1.4f);
            }
            else
            {
                // Expo modern: tall blade with a light bar.
                Kit.Mesh(g, "Blade", MeshGen.Box(new Vector3(0.12f, 5f, 0.4f)), mMetal, new Vector3(0, 2.5f, 0), Vector3.one);
                Kit.Mesh(g, "Bar", MeshGen.Box(new Vector3(0.14f, 1.4f, 0.1f)), ExpoMaterials.Emissive(color, 3.2f), new Vector3(0, 4.1f, -0.2f), Vector3.one);
                Kit.PointLight(g, new Vector3(0, 4.2f, -0.6f), color, 9.5f, 1.5f);
            }
        }

        void Planter(Transform parent, Vector3 pos, Vector3 size, Material body, Color trim, bool palm, bool collider = true)
        {
            var g = Kit.Group(parent, "Planter", pos);
            Kit.Block(g, "Box", new Vector3(0, size.y * 0.5f, 0), size, body, 0f, collider);
            Kit.Mesh(g, "Soil", MeshGen.Box(new Vector3(size.x - 0.2f, 0.05f, size.z - 0.2f)), mHedge, new Vector3(0, size.y + 0.01f, 0), Vector3.one);
            Kit.Mesh(g, "Trim", MeshGen.Box(new Vector3(size.x + 0.02f, 0.05f, size.z + 0.02f)), ExpoMaterials.Emissive(trim, 1.6f), new Vector3(0, size.y - 0.12f, 0), Vector3.one);
            if (palm) Palm(g, new Vector3(0, size.y, 0), 5.5f + Mathf.Abs(pos.x * 7.13f + pos.z * 3.7f) % 2f);
            else
            {
                Kit.Mesh(g, "Shrub", MeshGen.Sphere(Mathf.Min(size.x, size.z) * 0.38f, 10, 7), mHedge, new Vector3(0, size.y + 0.25f, 0), new Vector3(1f, 0.7f, 1f));
            }
        }

        void Palm(Transform parent, Vector3 basePos, float height)
        {
            var g = Kit.Group(parent, "Palm", basePos);
            int segments = 6;
            Vector3 p = Vector3.zero;
            float lean = 6f;
            for (int i = 0; i < segments; i++)
            {
                float h = height / segments;
                var seg = Kit.Mesh(g, "Trunk", MeshGen.Cylinder(0.2f - i * 0.018f, 0.18f - i * 0.018f, h * 1.02f, 7), mTrunk, p, Vector3.one, Quaternion.Euler(lean * i * 0.4f, 0, 0));
                p += seg.transform.localRotation * Vector3.up * h;
            }
            var crown = Kit.Group(g, "Crown", p);
            for (int i = 0; i < 9; i++)
            {
                float yaw = i * 40f + 7f;
                var leaf = Kit.Mesh(crown, "Frond", MeshGen.TaperedBox(new Vector2(0.5f, 0.06f), new Vector2(0.06f, 0.02f), 2.6f), mLeaf,
                    Vector3.zero, Vector3.one, Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, -62f - (i % 3) * 9f));
                leaf.shadowCastingMode = ShadowCastingMode.On;
            }
            Kit.Mesh(crown, "Dates", MeshGen.Sphere(0.22f, 8, 6), ExpoMaterials.Emissive(ExpoPalette.Gold, 0.6f), Vector3.down * 0.15f, Vector3.one);
        }

        void Bench(Transform parent, Vector3 pos, float yaw, Color accent)
        {
            var g = Kit.Group(parent, "Bench", pos, yaw);
            Kit.Block(g, "Seat", new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.12f, 0.6f), mWhite);
            Kit.Block(g, "LegL", new Vector3(-0.85f, 0.2f, 0), new Vector3(0.12f, 0.4f, 0.5f), mDarkMetal, 0f, false);
            Kit.Block(g, "LegR", new Vector3(0.85f, 0.2f, 0), new Vector3(0.12f, 0.4f, 0.5f), mDarkMetal, 0f, false);
            Kit.Mesh(g, "Glow", MeshGen.Box(new Vector3(2.0f, 0.03f, 0.04f)), ExpoMaterials.Emissive(accent, 2f), new Vector3(0, 0.36f, 0.28f), Vector3.one);
        }

        void Kiosk(Transform parent, Vector3 pos, float yaw, Color accent, string caption)
        {
            var g = Kit.Group(parent, "Kiosk", pos, yaw);
            Kit.Block(g, "Body", new Vector3(0, 1.1f, 0), new Vector3(2.4f, 2.2f, 1.6f), mWall);
            Kit.Mesh(g, "Counter", MeshGen.Box(new Vector3(2.6f, 0.1f, 1.8f)), mMetal, new Vector3(0, 1.0f, 0), Vector3.one);
            Kit.Mesh(g, "Roof", MeshGen.Box(new Vector3(3.0f, 0.14f, 2.2f)), mDarkMetal, new Vector3(0, 2.27f, 0), Vector3.one);
            Kit.Mesh(g, "Sign", MeshGen.Box(new Vector3(2.2f, 0.4f, 0.04f)), ExpoMaterials.Emissive(accent, 2.2f), new Vector3(0, 1.75f, -0.82f), Vector3.one);
            var t = Kit.Text(g, caption, new Vector3(0, 2.75f, 0), 0.2f, accent);
            t.gameObject.AddComponent<FaceCamera>();
        }

        /// <summary>Floating holographic sign that turns to face the camera.</summary>
        void HoloSign(Transform parent, Vector3 pos, string title, string subtitle, Color color, float size = 0.5f)
        {
            var g = Kit.Group(parent, "HoloSign", pos);
            var t = Kit.Text(g, title, Vector3.zero, size, color);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = Kit.Text(g, subtitle, new Vector3(0, -size * 1.4f, 0), size * 0.45f, Color.Lerp(color, Color.white, 0.5f));
                s.transform.SetParent(t.transform, true);
            }
            Kit.Mesh(g, "Underline", MeshGen.Box(new Vector3(size * 9f, 0.025f, 0.025f)), ExpoMaterials.Emissive(color, 2.5f), new Vector3(0, -size * 0.75f, 0), Vector3.one).transform.SetParent(t.transform, true);
            t.gameObject.AddComponent<FaceCamera>().flat = true;
            g.gameObject.AddComponent<Bob>().Init(0.12f, 0.8f);
        }

        /// <summary>A strip of emissive guide light set into the floor.</summary>
        void GuideStrip(Transform parent, Vector3 a, Vector3 b, Color color, float width = 0.14f)
        {
            Vector3 d = b - a;
            Kit.Mesh(parent, "Guide", MeshGen.Box(new Vector3(width, 0.02f, d.magnitude)), ExpoMaterials.Emissive(color, 2.2f),
                (a + b) * 0.5f + Vector3.up * 0.011f, Vector3.one, Quaternion.LookRotation(d.normalized));
        }

        // ================================================================ checkpoints & progress

        void RegisterCheckpoints()
        {
            var cp = Game.Checkpoints;
            if (cp == null) return;
            cp.SetStart(PlayerStart, PlayerStartYaw);
            cp.AddZone(1, "WEST CONCOURSE", new Bounds(new Vector3(-30, 1, -49), new Vector3(4, 4, 18)), new Vector3(-30, 0, -49), 270f, 0);
            cp.AddZone(2, "UAE DISTRICT", new Bounds(new Vector3(-49, 1, -36), new Vector3(18, 4, 6)), new Vector3(-49, 0, -37), 0f, 1);
            cp.AddZone(3, "UAE COURTYARD", new Bounds(new Vector3(-49, 1, -25.5f), new Vector3(18, 4, 4)), new Vector3(-49, 0, -25.5f), 0f, 1);
            cp.AddZone(4, "WIND TOWER WALK", new Bounds(new Vector3(-49, 1, 5.5f), new Vector3(18, 4, 4)), new Vector3(-45.5f, 0, 5.5f), 0f, 1);
            cp.AddZone(5, "BURJ PLAZA", new Bounds(new Vector3(-49, 1, 44), new Vector3(18, 4, 8)), new Vector3(-49, 0, 43), 0f, 1);
            cp.AddZone(6, "FRANCE ARCADE", new Bounds(new Vector3(-36, 1, 49), new Vector3(6, 4, 18)), new Vector3(-37, 0, 49), 90f, 2);
            cp.AddZone(7, "FRENCH CONSULATE", new Bounds(new Vector3(3.5f, 1, 49), new Vector3(4, 4, 18)), new Vector3(3.5f, 0, 49), 90f, 2);
            cp.AddZone(8, "EIFFEL PLAZA", new Bounds(new Vector3(44, 1, 49), new Vector3(8, 4, 18)), new Vector3(43, 0, 49), 90f, 2);
            cp.AddZone(9, "UK DISTRICT", new Bounds(new Vector3(49, 1, 36), new Vector3(18, 4, 6)), new Vector3(49, 0, 37), 180f, 3);
            cp.AddZone(10, "CLOCK PLAZA", new Bounds(new Vector3(49, 1, 17), new Vector3(18, 4, 4)), new Vector3(49, 0, 17), 180f, 3);
            cp.AddZone(11, "TEMPO GATES", new Bounds(new Vector3(49, 1, -18), new Vector3(18, 4, 4)), new Vector3(49, 0, -18), 180f, 3);
            cp.AddZone(12, "BIG BEN PLAZA", new Bounds(new Vector3(49, 1, -44), new Vector3(18, 4, 8)), new Vector3(49, 0, -43), 180f, 3);
            cp.AddZone(13, "CONVERGENCE WALK", new Bounds(new Vector3(37.5f, 1, -49), new Vector3(3, 4, 18)), new Vector3(37.5f, 0, -49), 270f, 4);
            cp.AddZone(14, "AL WASL PLAZA", new Bounds(new Vector3(0, 1, -27), new Vector3(6, 4, 6)), new Vector3(0, 0, -27), 0f, 5);
        }

        /// <summary>Called by <see cref="SectorProgressionManager"/> after each landmark is restored.</summary>
        public void OnLandmarkRestored(int index)
        {
            dome?.SetSectorRestored(index);
            if (Game.Progress != null && Game.Progress.AllRestored)
            {
                doorWalk?.Open();
                dome?.OpenSeal();
            }
        }
    }

    /// <summary>Turns a transform to face the active camera (holographic signage).</summary>
    public class FaceCamera : MonoBehaviour
    {
        public bool flat = true;
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            if (flat) d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }

    /// <summary>Gentle vertical bobbing.</summary>
    public class Bob : MonoBehaviour
    {
        float amplitude, speed, phase;
        Vector3 origin;

        public void Init(float amp, float spd)
        {
            amplitude = amp;
            speed = spd;
        }

        // Captured in Start so re-parenting during world assembly does not shift the rest position.
        void Start()
        {
            origin = transform.localPosition;
            phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % 6.28f;
        }

        void Update() => transform.localPosition = origin + Vector3.up * Mathf.Sin(Time.time * speed + phase) * amplitude;
    }

    /// <summary>Scrolls the lagoon's normal map so the water ripples.</summary>
    public class WaterAnimator : MonoBehaviour
    {
        public Material material;
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        void Update()
        {
            if (material == null) return;
            material.SetTextureOffset(BaseMap, new Vector2(Time.time * 0.006f, Time.time * 0.0035f));
        }
    }

    /// <summary>Constant spin around a local axis.</summary>
    public class Spin : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 30f;
        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
