using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExpoConvergence
{
    public partial class ExpoWorld
    {
        SensorProfile thermalDrone, opticalDrone, opticalCam, thermalScanner, opticalScanner;

        void LoadProfiles()
        {
            // Tuned for a short judged demo: readable, fair, forgiving.
            thermalDrone = Load("Sensor_ThermalDrone") ?? SensorProfile.Create(SensorKind.Thermal, 9.5f, 46f, 0.8f, 4.5f, 2.5f, 5.4f);
            opticalDrone = Load("Sensor_OpticalDrone") ?? SensorProfile.Create(SensorKind.Optical, 9.5f, 46f, 0.8f, 4.5f, 2.6f, 5.6f);
            opticalCam = Load("Sensor_OpticalCamera") ?? SensorProfile.Create(SensorKind.Optical, 12f, 44f, 0.95f, 0f);
            thermalScanner = Load("Sensor_ThermalScanner") ?? SensorProfile.Create(SensorKind.Thermal, 10f, 20f, 1.3f, 0f);
            opticalScanner = Load("Sensor_OpticalScanner") ?? SensorProfile.Create(SensorKind.Optical, 10f, 20f, 1.3f, 0f);
        }

        static SensorProfile Load(string name) => Resources.Load<SensorProfile>("ExpoData/" + name);

        // ================================================================ device helpers

        SentryAI Drone(Frame f, SensorProfile p, params Vector2[] localPath)
        {
            var go = new GameObject("FalconSentry");
            go.transform.SetParent(f.d, false);
            var world = new Vector3[localPath.Length];
            for (int i = 0; i < localPath.Length; i++) world[i] = f.P(localPath[i].x, 0f, localPath[i].y);
            var s = go.AddComponent<SentryAI>();
            s.Configure(p, world, 3.3f);
            return s;
        }

        SecurityCamera CameraOnPost(Frame f, Vector3 local, float localYaw, float sweep, float speed, bool wallMounted)
        {
            if (!wallMounted)
            {
                Kit.Block(f.s, "CamPost", new Vector3(local.x, local.y * 0.5f, local.z), new Vector3(0.35f, local.y, 0.35f), mDarkMetal);
                Kit.Mesh(f.s, "CamPostLight", MeshGen.Box(new Vector3(0.05f, local.y * 0.8f, 0.36f)), ExpoMaterials.Emissive(ExpoPalette.Cyan, 1.6f), new Vector3(local.x, local.y * 0.45f, local.z), Vector3.one);
            }
            var go = new GameObject("SecurityCamera");
            go.transform.SetParent(f.d, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0, localYaw, 0);
            var c = go.AddComponent<SecurityCamera>();
            c.Configure(opticalCam, sweep, speed, 24f);
            return c;
        }

        ScannerTurret Scanner(Frame f, Vector3 local, SensorProfile p, float speed, float startYaw, bool swing = false, float arc = 150f)
        {
            var go = new GameObject("PrismScanner");
            go.transform.SetParent(f.d, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.2f, 0);
            col.size = new Vector3(0.7f, 2.4f, 0.7f);
            var s = go.AddComponent<ScannerTurret>();
            s.Configure(p, speed, startYaw, swing, arc);
            return s;
        }

        LaserBarrier Laser(Frame f, Vector3 local, LaserBarrier.Pattern pattern, float width, float[] heights, Action<LaserBarrier> setup = null)
        {
            var go = new GameObject("LaserGrid");
            go.transform.SetParent(f.d, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var l = go.AddComponent<LaserBarrier>();
            l.pattern = pattern;
            l.width = width;
            l.heights = heights;
            setup?.Invoke(l);
            l.Build();
            return l;
        }

        ThermalTurnstile Turnstile(Frame f, Vector3 local, float width)
        {
            var go = new GameObject("ThermalTurnstile");
            go.transform.SetParent(f.d, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var t = go.AddComponent<ThermalTurnstile>();
            t.width = width;
            t.Build();
            return t;
        }

        TempoGate Tempo(Frame f, Vector3 local, float width, float cycle, float open, float phase)
        {
            var go = new GameObject("TempoGate");
            go.transform.SetParent(f.d, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var t = go.AddComponent<TempoGate>();
            t.width = width; t.cycle = cycle; t.openFraction = open; t.phaseOffset = phase;
            t.Build();
            return t;
        }

        ConsularGateTerminal Terminal(Vector3 worldPos, Vector3 faceToward, PassportType required, bool any, string title, string action, float hold, Action onDone)
        {
            var go = new GameObject("Terminal_" + title);
            go.transform.SetParent(dynamicRoot, false);
            go.transform.position = worldPos;
            Vector3 away = worldPos - faceToward; away.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward);
            var t = go.AddComponent<ConsularGateTerminal>();
            t.required = required;
            t.anyPassport = any;
            t.action = action;
            t.holdTime = hold;
            t.onComplete = onDone;
            t.Build(title);
            return t;
        }

        /// <summary>A sliding door across the corridor at depth z, with walls filling the rest of the width.</summary>
        SciFiDoor Door(Frame f, float z, Color color, string caption, Material wallMat)
        {
            CrossWall(f, -Half, -3.7f, z, 6f, wallMat, color);
            CrossWall(f, 3.7f, Half, z, 6f, wallMat, color);
            var go = new GameObject("Door_" + caption);
            go.transform.SetParent(f.d, false);
            go.transform.localPosition = new Vector3(0, 0, z);
            go.transform.localRotation = Quaternion.identity;
            var d = go.AddComponent<SciFiDoor>();
            d.width = 6f;
            d.height = 4.6f;
            d.color = color;
            d.Build(caption);
            return d;
        }

        void Hint(Frame f, float z0, float z1, string text, float seconds = 6f)
        {
            var go = new GameObject("HintZone");
            go.transform.SetParent(f.d, false);
            var h = go.AddComponent<HintZone>();
            Vector3 a = f.P(-Half, 0, z0), b = f.P(Half, 4, z1);
            var bounds = new Bounds((a + b) * 0.5f, Vector3.zero);
            bounds.Encapsulate(a); bounds.Encapsulate(b);
            h.Init(bounds, text, seconds);
        }

        void InnerLamps(Frame f, Color color, int style, float z0 = 5f, float z1 = 76f, float step = 10f)
        {
            for (float z = z0; z <= z1; z += step) LampPost(f.s, new Vector3(Half - 1.0f, 0, z), color, style);
        }

        // ================================================================ arrival & tutorial

        void BuildStartPlaza()
        {
            LoadProfiles();
            PlayerStart = new Vector3(0f, 0f, -52f);
            PlayerStartYaw = 0f;
            var g = Kit.Group(staticRoot, "ArrivalConcourse", Vector3.zero);

            ExpoFacade(g, -Outer, Outer);

            // Arrival portal behind the courier.
            var portalMat = ExpoMaterials.Emissive(ExpoPalette.Cyan, 2.4f);
            for (int s = -1; s <= 1; s += 2)
            {
                Kit.Block(g, "Pylon", new Vector3(7.5f * s, 6f, -56.5f), new Vector3(2f, 12f, 2f), mWhite);
                Kit.Mesh(g, "PylonSlit", MeshGen.Box(new Vector3(0.12f, 10.5f, 0.06f)), portalMat, new Vector3(7.5f * s - 1.01f * s, 6.2f, -56.5f), Vector3.one);
            }
            Kit.Block(g, "Lintel", new Vector3(0, 12.4f, -56.5f), new Vector3(17f, 1.2f, 2.2f), mWhite);
            Kit.Mesh(g, "LintelGlow", MeshGen.Box(new Vector3(15f, 0.08f, 2.22f)), portalMat, new Vector3(0, 11.75f, -56.5f), Vector3.one);
            // Boards between the two faces' captions, so neither shows mirrored through the other.
            Kit.Block(g, "SignBoard", new Vector3(0, 13.75f, -56.5f), new Vector3(13.6f, 1.5f, 2.2f), mDarkMetal);
            Kit.Block(g, "BannerBoard", new Vector3(0, 10.7f, -56.5f), new Vector3(9.8f, 0.62f, 2.2f), mDarkMetal);
            // Text reads from its -Z side, so signs seen from the north are turned around.
            Kit.Text(g, "EXPO CITY DUBAI", new Vector3(0, 13.6f, -55.3f), 0.9f, Color.white).transform.localRotation = Quaternion.Euler(0, 180f, 0);
            Kit.Text(g, "OPENING NIGHT  ·  FESTIVAL OF PASSPORTS", new Vector3(0, 10.7f, -55.3f), 0.32f, ExpoPalette.Cyan).transform.localRotation = Quaternion.Euler(0, 180f, 0);
            // Same sign on the outer face, seen during the intro flyover.
            Kit.Text(g, "EXPO CITY DUBAI", new Vector3(0, 13.6f, -57.7f), 0.9f, Color.white);
            Kit.Text(g, "OPENING NIGHT  ·  FESTIVAL OF PASSPORTS", new Vector3(0, 10.7f, -57.7f), 0.32f, ExpoPalette.Cyan);

            // Canopy over the arrival point.
            Kit.Mesh(g, "Canopy", MeshGen.Box(new Vector3(14f, 0.25f, 7f)), mWhite, new Vector3(0, 7.5f, -53f), Vector3.one);
            Kit.Mesh(g, "CanopyGlow", MeshGen.Box(new Vector3(13.6f, 0.04f, 6.6f)), ExpoMaterials.Emissive(new Color(0.7f, 0.85f, 1f), 0.9f), new Vector3(0, 7.36f, -53f), Vector3.one);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Kit.Block(g, "CanopyColumn", new Vector3(6.2f * sx, 3.75f, -53f + 3f * sz), new Vector3(0.25f, 7.5f, 0.25f), mMetal);
            Kit.Mesh(g, "SpawnRing", MeshGen.Annulus(2.6f, 2.75f, 64), ExpoMaterials.Emissive(ExpoPalette.Cyan, 2.5f), new Vector3(0, 0.012f, -52f), Vector3.one);
            Kit.Mesh(g, "SpawnRing2", MeshGen.Annulus(1.4f, 1.48f, 48), ExpoMaterials.Emissive(ExpoPalette.Gold, 2.5f), new Vector3(0, 0.012f, -52f), Vector3.one);

            Bench(g, new Vector3(-8f, 0, -45.5f), 90f, ExpoPalette.Cyan);
            Bench(g, new Vector3(8f, 0, -45.5f), -90f, ExpoPalette.Cyan);
            Planter(g, new Vector3(-9.5f, 0, -50f), new Vector3(2f, 1.1f, 4f), mWhite, ExpoPalette.Cyan, true);
            Planter(g, new Vector3(9.5f, 0, -50f), new Vector3(2f, 1.1f, 4f), mWhite, ExpoPalette.Cyan, true);
            LampPost(g, new Vector3(-11f, 0, -41.5f), ExpoPalette.Cyan, 0);
            LampPost(g, new Vector3(11f, 0, -41.5f), ExpoPalette.Cyan, 0);
            GuideStrip(g, new Vector3(-1.5f, 0, -49f), new Vector3(-12f, 0, -49f), ExpoPalette.Gold, 0.2f);
        }

        void ExpoFacade(Transform g, float x0, float x1)
        {
            // Modern pavilion wall along the south boundary.
            float z = -Outer - 1.25f;
            Kit.Block(g, "SouthWall", new Vector3((x0 + x1) * 0.5f, 4.5f, z), new Vector3(x1 - x0, 9f, 2.5f), mWall);
            var slit = ExpoMaterials.Emissive(ExpoPalette.Cyan, 1.7f);
            var slitMesh = MeshGen.Box(new Vector3(0.1f, 6.5f, 0.05f));
            for (float x = x0 + 2f; x < x1; x += 4f)
            {
                if (Mathf.Abs(x) < 9f) continue;
                Kit.Mesh(g, "Slit", slitMesh, slit, new Vector3(x, 4f, -Outer + 0.02f), Vector3.one);
            }
            Kit.Mesh(g, "Cornice", MeshGen.Box(new Vector3(x1 - x0, 0.4f, 3f)), mWhite, new Vector3((x0 + x1) * 0.5f, 9.2f, z + 0.2f), Vector3.one);
            foreach (float sx in new[] { -26f, 26f })
            {
                Kit.Mesh(g, "Screen", MeshGen.Box(new Vector3(10f, 4.5f, 0.2f)), mDarkMetal, new Vector3(sx, 5f, -Outer + 0.1f), Vector3.one);
                Kit.Mesh(g, "ScreenGlow", MeshGen.Box(new Vector3(9.6f, 0.06f, 0.22f)), slit, new Vector3(sx, 2.8f, -Outer + 0.1f), Vector3.one);
                Kit.Text(g, sx < 0 ? "FESTIVAL OF PASSPORTS" : "CONNECTING MINDS", new Vector3(sx, 5.6f, -Outer + 0.25f), 0.42f, Color.white)
                    .transform.localRotation = Quaternion.Euler(0, 180f, 0);
                Kit.Text(g, sx < 0 ? "UAE  ·  FRANCE  ·  UNITED KINGDOM" : "CREATING THE FUTURE", new Vector3(sx, 4.4f, -Outer + 0.25f), 0.24f, ExpoPalette.Cyan)
                    .transform.localRotation = Quaternion.Euler(0, 180f, 0);
            }
        }

        void BuildTutorialPath()
        {
            var f = MakeFrame("WestConcourse", new Vector3(-12f, 0f, -49f), 270f);
            InnerLamps(f, ExpoPalette.Cyan, 0, 4f, 26f, 8f);
            GuideStrip(f.s, new Vector3(3f, 0, 0f), new Vector3(3f, 0, 28f), ExpoPalette.Gold);
            Planter(f.s, new Vector3(-6.8f, 0, 4f), new Vector3(2.4f, 1.1f, 2.4f), mWhite, ExpoPalette.Cyan, true);
            Planter(f.s, new Vector3(-6.8f, 0, 22f), new Vector3(2.4f, 1.1f, 2.4f), mWhite, ExpoPalette.Cyan, true);
            Bench(f.s, new Vector3(-7.4f, 0, 9.5f), 90f, ExpoPalette.Cyan);

            // Low security shutter: slide underneath.
            float z = 14f;
            for (int s = -1; s <= 1; s += 2)
                Kit.Block(f.s, "ShutterPost", new Vector3(8.8f * s, 1.6f, z), new Vector3(0.4f, 3.2f, 0.5f), mDarkMetal);
            Kit.Block(f.s, "Shutter", new Vector3(0, 1.45f, z), new Vector3(17.2f, 0.75f, 0.35f), mMetal);
            var warn = ExpoMaterials.Emissive(ExpoPalette.Suspicious, 2.4f);
            for (float x = -8f; x <= 8f; x += 1.6f)
                Kit.Mesh(f.s, "Chevron", MeshGen.Box(new Vector3(0.5f, 0.1f, 0.37f)), warn, new Vector3(x, 1.2f, z), Vector3.one, Quaternion.Euler(0, 0, 35f));
            Kit.Text(f.s, "SECURITY SHUTTER  ·  SLIDE  [SPACE]", new Vector3(0, 2.25f, z - 0.25f), 0.26f, ExpoPalette.Suspicious);

            Hint(f, 0f, 6f, "Hold SHIFT to sprint  ·  watch your stamina", 5f);
            Hint(f, 8f, 13f, "Sprint, then press SPACE to slide under the shutter", 5f);
            HoloSign(dynamicRoot, f.P(0, 4.5f, 26f), "UAE CONSULAR GATE", "AUTHENTICATE TO ENTER THE DISTRICT", ExpoPalette.Gold, 0.4f);
        }

        // ================================================================ UAE

        void BuildUAEDistrict()
        {
            var f = MakeFrame("UAE_District", new Vector3(-49f, 0f, -40f), 0f);
            Color gold = ExpoPalette.Gold;

            UAEFacade(f, -18f, 98f);
            InnerLamps(f, new Color(1f, 0.78f, 0.45f), 0);
            GuideStrip(f.s, new Vector3(3f, 0, 1f), new Vector3(3f, 0, 79f), gold);
            doorUAE = Door(f, 0f, gold, "UAE PAVILION DISTRICT", mSand);

            // SW concourse: UAE consular gate terminal.
            RegisterObjective(0, Terminal(new Vector3(-44.5f, 0f, -43.5f), new Vector3(-49f, 0f, -49f), PassportType.UAE, false, "UAE CONSULAR GATE", "AUTHENTICATE", 1.1f, () =>
            {
                doorUAE.Open();
                GameEvents.RaiseToast("UAE DISTRICT OPEN  ·  THERMAL GRID ACTIVE", gold, 3f);
                GameEvents.RaiseHint("Thermal sentries can't see you while MIRAGE CLOAK is active  ·  RIGHT-CLICK", 7f);
            }));
            Planter(f.s, new Vector3(-6.5f, 0, -12f), new Vector3(2.4f, 1.2f, 2.4f), mSandDark, gold, true);
            Planter(f.s, new Vector3(6.2f, 0, -14f), new Vector3(2.4f, 1.2f, 2.4f), mSandDark, gold, true);

            // U1: first thermal turnstile.
            Planter(f.s, new Vector3(-6f, 0, 5f), new Vector3(2.6f, 1.25f, 1.6f), mSandDark, gold, true);
            Planter(f.s, new Vector3(6f, 0, 7.5f), new Vector3(2.6f, 1.25f, 1.6f), mSandDark, gold, true);
            CrossWall(f, -Half, -2.35f, 12f, 4.2f, mSand, gold);
            CrossWall(f, 2.35f, Half, 12f, 4.2f, mSand, gold);
            Turnstile(f, new Vector3(0, 0, 12f), 4f);
            Hint(f, 3f, 9f, "THERMAL TURNSTILE  ·  press RIGHT-CLICK for MIRAGE CLOAK and walk through", 6f);

            // U2: courtyard with two thermal falcons.
            Drone(f, thermalDrone, new Vector2(-6f, 17f), new Vector2(-1.2f, 17f), new Vector2(-1.2f, 37f), new Vector2(-6f, 37f));
            Drone(f, thermalDrone, new Vector2(6f, 37f), new Vector2(1.2f, 37f), new Vector2(1.2f, 17f), new Vector2(6f, 17f));
            Planter(f.s, new Vector3(-7f, 0, 21f), new Vector3(2.6f, 1.25f, 1.6f), mSandDark, gold, true);
            Planter(f.s, new Vector3(7f, 0, 25f), new Vector3(2.6f, 1.25f, 1.6f), mSandDark, gold, true);
            Dune(f, new Vector3(-3.5f, 0, 25.5f), 3.2f, 15f, 165f);
            Dune(f, new Vector3(3.5f, 0, 32.5f), 3.2f, 195f, 345f);
            MashrabiyaScreen(f, new Vector3(0f, 0, 29f), 0f);
            Kiosk(f.s, new Vector3(-6.2f, 0, 32f), 90f, gold, "DATES  ·  KARAK");
            Bench(f.s, new Vector3(7.4f, 0, 33f), -90f, gold);
            Hint(f, 13f, 17f, "Thermal falcons patrol the courtyard  ·  stay behind cover or cloak past their beams", 6f);

            // U3: thermal scanner covering the second turnstile.
            Scanner(f, new Vector3(-3.2f, 0, 37.5f), thermalScanner, 55f, 0f);
            CrossWall(f, -Half, 2.2f, 42f, 4.2f, mSand, gold);
            CrossWall(f, 6.8f, Half, 42f, 4.2f, mSand, gold);
            Turnstile(f, new Vector3(4.5f, 0, 42f), 4f);

            // U4: wind tower walk.
            WindTower(f, new Vector3(0f, 0, 58f));
            Drone(f, thermalDrone, new Vector2(-6f, 50f), new Vector2(6f, 55f), new Vector2(-6f, 63f), new Vector2(6f, 70f), new Vector2(-6f, 72f));
            Scanner(f, new Vector3(5f, 0, 74.5f), thermalScanner, 38f, 180f, true, 150f);
            Planter(f.s, new Vector3(-6.5f, 0, 52f), new Vector3(2.4f, 1.25f, 2.4f), mSandDark, gold, true);
            Planter(f.s, new Vector3(6.5f, 0, 62f), new Vector3(2.4f, 1.25f, 2.4f), mSandDark, gold, true);
            Planter(f.s, new Vector3(-5.5f, 0, 69.5f), new Vector3(2.4f, 1.25f, 2.4f), mSandDark, gold, true);
            Kiosk(f.s, new Vector3(6.2f, 0, 68f), -90f, gold, "PASSPORT STAMPS");
            HoloSign(dynamicRoot, f.P(0, 5f, 79f), "BURJ DISPLAY", "LANDMARK OFFLINE", gold, 0.42f);

            VfxFactory.AmbientMotes(f.P(0, 3f, 40f), new Vector3(18f, 5f, 80f), new Color(1f, 0.75f, 0.4f), 14f).transform.SetParent(dynamicRoot, true);
        }

        void UAEFacade(Frame f, float z0, float z1)
        {
            var gold = ExpoMaterials.Emissive(ExpoPalette.Gold, 2.6f);
            var goldSoft = ExpoMaterials.Emissive(new Color(1f, 0.7f, 0.35f), 1.2f);
            float len = z1 - z0, mid = (z0 + z1) * 0.5f;
            Kit.Block(f.s, "UAEWall", new Vector3(-Half - 1.25f, 3.75f, mid), new Vector3(2.5f, 7.5f, len), mSand);
            Kit.Mesh(f.s, "UAEPlinth", MeshGen.Box(new Vector3(0.3f, 0.8f, len)), mSandDark, new Vector3(-Half + 0.05f, 0.4f, mid), Vector3.one);
            Kit.Mesh(f.s, "UAEBand", MeshGen.Box(new Vector3(0.06f, 0.08f, len)), gold, new Vector3(-Half + 0.02f, 7.1f, mid), Vector3.one);
            for (float z = z0 + 4.5f; z < z1 - 2f; z += 9f)
            {
                // Wind tower (barjeel) with glowing vents.
                Kit.Block(f.s, "WindTower", new Vector3(-Half - 0.6f, 6.5f, z), new Vector3(3f, 13f, 3f), mSand);
                Kit.Mesh(f.s, "TowerCap", MeshGen.Box(new Vector3(3.4f, 0.35f, 3.4f)), mSandDark, new Vector3(-Half - 0.6f, 13.15f, z), Vector3.one);
                for (int k = -1; k <= 1; k++)
                    Kit.Mesh(f.s, "Vent", MeshGen.Box(new Vector3(0.06f, 3.2f, 0.22f)), gold, new Vector3(-Half + 0.92f, 10.5f, z + k * 0.75f), Vector3.one);
                Kit.Mesh(f.s, "Doorway", MeshGen.Box(new Vector3(0.1f, 2.6f, 1.4f)), mSandDark, new Vector3(-Half + 0.9f, 1.3f, z), Vector3.one);
                Kit.Mesh(f.s, "DoorTrim", MeshGen.Box(new Vector3(0.06f, 0.06f, 1.5f)), goldSoft, new Vector3(-Half + 0.95f, 2.65f, z), Vector3.one);
                // Mashrabiya screen between towers.
                var screen = Kit.Mesh(f.s, "Mashrabiya", MeshGen.Quad(5.2f, 4.2f), mMashrabiya, new Vector3(-Half + 0.03f, 1.6f, z + 4.5f), Vector3.one, Quaternion.Euler(0, -90f, 0));
                screen.shadowCastingMode = ShadowCastingMode.Off;
                Kit.Mesh(f.s, "ScreenFrame", MeshGen.Box(new Vector3(0.12f, 0.12f, 5.4f)), mSandDark, new Vector3(-Half + 0.06f, 5.85f, z + 4.5f), Vector3.one);
                if (Mathf.RoundToInt((z - z0) / 9f) % 2 == 0)
                    Kit.PointLight(f.s, new Vector3(-Half + 2f, 1.2f, z), new Color(1f, 0.65f, 0.3f), 7f, 1.3f);
            }
        }

        void Dune(Frame f, Vector3 local, float radius, float from, float to)
        {
            var mesh = MeshGen.ArcBeam(radius, 0.7f, 1.3f, from, to, 18);
            var mr = Kit.Mesh(f.s, "DuneWall", mesh, mSand, local + Vector3.up * 0.65f, Vector3.one, Quaternion.Euler(-90f, 0, 0));
            mr.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            mr.gameObject.isStatic = true;
            var cap = MeshGen.ArcBeam(radius, 0.74f, 0.05f, from, to, 18);
            Kit.Mesh(f.s, "DuneGlow", cap, ExpoMaterials.Emissive(ExpoPalette.Gold, 1.8f), local + Vector3.up * 1.32f, Vector3.one, Quaternion.Euler(-90f, 0, 0));
        }

        void MashrabiyaScreen(Frame f, Vector3 local, float yaw)
        {
            var g = Kit.Group(f.s, "ScreenPavilion", local, yaw);
            Kit.Block(g, "Frame", new Vector3(0, 1.6f, 0), new Vector3(3.4f, 3.2f, 0.35f), mSandDark);
            var front = Kit.Mesh(g, "PatternA", MeshGen.Quad(3f, 2.8f), mMashrabiya, new Vector3(0, 0.2f, -0.18f), Vector3.one);
            var back = Kit.Mesh(g, "PatternB", MeshGen.Quad(3f, 2.8f), mMashrabiya, new Vector3(0, 0.2f, 0.18f), Vector3.one, Quaternion.Euler(0, 180f, 0));
            front.shadowCastingMode = ShadowCastingMode.Off;
            back.shadowCastingMode = ShadowCastingMode.Off;
            Kit.PointLight(g, new Vector3(0, 1.5f, -0.8f), ExpoPalette.Gold, 5f, 1.1f);
        }

        void WindTower(Frame f, Vector3 local)
        {
            var g = Kit.Group(f.s, "WindTowerSculpture", local);
            Kit.Block(g, "Shaft", new Vector3(0, 3.5f, 0), new Vector3(2.6f, 7f, 2.6f), mSand);
            Kit.Mesh(g, "Crown", MeshGen.Box(new Vector3(3f, 0.4f, 3f)), mSandDark, new Vector3(0, 7.2f, 0), Vector3.one);
            var gold = ExpoMaterials.Emissive(ExpoPalette.Gold, 3f);
            for (int side = 0; side < 4; side++)
            {
                Quaternion r = Quaternion.Euler(0, side * 90f, 0);
                for (int k = -1; k <= 1; k++)
                    Kit.Mesh(g, "Vent", MeshGen.Box(new Vector3(0.2f, 2.4f, 0.06f)), gold, r * new Vector3(k * 0.6f, 5.4f, -1.32f), Vector3.one, r);
            }
            Kit.PointLight(g, new Vector3(0, 8f, 0), ExpoPalette.Gold, 9f, 1.6f);
        }

        // ================================================================ FRANCE

        void BuildFranceDistrict()
        {
            var f = MakeFrame("France_District", new Vector3(-40f, 0f, 49f), 90f);
            Color blue = ExpoPalette.Blue;
            Color warm = new Color(1f, 0.82f, 0.6f);

            FranceArcade(f, -18f, 98f);
            InnerLamps(f, warm, 1);
            GuideStrip(f.s, new Vector3(3f, 0, 1f), new Vector3(3f, 0, 79f), blue);
            doorFrance = Door(f, 0f, blue, "FRANCE PAVILION DISTRICT", mLime);

            // F1: two sweeping optical cameras over a narrow gap.
            CameraOnPost(f, new Vector3(-8.3f, 3.3f, 15f), 90f, 70f, 24f, false);
            CameraOnPost(f, new Vector3(8.3f, 3.3f, 19f), -90f, 70f, 24f, false);
            CrossWall(f, -Half, -2f, 22.5f, 3.8f, mLime, blue);
            CrossWall(f, 2f, Half, 22.5f, 3.8f, mLime, blue);
            Topiary(f, new Vector3(-6.5f, 0, 6f));
            Topiary(f, new Vector3(6.5f, 0, 9f));
            Bench(f.s, new Vector3(-7.4f, 0, 11f), 90f, blue);
            Hint(f, 2f, 8f, "Optical cameras see through the cloak  ·  press 2 for FRANCE, aim and RIGHT-CLICK to fire PRISM PULSE", 7f);

            // F2: consulate door, terminal guarded by a prism scanner.
            var innerDoor = Door(f, 40f, blue, "FRENCH CONSULATE", mLime);
            RegisterObjective(3, Terminal(f.P(5.6f, 0, 35.5f), f.P(5.6f, 0, 32f), PassportType.France, false, "CONSULATE LOCK", "UNLOCK", 1.6f, () =>
            {
                innerDoor.Open();
                GameEvents.RaiseToast("CONSULATE DOOR UNLOCKED", blue, 2.5f);
            }));
            Scanner(f, new Vector3(-1f, 0, 30.5f), opticalScanner, 50f, 90f);
            Kiosk(f.s, new Vector3(-6.4f, 0, 27f), 90f, blue, "CAFÉ LUMIÈRE");
            Topiary(f, new Vector3(2.5f, 0, 26f));
            Hint(f, 24f, 28f, "Freeze the prism scanner, then hold E at the consulate lock before it reboots", 6f);

            // F3: mixed grid — thermal falcon plus an optical camera.
            Drone(f, thermalDrone, new Vector2(-6f, 46f), new Vector2(6f, 46f), new Vector2(6f, 64f), new Vector2(-6f, 64f));
            CameraOnPost(f, new Vector3(-8.3f, 3.4f, 62f), 90f, 80f, 20f, false);
            Fountain(f, new Vector3(0f, 0, 55f));
            Topiary(f, new Vector3(-6f, 0, 50f));
            Topiary(f, new Vector3(6.2f, 0, 58f));
            Bench(f.s, new Vector3(7.4f, 0, 68f), -90f, blue);
            Hint(f, 42f, 46f, "Mixed grid  ·  cloak past thermal falcons [1], pulse optical cameras [2]", 6f);

            // Ceremonial arch before the Eiffel plaza.
            GrandArch(f, 76f, blue);
            HoloSign(dynamicRoot, f.P(0, 9.5f, 76f), "EIFFEL DISPLAY", "LANDMARK OFFLINE", blue, 0.42f);
            VfxFactory.AmbientMotes(f.P(0, 3f, 40f), new Vector3(18f, 5f, 80f), new Color(0.5f, 0.7f, 1f), 14f).transform.SetParent(dynamicRoot, true);
        }

        void FranceArcade(Frame f, float z0, float z1)
        {
            var blueGlow = ExpoMaterials.Emissive(new Color(0.18f, 0.36f, 0.85f), 0.38f);
            var blueNeon = ExpoMaterials.Emissive(ExpoPalette.Blue, 2.8f);
            var warmWindow = ExpoMaterials.Emissive(new Color(1f, 0.78f, 0.5f), 1.4f);
            float len = z1 - z0, mid = (z0 + z1) * 0.5f;
            Kit.Block(f.s, "FrWall", new Vector3(-Half - 1.5f, 5f, mid), new Vector3(2f, 10f, len), mLime);
            Kit.Mesh(f.s, "FrGlass", MeshGen.Box(new Vector3(0.05f, 4.2f, len)), blueGlow, new Vector3(-Half - 0.45f, 2.5f, mid), Vector3.one);
            Kit.Mesh(f.s, "Cornice", MeshGen.Box(new Vector3(1.6f, 0.5f, len)), mLime, new Vector3(-Half - 0.2f, 5.85f, mid), Vector3.one);
            Kit.Mesh(f.s, "CorniceNeon", MeshGen.Box(new Vector3(0.05f, 0.06f, len)), blueNeon, new Vector3(-Half + 0.62f, 5.55f, mid), Vector3.one);
            Kit.Mesh(f.s, "Mansard", MeshGen.TaperedBox(new Vector2(3f, len), new Vector2(1.2f, len), 3f), mSlate, new Vector3(-Half - 1.5f, 10f, mid), Vector3.one);
            var arch = MeshGen.ArcBeam(2.05f, 0.42f, 0.9f, 0f, 180f, 14);
            for (float z = z0 + 2.5f; z <= z1; z += 5f)
            {
                Kit.Block(f.s, "Pillar", new Vector3(-Half + 0.2f, 2.6f, z), new Vector3(0.9f, 5.2f, 0.9f), mLime);
                Kit.Mesh(f.s, "PillarNeon", MeshGen.Box(new Vector3(0.05f, 4.6f, 0.06f)), blueNeon, new Vector3(-Half + 0.67f, 2.5f, z), Vector3.one);
                if (z + 5f <= z1 + 0.01f)
                    Kit.Mesh(f.s, "Arch", arch, mLime, new Vector3(-Half + 0.2f, 3.15f, z + 2.5f), Vector3.one, Quaternion.Euler(0, 90f, 0));
                // Upper-floor French windows with a balcony rail.
                Kit.Mesh(f.s, "Window", MeshGen.Box(new Vector3(0.06f, 1.9f, 1.3f)), warmWindow, new Vector3(-Half - 0.48f, 7.6f, z + 2.5f), Vector3.one);
                Kit.Mesh(f.s, "Balcony", MeshGen.Box(new Vector3(0.5f, 0.06f, 1.6f)), mDarkMetal, new Vector3(-Half - 0.2f, 6.75f, z + 2.5f), Vector3.one);
                Kit.Mesh(f.s, "BalconyRail", MeshGen.Box(new Vector3(0.04f, 0.45f, 1.6f)), mDarkMetal, new Vector3(-Half + 0.03f, 6.98f, z + 2.5f), Vector3.one);
                Kit.Mesh(f.s, "Dormer", MeshGen.Box(new Vector3(0.4f, 0.9f, 0.9f)), warmWindow, new Vector3(-Half - 0.5f, 11f, z + 2.5f), Vector3.one);
            }
        }

        void Topiary(Frame f, Vector3 local)
        {
            var g = Kit.Group(f.s, "Topiary", local);
            Kit.Block(g, "Pot", new Vector3(0, 0.55f, 0), new Vector3(1.4f, 1.1f, 1.4f), mLime);
            Kit.Mesh(g, "PotTrim", MeshGen.Box(new Vector3(1.42f, 0.06f, 1.42f)), ExpoMaterials.Emissive(ExpoPalette.Blue, 1.5f), new Vector3(0, 1.0f, 0), Vector3.one);
            Kit.Mesh(g, "Ball", MeshGen.Sphere(0.62f, 12, 8), mHedge, new Vector3(0, 1.65f, 0), Vector3.one);
            Kit.Mesh(g, "Ball2", MeshGen.Sphere(0.38f, 10, 7), mHedge, new Vector3(0, 2.55f, 0), Vector3.one);
        }

        void Fountain(Frame f, Vector3 local)
        {
            var g = Kit.Group(f.s, "Fountain", local);
            var basin = Kit.Mesh(g, "Basin", MeshGen.Cylinder(2.7f, 2.7f, 0.8f, 32), mLime, Vector3.zero, Vector3.one);
            var col = basin.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 2.7f; col.height = 6f; col.center = new Vector3(0, 0.4f, 0);
            Kit.Mesh(g, "Water", MeshGen.Cylinder(2.45f, 2.45f, 0.05f, 32), waterMat, new Vector3(0, 0.7f, 0), Vector3.one);
            Kit.Mesh(g, "Rim", MeshGen.Torus(2.6f, 0.05f, 48, 5), ExpoMaterials.Emissive(ExpoPalette.Blue, 2.2f), new Vector3(0, 0.82f, 0), Vector3.one);
            Kit.Mesh(g, "Column", MeshGen.Cylinder(0.25f, 0.18f, 1.8f, 12), mLime, new Vector3(0, 0.8f, 0), Vector3.one);
            var jet = Kit.Mesh(g, "Jet", MeshGen.Cylinder(0.35f, 0.05f, 2.4f, 16, false), ExpoMaterials.Holo(new Color(0.55f, 0.75f, 1f), 0.5f), new Vector3(0, 2.6f, 0), Vector3.one);
            jet.shadowCastingMode = ShadowCastingMode.Off;
            Kit.PointLight(g, new Vector3(0, 1.4f, 0), ExpoPalette.Blue, 6f, 1.6f);
        }

        void GrandArch(Frame f, float z, Color color)
        {
            var g = Kit.Group(f.s, "GrandArch", new Vector3(0, 0, z));
            for (int s = -1; s <= 1; s += 2)
                Kit.Block(g, "ArchFoot", new Vector3(8.4f * s, 2f, 0), new Vector3(1.2f, 4f, 1.4f), mLime);
            Kit.Mesh(g, "ArchSpan", MeshGen.ArcBeam(8.4f, 0.7f, 1.2f, 0f, 180f, 36), mLime, new Vector3(0, 4f, 0), Vector3.one);
            Kit.Mesh(g, "ArchNeon", MeshGen.ArcBeam(7.95f, 0.08f, 1.22f, 0f, 180f, 36), ExpoMaterials.Emissive(color, 2.6f), new Vector3(0, 4f, 0), Vector3.one);
        }

        // ================================================================ UK

        void BuildUKDistrict()
        {
            var f = MakeFrame("UK_District", new Vector3(49f, 0f, 40f), 180f);
            Color emerald = ExpoPalette.Emerald;

            UKFacade(f, -18f, 98f);
            InnerLamps(f, emerald, 2);
            GuideStrip(f.s, new Vector3(3f, 0, 1f), new Vector3(3f, 0, 79f), emerald);
            doorUK = Door(f, 0f, emerald, "UNITED KINGDOM PAVILION DISTRICT", mUkStone);

            // K1: sweeping gantry (slide under) and a pulsing grid.
            Laser(f, new Vector3(0, 0, 12f), LaserBarrier.Pattern.Sweep, Half * 2f, new[] { 1.05f, 1.6f, 2.2f }, l => { l.sweepDistance = 9f; l.sweepPeriod = 6.5f; });
            Laser(f, new Vector3(0, 0, 20.5f), LaserBarrier.Pattern.Pulse, Half * 2f, new[] { 0.45f, 1.0f, 1.55f, 2.1f }, l => { l.onTime = 1.6f; l.offTime = 1.3f; });
            Hedge(f, new Vector3(-6.6f, 0, 6f), new Vector3(1f, 1.2f, 4f));
            Hint(f, 1f, 5f, "Slide under the moving gantry [SPACE]  ·  press 3 for UK and RIGHT-CLICK: CHRONO LOCK slows lasers", 7f);

            // K2: Big Ben clock plaza with rotating laser hands.
            ClockPlaza(f, new Vector3(0, 0, 35f));
            Laser(f, new Vector3(0, 0, 35f), LaserBarrier.Pattern.Rotate, 0f, new[] { 0.55f, 1.3f }, l => { l.radius = 8.6f; l.arms = 2; l.rotateSpeed = 48f; });
            Hint(f, 22f, 25.5f, "The clock hands sweep the plaza  ·  CHRONO LOCK slows them to a crawl", 6f);

            // K3: pulsing grid, then the tempo gates.
            Laser(f, new Vector3(0, 0, 48.5f), LaserBarrier.Pattern.Pulse, Half * 2f, new[] { 0.5f, 1.1f, 1.7f }, l => { l.onTime = 1.3f; l.offTime = 1.3f; });
            CrossWall(f, -Half, -6.3f, 54f, 4.4f, mUkStone, emerald);
            CrossWall(f, -2.7f, 2.7f, 54f, 4.4f, mUkStone, emerald);
            CrossWall(f, 6.3f, Half, 54f, 4.4f, mUkStone, emerald);
            Tempo(f, new Vector3(-4.5f, 0, 54f), 3.4f, 1.15f, 0.27f, 0f);
            Tempo(f, new Vector3(4.5f, 0, 54f), 3.4f, 1.3f, 0.25f, 0.5f);
            Hint(f, 49.5f, 52.5f, "TEMPO GATES slam shut too fast to pass  ·  open a CHRONO LOCK field first", 6f);

            // K4: optical sentry among the hedges.
            Drone(f, opticalDrone, new Vector2(-6f, 63f), new Vector2(6f, 63f), new Vector2(6f, 76f), new Vector2(-6f, 76f));
            Hedge(f, new Vector3(-3f, 0, 66.5f), new Vector3(5f, 1.25f, 1f));
            Hedge(f, new Vector3(4f, 0, 71f), new Vector3(4f, 1.25f, 1f));
            Hedge(f, new Vector3(-5f, 0, 75.5f), new Vector3(4f, 1.25f, 1f));
            HoloSign(dynamicRoot, f.P(0, 5f, 79f), "BIG BEN DISPLAY", "LANDMARK OFFLINE", emerald, 0.42f);
            VfxFactory.AmbientMotes(f.P(0, 3f, 40f), new Vector3(18f, 5f, 80f), new Color(0.45f, 1f, 0.7f), 14f).transform.SetParent(dynamicRoot, true);
        }

        void UKFacade(Frame f, float z0, float z1)
        {
            var emerald = ExpoMaterials.Emissive(ExpoPalette.Emerald, 2.6f);
            var lancet = ExpoMaterials.Emissive(new Color(1f, 0.72f, 0.42f), 0.5f);
            float len = z1 - z0, mid = (z0 + z1) * 0.5f;
            Kit.Block(f.s, "UKWall", new Vector3(-Half - 1.5f, 4.5f, mid), new Vector3(2f, 9f, len), mUkStone);
            Kit.Mesh(f.s, "UKBand", MeshGen.Box(new Vector3(0.06f, 0.08f, len)), emerald, new Vector3(-Half - 0.47f, 8.6f, mid), Vector3.one);
            int i = 0;
            for (float z = z0 + 3f; z < z1; z += 6f, i++)
            {
                Kit.Mesh(f.s, "Pilaster", MeshGen.TaperedBox(new Vector2(0.9f, 0.9f), new Vector2(0.55f, 0.55f), 12f), mUkStone, new Vector3(-Half - 0.2f, 0, z), Vector3.one);
                Kit.Mesh(f.s, "Spire", MeshGen.TaperedBox(new Vector2(0.6f, 0.6f), new Vector2(0.02f, 0.02f), 2.6f), mUkStone, new Vector3(-Half - 0.2f, 12f, z), Vector3.one);
                Kit.Mesh(f.s, "PilasterNeon", MeshGen.Box(new Vector3(0.05f, 10.5f, 0.06f)), emerald, new Vector3(-Half + 0.27f, 5.6f, z), Vector3.one);
                // Lancet windows between pilasters.
                for (int k = -1; k <= 1; k++)
                    Kit.Mesh(f.s, "Lancet", MeshGen.Box(new Vector3(0.05f, 4.2f, 0.45f)), lancet, new Vector3(-Half - 0.47f, 3.6f, z + 3f + k * 0.85f), Vector3.one);
                if (i % 2 == 0)
                {
                    // Clock rings with slowly turning hands.
                    var clock = Kit.Group(f.s, "FacadeClock", new Vector3(-Half - 0.42f, 6.6f, z + 3f), 0f);
                    Kit.Mesh(clock, "Ring", MeshGen.Torus(1.25f, 0.06f, 40, 5), emerald, Vector3.zero, Vector3.one, Quaternion.Euler(0, 0, 90f));
                    for (int t = 0; t < 12; t++)
                    {
                        Quaternion r = Quaternion.Euler(t * 30f, 0, 0);
                        Kit.Mesh(clock, "Tick", MeshGen.Box(new Vector3(0.04f, 0.2f, 0.05f)), emerald, r * new Vector3(0.02f, 1.02f, 0), Vector3.one, r);
                    }
                    var hands = Kit.Group(clock, "Hands", new Vector3(0.05f, 0, 0));
                    Kit.Mesh(hands, "Hour", MeshGen.Box(new Vector3(0.04f, 0.6f, 0.06f)), lancet, new Vector3(0, 0.3f, 0), Vector3.one);
                    Kit.Mesh(hands, "Minute", MeshGen.Box(new Vector3(0.03f, 0.9f, 0.04f)), lancet, new Vector3(0, 0.45f, 0), Vector3.one, Quaternion.Euler(70f, 0, 0));
                    var spin = hands.gameObject.AddComponent<Spin>();
                    spin.axis = Vector3.right;
                    spin.degreesPerSecond = 12f;
                }
            }
        }

        void Hedge(Frame f, Vector3 local, Vector3 size)
        {
            Kit.Block(f.s, "Hedge", local + Vector3.up * size.y * 0.5f, size, mHedge);
            Kit.Mesh(f.s, "HedgeLight", MeshGen.Box(new Vector3(size.x + 0.02f, 0.04f, size.z + 0.02f)), ExpoMaterials.Emissive(ExpoPalette.Emerald, 1.2f), local + Vector3.up * 0.12f, Vector3.one);
        }

        void ClockPlaza(Frame f, Vector3 local)
        {
            var g = Kit.Group(f.s, "ClockPlaza", local);
            var face = Kit.Mesh(g, "ClockFace", MeshGen.Annulus(0.6f, 8.9f, 96), mClock, new Vector3(0, 0.008f, 0), Vector3.one);
            face.shadowCastingMode = ShadowCastingMode.Off;
            var em = ExpoMaterials.Emissive(ExpoPalette.Emerald, 2.2f);
            Kit.Mesh(g, "Rim", MeshGen.Annulus(8.75f, 8.9f, 96), em, new Vector3(0, 0.012f, 0), Vector3.one);
            for (int i = 0; i < 12; i++)
            {
                Quaternion r = Quaternion.Euler(0, i * 30f, 0);
                Kit.Mesh(g, "Hour", MeshGen.Box(new Vector3(0.18f, 0.02f, i % 3 == 0 ? 1.4f : 0.7f)), em, r * new Vector3(0, 0.013f, 7.8f), Vector3.one, r);
            }
            Kit.PointLight(g, new Vector3(0, 3f, 0), ExpoPalette.Emerald, 12f, 1.4f);
        }

        // ================================================================ convergence walk

        void BuildConvergenceWalk()
        {
            var f = MakeFrame("ConvergenceWalk", new Vector3(40f, 0f, -49f), 270f);
            InnerLamps(f, Color.white, 0, 6f, 24f, 9f);
            GuideStrip(f.s, new Vector3(2.4f, 0, 0f), new Vector3(2.4f, 0, 28f), ExpoPalette.Gold);
            GuideStrip(f.s, new Vector3(3f, 0, 0f), new Vector3(3f, 0, 28f), ExpoPalette.Blue);
            GuideStrip(f.s, new Vector3(3.6f, 0, 0f), new Vector3(3.6f, 0, 28f), ExpoPalette.Emerald);

            CrossWall(f, -Half, -2.35f, 4f, 4.2f, mWall, ExpoPalette.Gold);
            CrossWall(f, 2.35f, Half, 4f, 4.2f, mWall, ExpoPalette.Gold);
            Turnstile(f, new Vector3(0, 0, 4f), 4f);
            CameraOnPost(f, new Vector3(-8.3f, 3.4f, 12.5f), 90f, 70f, 24f, false);
            Planter(f.s, new Vector3(4.5f, 0, 9.5f), new Vector3(2.4f, 1.2f, 2.4f), mWhite, Color.white, true);
            Planter(f.s, new Vector3(-5f, 0, 15.5f), new Vector3(2.4f, 1.2f, 2.4f), mWhite, Color.white, true);
            Laser(f, new Vector3(0, 0, 20f), LaserBarrier.Pattern.Pulse, Half * 2f, new[] { 0.5f, 1.1f, 1.7f }, l => { l.onTime = 1.25f; l.offTime = 1.1f; });
            doorWalk = Door(f, 28f, Color.white, "CONVERGENCE WALK", mWall);
            Hint(f, 0f, 3f, "Final gauntlet  ·  every passport counts: cloak, pulse and bend time", 6f);
        }
    }

    /// <summary>Shows a contextual hint the first time the courier enters its bounds.</summary>
    public class HintZone : MonoBehaviour
    {
        Bounds bounds;
        string text;
        float seconds;
        bool shown;

        public void Init(Bounds b, string message, float duration)
        {
            bounds = b;
            text = message;
            seconds = duration;
        }

        void Update()
        {
            if (shown || Game.Player == null || Game.State == null || Game.State.Current != GameState.Playing) return;
            if (bounds.Contains(Game.Player.transform.position + Vector3.up))
            {
                shown = true;
                GameEvents.RaiseHint(text, seconds);
            }
        }
    }
}
