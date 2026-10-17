using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExpoConvergence
{
    public partial class ExpoWorld
    {
        // ================================================================ corner plazas

        void BuildCornerPlazas()
        {
            // NW · Burj Display + France consular gate.
            Dais(new Vector3(-49f, 0, 49.5f), ExpoPalette.Gold);
            Terminal(new Vector3(-49f, 0.3f, 50.2f), new Vector3(-49f, 0, 44f), PassportType.UAE, false, "BURJ DISPLAY", "RESTORE LANDMARK", 1.6f,
                () => landmarks[0].Restore(new Vector3(-49f, 0, 49f), () => Game.Progress?.RestoreLandmark(0)));
            Terminal(new Vector3(-43.2f, 0f, 54f), new Vector3(-49f, 0, 49f), PassportType.France, false, "FRANCE CONSULAR GATE", "AUTHENTICATE", 1.1f, () =>
            {
                doorFrance.Open();
                GameEvents.RaiseToast("FRANCE DISTRICT OPEN  ·  OPTICAL GRID ACTIVE", ExpoPalette.Blue, 3f);
                Game.Progress?.SetObjective("Blind the optical grid and reach the Eiffel Display");
            });
            Planter(staticRoot, new Vector3(-55f, 0, 55f), new Vector3(2.6f, 1.2f, 2.6f), mSandDark, ExpoPalette.Gold, true);
            Planter(staticRoot, new Vector3(-55f, 0, 43f), new Vector3(2.6f, 1.2f, 2.6f), mSandDark, ExpoPalette.Gold, true);

            // NE · Eiffel Display + UK consular gate.
            Dais(new Vector3(49.5f, 0, 49f), ExpoPalette.Blue);
            Terminal(new Vector3(50.2f, 0.3f, 49f), new Vector3(44f, 0, 49f), PassportType.France, false, "EIFFEL DISPLAY", "RESTORE LANDMARK", 1.6f,
                () => landmarks[1].Restore(new Vector3(49f, 0, 49f), () => Game.Progress?.RestoreLandmark(1)));
            Terminal(new Vector3(54f, 0f, 43.2f), new Vector3(49f, 0, 49f), PassportType.UK, false, "UK CONSULAR GATE", "AUTHENTICATE", 1.1f, () =>
            {
                doorUK.Open();
                GameEvents.RaiseToast("UK DISTRICT OPEN  ·  LASER GRID ACTIVE", ExpoPalette.Emerald, 3f);
                Game.Progress?.SetObjective("Bend time through the laser grid and reach the Big Ben Display");
            });
            Topiary(new Frame { s = staticRoot, d = dynamicRoot }, new Vector3(55f, 0, 55f));
            Topiary(new Frame { s = staticRoot, d = dynamicRoot }, new Vector3(43f, 0, 55f));

            // SE · Big Ben Display and the Poem Pavilion sculpture.
            Dais(new Vector3(49f, 0, -49.5f), ExpoPalette.Emerald);
            Terminal(new Vector3(49f, 0.3f, -50.2f), new Vector3(49f, 0, -44f), PassportType.UK, false, "BIG BEN DISPLAY", "RESTORE LANDMARK", 1.6f,
                () => landmarks[2].Restore(new Vector3(49f, 0, -49f), () => Game.Progress?.RestoreLandmark(2)));
            PoemPavilion(new Vector3(54.5f, 0, -54.5f));

            // SW · decorative.
            Planter(staticRoot, new Vector3(-55f, 0, -55f), new Vector3(2.6f, 1.2f, 2.6f), mSandDark, ExpoPalette.Gold, true);
            Bench(staticRoot, new Vector3(-52f, 0, -56.6f), 0f, ExpoPalette.Gold);
        }

        void Dais(Vector3 pos, Color color)
        {
            var g = Kit.Group(staticRoot, "Dais", pos);
            var disc = Kit.Mesh(g, "Platform", MeshGen.Cylinder(3.4f, 3.2f, 0.3f, 40), mDarkMetal, Vector3.zero, Vector3.one);
            var col = disc.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = disc.GetComponent<MeshFilter>().sharedMesh;
            Kit.Mesh(g, "Rim", MeshGen.Torus(3.3f, 0.05f, 64, 5), ExpoMaterials.Emissive(color, 2.6f), new Vector3(0, 0.3f, 0), Vector3.one);
            Kit.Mesh(g, "Inner", MeshGen.Annulus(2.2f, 2.3f, 48), ExpoMaterials.Emissive(color, 2f), new Vector3(0, 0.305f, 0), Vector3.one);
            Kit.PointLight(g, new Vector3(0, 2.5f, 0), color, 9f, 1.6f);
        }

        void PoemPavilion(Vector3 pos)
        {
            // Inspired by the UK pavilion's poem cone: a tall cone of glowing message slats.
            var g = Kit.Group(staticRoot, "PoemPavilion", pos);
            Kit.Block(g, "Base", new Vector3(0, 0.25f, 0), new Vector3(4.5f, 0.5f, 4.5f), mUkStone);
            var slat = ExpoMaterials.Emissive(ExpoPalette.Emerald, 1.8f);
            var white = ExpoMaterials.Emissive(Color.white, 1.4f);
            for (int i = 0; i < 18; i++)
            {
                float y = 0.6f + i * 0.42f;
                float r = Mathf.Lerp(2f, 0.25f, i / 17f);
                Kit.Mesh(g, "Slat", MeshGen.Torus(r, 0.05f, 32, 4), i % 3 == 0 ? white : slat, new Vector3(0, y, 0), Vector3.one);
            }
            Kit.PointLight(g, new Vector3(0, 3.5f, 0), ExpoPalette.Emerald, 8f, 1.4f);
        }

        // ================================================================ dome

        void BuildDome()
        {
            var g = Kit.Group(staticRoot, "AlWaslDome", Vector3.zero);
            const float R = 15f;

            var lattice = ExpoMaterials.EmissiveInstance(new Color(0.75f, 0.85f, 1f), 0.55f);
            lattice.SetColor("_BaseColor", new Color(0.28f, 0.31f, 0.38f));
            lattice.SetFloat("_Smoothness", 0.8f);
            lattice.SetFloat("_Metallic", 0.9f);
            var model = Resources.Load<GameObject>("Models/AlWaslDome");
            if (model != null)
            {
                var inst = Instantiate(model, g);
                inst.transform.localPosition = Vector3.zero;
                ExpoModelUtil.ApplyMaterials(inst, lattice, mDarkMetal, lattice);
            }
            else
            {
                var dl = Kit.Mesh(g, "Lattice", MeshGen.LatticeDome(R, 0.26f, 28, 7), lattice, Vector3.zero, Vector3.one);
                dl.shadowCastingMode = ShadowCastingMode.On;
                // A second, finer and slightly rotated trellis for the woven look.
                var fine = Kit.Mesh(g, "LatticeFine", MeshGen.LatticeDome(R - 0.35f, 0.1f, 56, 14), lattice, Vector3.zero, Vector3.one, Quaternion.Euler(0, 360f / 112f, 0));
                fine.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Projection shell, one third per nation.
            var sectors = new Material[3];
            float[] from = { 150f, 30f, -90f };
            for (int i = 0; i < 3; i++)
            {
                sectors[i] = ExpoMaterials.Holo(ExpoPalette.ForPassport((PassportType)i), 0.035f, true);
                var shell = Kit.Mesh(g, "Projection_" + (PassportType)i, MeshGen.SphereSector(R - 0.6f, from[i], from[i] + 120f, 36, 14, 4f, 86f), sectors[i], Vector3.zero, Vector3.one);
                shell.shadowCastingMode = ShadowCastingMode.Off;
            }
            var oculus = ExpoMaterials.EmissiveInstance(Color.white, 1.4f);
            var crown = ExpoMaterials.EmissiveInstance(ExpoPalette.Cyan, 2f);
            Kit.Mesh(g, "Oculus", MeshGen.Torus(2.3f, 0.22f, 40, 6), oculus, new Vector3(0, R * 0.985f, 0), Vector3.one);
            Kit.Mesh(g, "BaseRing", MeshGen.Torus(R + 0.1f, 0.28f, 96, 6), crown, new Vector3(0, 0.2f, 0), Vector3.one);

            // Base wall ring with the entrance facing the bridge (south).
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f + 7.5f;
                if (a > 247f && a < 293f) continue;
                Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
                Kit.Block(g, "DomeBase", dir * (R + 0.15f) + Vector3.up * 0.6f, new Vector3(0.5f, 1.2f, 4.1f), mDarkMetal, 90f - a);
                InvisibleWall(g, dir * (R + 0.15f) + Vector3.up * 3f, new Vector3(0.5f, 6f, 4.1f), Quaternion.Euler(0, 90f - a, 0));
            }
            for (int s = -1; s <= 1; s += 2)
                Kit.Block(g, "EntrancePillar", new Vector3(3.3f * s, 2.5f, -R + 0.2f), new Vector3(0.6f, 5f, 0.8f), mMetal);
            Kit.Mesh(g, "EntranceArch", MeshGen.ArcBeam(3.3f, 0.3f, 0.8f, 0f, 180f, 24), ExpoMaterials.Emissive(Color.white, 2.2f), new Vector3(0, 5f, -R + 0.2f), Vector3.one);

            // Interior: tri-colour rings and the authentication pedestal.
            for (int i = 0; i < 3; i++)
            {
                float a0 = from[i] + 8f, a1 = from[i] + 112f;
                Kit.Mesh(g, "FloorArc", MeshGen.Annulus(5.2f, 5.45f, 40, a0, a1), ExpoMaterials.Emissive(ExpoPalette.ForPassport((PassportType)i), 2.2f), new Vector3(0, 0.012f, 0), Vector3.one);
                Kit.Mesh(g, "FloorArcOuter", MeshGen.Annulus(9.6f, 9.75f, 60, a0, a1), ExpoMaterials.Emissive(ExpoPalette.ForPassport((PassportType)i), 1.2f), new Vector3(0, 0.012f, 0), Vector3.one);
            }
            var pedestalBase = Kit.Mesh(g, "PedestalDais", MeshGen.Cylinder(3.6f, 3.3f, 0.3f, 40), mDarkMetal, Vector3.zero, Vector3.one);
            pedestalBase.gameObject.AddComponent<MeshCollider>().sharedMesh = pedestalBase.GetComponent<MeshFilter>().sharedMesh;
            var domeLightGo = new GameObject("DomeLight");
            domeLightGo.transform.SetParent(g, false);
            domeLightGo.transform.localPosition = new Vector3(0, 9f, 0);
            var domeLight = domeLightGo.AddComponent<Light>();
            domeLight.type = LightType.Point;
            domeLight.range = 34f;
            domeLight.intensity = 1.1f;
            domeLight.color = new Color(0.6f, 0.75f, 1f);
            domeLight.shadows = LightShadows.None;

            var domeGo = new GameObject("DomeController");
            domeGo.transform.SetParent(dynamicRoot, false);
            dome = domeGo.AddComponent<AlWaslDome>();
            dome.Bind(sectors, lattice, oculus, crown, domeLight);

            var pedestal = Terminal(new Vector3(0, 0.3f, -0.4f), new Vector3(0, 0, -10f), PassportType.UAE, true, "AUTHENTICATION PEDESTAL", "INSERT TRI-DRIVE", 2f,
                () => dome.BeginFinale(new Vector3(0, 0.3f, 0)));

            BuildSealGate();
        }

        void BuildSealGate()
        {
            var g = Kit.Group(dynamicRoot, "WaslSeal", new Vector3(0, 0, -40.7f));
            var frame = ExpoMaterials.Lit(new Color(0.1f, 0.11f, 0.14f), 0.7f, 0.85f);
            for (int s = -1; s <= 1; s += 2)
                Kit.Block(g, "SealPylon", new Vector3(3.6f * s, 3f, 0), new Vector3(1.1f, 6f, 1.2f), frame);
            Kit.Block(g, "SealLintel", new Vector3(0, 6.3f, 0), new Vector3(8.4f, 0.8f, 1.2f), frame);
            var fieldMat = ExpoMaterials.Field(ExpoPalette.Cyan, 0.3f, true);
            var field = Kit.Mesh(g, "SealField", MeshGen.Quad(6.1f, 5.9f), fieldMat, Vector3.zero, Vector3.one);
            field.shadowCastingMode = ShadowCastingMode.Off;
            var locks = new Renderer[3];
            var lockMats = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                lockMats[i] = ExpoMaterials.EmissiveInstance(ExpoPalette.ForPassport((PassportType)i), 0.35f);
                locks[i] = Kit.Mesh(g, "SealLock", MeshGen.Sphere(0.32f, 16, 10), lockMats[i], new Vector3(-1.4f + i * 1.4f, 7.15f, -0.2f), Vector3.one);
                Kit.Mesh(g, "LockRing", MeshGen.Torus(0.42f, 0.04f, 24, 5), frame, new Vector3(-1.4f + i * 1.4f, 7.15f, -0.2f), Vector3.one, Quaternion.Euler(90, 0, 0));
            }
            Kit.Text(g, "AL WASL SEAL  ·  THREE KEYS REQUIRED", new Vector3(0, 8.1f, -0.65f), 0.26f, Color.white);
            var blockerGo = new GameObject("SealBlocker") { layer = ExpoLayers.Sensor };
            blockerGo.transform.SetParent(g, false);
            var blocker = blockerGo.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, 3f, 0);
            blocker.size = new Vector3(6.2f, 6f, 0.6f);
            dome.BindSeal(locks, lockMats, fieldMat, blocker);
        }

        // ================================================================ landmarks

        void BuildLandmarks()
        {
            landmarks[0] = BuildBurj(new Vector3(-84f, 0, 84f));
            landmarks[1] = BuildEiffel(new Vector3(84f, 0, 84f));
            landmarks[2] = BuildBigBen(new Vector3(84f, 0, -84f));
        }

        LandmarkController NewLandmark(string name, Vector3 pos, int index, PassportType nation, float height, out Transform root)
        {
            root = Kit.Group(staticRoot, name, pos);
            var go = new GameObject(name + "_Controller");
            go.transform.SetParent(dynamicRoot, false);
            go.transform.position = pos;
            var lc = go.AddComponent<LandmarkController>();
            lc.index = index;
            lc.nation = nation;
            lc.height = height;
            lc.displayName = name;
            // Base lighting that switches on when restored.
            for (int k = 0; k < 2; k++)
            {
                // Range is capped: huge light volumes are costly on integrated GPUs.
                var l = Kit.PointLight(go.transform, new Vector3(0, height * (0.12f + k * 0.35f), 0), ExpoPalette.ForPassport(nation), Mathf.Min(height * 0.5f, 34f), 2.4f);
                lc.AddLight(l);
            }
            // Sky beam.
            var beamMat = ExpoMaterials.Beam(ExpoPalette.ForPassport(nation), 3f, true);
            beamMat.SetFloat("_Alpha", 0.55f);
            var beamGo = new GameObject("SkyBeam");
            beamGo.transform.SetParent(go.transform, false);
            beamGo.transform.localPosition = new Vector3(0, height, 0);
            var quadA = Kit.Mesh(beamGo.transform, "BeamA", MeshGen.BeamQuad(4f, 260f), beamMat, Vector3.zero, Vector3.one);
            var quadB = Kit.Mesh(beamGo.transform, "BeamB", MeshGen.BeamQuad(4f, 260f), beamMat, Vector3.zero, Vector3.one, Quaternion.Euler(0, 90f, 0));
            quadA.shadowCastingMode = ShadowCastingMode.Off;
            quadB.shadowCastingMode = ShadowCastingMode.Off;
            beamGo.AddComponent<BeamBillboard>();
            lc.SetBeam(quadA, beamMat);
            quadB.enabled = false;
            beamGo.GetComponent<BeamBillboard>().Bind(quadA, quadB);
            return lc;
        }

        Material Band(LandmarkController lc, float y, float intensity)
        {
            var m = ExpoMaterials.EmissiveInstance(ExpoPalette.ForPassport(lc.nation), intensity);
            lc.AddGlow(m, y, intensity);
            return m;
        }

        LandmarkController BuildBurj(Vector3 pos)
        {
            const float h = 128f;
            var lc = NewLandmark("BurjKhalifa", pos, 0, PassportType.UAE, h, out var root);
            var glass = ExpoMaterials.Lit(new Color(0.035f, 0.045f, 0.07f), 0.95f, 0.65f);
            var model = Resources.Load<GameObject>("Models/BurjKhalifa");
            if (model != null)
            {
                var inst = Instantiate(model, root);
                inst.transform.localPosition = Vector3.zero;
                ApplyLandmarkModel(lc, inst, glass);
                return lc;
            }
            const int seg = 12;
            float segH = 8f;
            for (int k = 0; k < seg; k++)
            {
                float y = k * segH;
                var glow = Band(lc, y, 3.6f);
                float coreR = Mathf.Lerp(3.6f, 1.4f, k / (float)seg);
                Kit.Mesh(root, "Core", MeshGen.Cylinder(coreR, coreR * 0.97f, segH, 12), glass, new Vector3(0, y, 0), Vector3.one);
                for (int w = 0; w < 3; w++)
                {
                    // Y-shaped plan: three wings that step back as the tower rises.
                    float yaw = w * 120f + 30f;
                    float len = Mathf.Lerp(11f, 2.2f, Mathf.Pow(k / (float)(seg - 1), 0.85f)) - (w == k % 3 ? 0.9f : 0f);
                    float width = Mathf.Lerp(3.6f, 1.6f, k / (float)seg);
                    Quaternion r = Quaternion.Euler(0, yaw, 0);
                    Kit.Mesh(root, "Wing", MeshGen.Box(new Vector3(width, segH, len)), glass, r * new Vector3(0, y + segH * 0.5f, len * 0.5f), Vector3.one, r);
                    Kit.Mesh(root, "WingEdge", MeshGen.Box(new Vector3(0.3f, segH - 0.2f, 0.3f)), glow, r * new Vector3(0, y + segH * 0.5f, len), Vector3.one, r);
                    Kit.Mesh(root, "WingBand", MeshGen.Box(new Vector3(width + 0.06f, 0.18f, len)), glow, r * new Vector3(0, y + segH - 0.1f, len * 0.5f), Vector3.one, r);
                }
            }
            float spireBase = seg * segH;
            var spireGlow = Band(lc, spireBase, 4.5f);
            Kit.Mesh(root, "Spire", MeshGen.Cylinder(1.3f, 0.12f, h - spireBase, 10), glass, new Vector3(0, spireBase, 0), Vector3.one);
            for (int k = 0; k < 4; k++)
                Kit.Mesh(root, "SpireRing", MeshGen.Torus(Mathf.Lerp(1.2f, 0.35f, k / 4f), 0.08f, 20, 4), spireGlow, new Vector3(0, spireBase + 4f + k * 7f, 0), Vector3.one);
            Kit.Mesh(root, "Beacon", MeshGen.Sphere(0.5f, 10, 8), Band(lc, h - 2f, 8f), new Vector3(0, h, 0), Vector3.one);
            Kit.Mesh(root, "Podium", MeshGen.Cylinder(16f, 14f, 3f, 6), glass, Vector3.zero, Vector3.one);
            Kit.Mesh(root, "PodiumRing", MeshGen.Torus(15f, 0.12f, 64, 4), Band(lc, 0f, 2.5f), new Vector3(0, 3f, 0), Vector3.one);
            return lc;
        }

        LandmarkController BuildEiffel(Vector3 pos)
        {
            const float h = 84f;
            var lc = NewLandmark("EiffelTower", pos, 1, PassportType.France, h, out var root);
            var iron = ExpoMaterials.Lit(new Color(0.07f, 0.06f, 0.055f), 0.55f, 0.85f);
            var model = Resources.Load<GameObject>("Models/EiffelTower");
            if (model != null)
            {
                var inst = Instantiate(model, root);
                inst.transform.localPosition = Vector3.zero;
                ApplyLandmarkModel(lc, inst, iron);
                return lc;
            }
            // Four curved legs meeting at the first platform.
            for (int c = 0; c < 4; c++)
            {
                float sx = (c & 1) == 0 ? -1f : 1f, sz = (c & 2) == 0 ? -1f : 1f;
                Vector3 prev = new Vector3(13f * sx, 0, 13f * sz);
                const int steps = 6;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float inset = Mathf.Lerp(13f, 4.6f, 1f - (1f - t) * (1f - t));
                    Vector3 p = new Vector3(inset * sx, t * 28f, inset * sz);
                    Vector3 d = p - prev;
                    Quaternion r = Quaternion.LookRotation(d.normalized);
                    var glow = Band(lc, prev.y, 3.2f);
                    Kit.Mesh(root, "Leg", MeshGen.Box(new Vector3(2.2f, 2.2f, d.magnitude)), iron, (prev + p) * 0.5f, Vector3.one, r);
                    Kit.Mesh(root, "LegLight", MeshGen.Box(new Vector3(0.22f, 0.22f, d.magnitude)), glow, (prev + p) * 0.5f + new Vector3(sx, 0, sz) * 1.15f, Vector3.one, r);
                    // Lattice cross-bracing lights on the leg.
                    Kit.Mesh(root, "Brace", MeshGen.Box(new Vector3(0.1f, 0.1f, d.magnitude * 1.2f)), glow, (prev + p) * 0.5f, Vector3.one, r * Quaternion.Euler(0, 0, 0) * Quaternion.AngleAxis(38f, Vector3.right));
                    prev = p;
                }
            }
            // Base arches between the legs.
            for (int side = 0; side < 4; side++)
            {
                Quaternion r = Quaternion.Euler(0, side * 90f, 0);
                Kit.Mesh(root, "BaseArch", MeshGen.ArcBeam(8.6f, 0.9f, 0.9f, 10f, 170f, 24), iron, r * new Vector3(0, 0, -11.5f), Vector3.one, r);
                Kit.Mesh(root, "BaseArchLight", MeshGen.ArcBeam(8.15f, 0.16f, 0.95f, 10f, 170f, 24), Band(lc, 2f, 3f), r * new Vector3(0, 0, -11.5f), Vector3.one, r);
            }
            // Platforms.
            PlatformRing(root, lc, 28f, 12f, iron);
            PlatformRing(root, lc, 50f, 7f, iron);
            // Upper tower: four converging edges.
            for (int c = 0; c < 4; c++)
            {
                float sx = (c & 1) == 0 ? -1f : 1f, sz = (c & 2) == 0 ? -1f : 1f;
                Vector3 a = new Vector3(4.6f * sx, 28f, 4.6f * sz);
                Vector3 b = new Vector3(2.6f * sx, 50f, 2.6f * sz);
                Vector3 cc = new Vector3(0.5f * sx, 76f, 0.5f * sz);
                Edge(root, lc, a, b, iron, 1.6f);
                Edge(root, lc, b, cc, iron, 1.1f);
            }
            Kit.Mesh(root, "Antenna", MeshGen.Cylinder(0.35f, 0.08f, h - 76f, 8), iron, new Vector3(0, 76f, 0), Vector3.one);
            Kit.Mesh(root, "Beacon", MeshGen.Sphere(0.55f, 10, 8), Band(lc, h - 2f, 8f), new Vector3(0, h, 0), Vector3.one);
            return lc;
        }

        void PlatformRing(Transform root, LandmarkController lc, float y, float half, Material iron)
        {
            var glow = Band(lc, y, 3.6f);
            for (int side = 0; side < 4; side++)
            {
                Quaternion r = Quaternion.Euler(0, side * 90f, 0);
                Kit.Mesh(root, "Platform", MeshGen.Box(new Vector3(half * 2f + 1f, 1.6f, 1.4f)), iron, r * new Vector3(0, y, -half), Vector3.one, r);
                Kit.Mesh(root, "PlatformLight", MeshGen.Box(new Vector3(half * 2f + 1.1f, 0.16f, 0.16f)), glow, r * new Vector3(0, y - 0.85f, -half - 0.72f), Vector3.one, r);
            }
        }

        void Edge(Transform root, LandmarkController lc, Vector3 a, Vector3 b, Material iron, float thickness)
        {
            Vector3 d = b - a;
            Quaternion r = Quaternion.LookRotation(d.normalized);
            int bands = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 9f));
            for (int i = 0; i < bands; i++)
            {
                Vector3 p0 = Vector3.Lerp(a, b, i / (float)bands), p1 = Vector3.Lerp(a, b, (i + 1) / (float)bands);
                Kit.Mesh(root, "Edge", MeshGen.Box(new Vector3(thickness, thickness, Vector3.Distance(p0, p1))), iron, (p0 + p1) * 0.5f, Vector3.one, r);
                Kit.Mesh(root, "EdgeLight", MeshGen.Box(new Vector3(0.18f, 0.18f, Vector3.Distance(p0, p1))), Band(lc, p0.y, 3.4f),
                    (p0 + p1) * 0.5f + new Vector3(Mathf.Sign(a.x), 0, Mathf.Sign(a.z)) * thickness * 0.55f, Vector3.one, r);
            }
        }

        LandmarkController BuildBigBen(Vector3 pos)
        {
            const float h = 66f;
            var lc = NewLandmark("BigBen", pos, 2, PassportType.UK, h, out var root);
            var stone = ExpoMaterials.Lit(new Color(0.11f, 0.1f, 0.085f), 0.35f, 0.1f);
            var gilt = ExpoMaterials.Lit(new Color(0.35f, 0.28f, 0.12f), 0.7f, 0.9f);
            var model = Resources.Load<GameObject>("Models/BigBen");
            if (model != null)
            {
                var inst = Instantiate(model, root);
                inst.transform.localPosition = Vector3.zero;
                ApplyLandmarkModel(lc, inst, stone);
                return lc;
            }
            // Shaft with glowing pilasters in four height bands.
            Kit.Mesh(root, "Shaft", MeshGen.Box(new Vector3(9f, 42f, 9f)), stone, new Vector3(0, 21f, 0), Vector3.one);
            for (int band = 0; band < 4; band++)
            {
                var glow = Band(lc, band * 10.5f, 3f);
                for (int side = 0; side < 4; side++)
                {
                    Quaternion r = Quaternion.Euler(0, side * 90f, 0);
                    for (int k = -1; k <= 1; k += 2)
                        Kit.Mesh(root, "Pilaster", MeshGen.Box(new Vector3(0.35f, 10f, 0.3f)), glow, r * new Vector3(k * 2.6f, band * 10.5f + 5.25f, -4.55f), Vector3.one, r);
                    Kit.Mesh(root, "Window", MeshGen.Box(new Vector3(1.2f, 4f, 0.1f)), glow, r * new Vector3(0, band * 10.5f + 5.5f, -4.52f), Vector3.one, r);
                }
            }
            // Clock stage with four faces.
            Kit.Mesh(root, "ClockStage", MeshGen.Box(new Vector3(10.6f, 8f, 10.6f)), stone, new Vector3(0, 46f, 0), Vector3.one);
            var faceGlow = Band(lc, 42f, 5f);
            var tickGlow = Band(lc, 43f, 3f);
            for (int side = 0; side < 4; side++)
            {
                Quaternion r = Quaternion.Euler(0, side * 90f, 0);
                Kit.Mesh(root, "ClockFace", MeshGen.Cylinder(3.4f, 3.4f, 0.12f, 40), faceGlow, r * new Vector3(0, 46f, -5.32f), Vector3.one, r * Quaternion.Euler(-90f, 0, 0));
                Kit.Mesh(root, "ClockRim", MeshGen.Torus(3.55f, 0.18f, 40, 6), gilt, r * new Vector3(0, 46f, -5.4f), Vector3.one, r * Quaternion.Euler(90f, 0, 0));
                for (int t = 0; t < 12; t++)
                {
                    Quaternion tr = r * Quaternion.Euler(0, 0, t * 30f);
                    Kit.Mesh(root, "Tick", MeshGen.Box(new Vector3(0.18f, 0.7f, 0.08f)), tickGlow, r * new Vector3(0, 46f, -5.42f) + tr * new Vector3(0, 2.8f, 0), Vector3.one, tr);
                }
                Kit.Mesh(root, "HandH", MeshGen.Box(new Vector3(0.2f, 1.8f, 0.08f)), gilt, r * new Vector3(0, 46.9f, -5.48f), Vector3.one, r);
                Kit.Mesh(root, "HandM", MeshGen.Box(new Vector3(0.14f, 2.6f, 0.08f)), gilt, r * new Vector3(0.9f, 46.4f, -5.5f), Vector3.one, r * Quaternion.Euler(0, 0, -70f));
            }
            // Belfry and spire.
            Kit.Mesh(root, "Belfry", MeshGen.Box(new Vector3(9f, 6f, 9f)), stone, new Vector3(0, 53f, 0), Vector3.one);
            var belfryGlow = Band(lc, 50f, 3.6f);
            for (int side = 0; side < 4; side++)
            {
                Quaternion r = Quaternion.Euler(0, side * 90f, 0);
                for (int k = -1; k <= 1; k++)
                    Kit.Mesh(root, "BelfryArch", MeshGen.Box(new Vector3(1.1f, 3.4f, 0.1f)), belfryGlow, r * new Vector3(k * 2.4f, 53f, -4.52f), Vector3.one, r);
            }
            Kit.Mesh(root, "Spire", MeshGen.TaperedBox(new Vector2(8.6f, 8.6f), new Vector2(0.4f, 0.4f), 11f), stone, new Vector3(0, 56f, 0), Vector3.one);
            var spireGlow = Band(lc, 56f, 4f);
            for (int c = 0; c < 4; c++)
            {
                float sx = (c & 1) == 0 ? -1f : 1f, sz = (c & 2) == 0 ? -1f : 1f;
                Vector3 a = new Vector3(4.3f * sx, 56f, 4.3f * sz), b = new Vector3(0.2f * sx, 67f, 0.2f * sz);
                Vector3 d = b - a;
                Kit.Mesh(root, "SpireEdge", MeshGen.Box(new Vector3(0.2f, 0.2f, d.magnitude)), spireGlow, (a + b) * 0.5f, Vector3.one, Quaternion.LookRotation(d.normalized));
            }
            Kit.Mesh(root, "Finial", MeshGen.Cylinder(0.25f, 0.05f, 4f, 8), gilt, new Vector3(0, 67f, 0), Vector3.one);
            Kit.Mesh(root, "Beacon", MeshGen.Sphere(0.45f, 10, 8), Band(lc, h - 2f, 7f), new Vector3(0, 70.5f, 0), Vector3.one);
            return lc;
        }

        /// <summary>Wires a Blender landmark model: "Glow" material slots become height-banded glow materials.</summary>
        void ApplyLandmarkModel(LandmarkController lc, GameObject inst, Material body)
        {
            var bands = new Dictionary<int, Material>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    if (n.Contains("glow") || n.Contains("emiss") || n.Contains("accent"))
                    {
                        int band = Mathf.FloorToInt((r.bounds.min.y - lc.transform.position.y) / 8f);
                        if (!bands.TryGetValue(band, out var m)) { m = Band(lc, band * 8f, n.Contains("beacon") ? 8f : 3.6f); bands[band] = m; }
                        mats[i] = m;
                    }
                    else mats[i] = body;
                }
                r.sharedMaterials = mats;
            }
        }

        // ================================================================ skyline & atmosphere

        void BuildSkyline()
        {
            var g = Kit.Group(staticRoot, "Skyline", Vector3.zero);
            var roof = mDarkMetal;
            var warning = ExpoMaterials.Emissive(ExpoPalette.Alert, 4f);
            var rng = new System.Random(2020);
            float[] skip = { 45f, 135f, 315f };
            for (int i = 0; i < 46; i++)
            {
                float a = i * (360f / 46f) + (float)rng.NextDouble() * 4f;
                bool nearLandmark = false;
                foreach (float s in skip) if (Mathf.Abs(Mathf.DeltaAngle(a, s)) < 13f) nearLandmark = true;
                if (nearLandmark) continue;
                // Keep the city low so the three landmarks own the skyline.
                float r = 108f + (float)rng.NextDouble() * 60f;
                float w = 9f + (float)rng.NextDouble() * 10f, d = 9f + (float)rng.NextDouble() * 10f;
                float hgt = 14f + (float)rng.NextDouble() * (r > 140f ? 34f : 22f);
                Vector3 p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r, 0, Mathf.Sin(a * Mathf.Deg2Rad) * r);
                float yaw = -a + 90f;
                var tower = Kit.Mesh(g, "Tower", MeshGen.Box(new Vector3(w, hgt, d)), mWindows, p + Vector3.up * hgt * 0.5f, Vector3.one, Quaternion.Euler(0, yaw, 0));
                tower.shadowCastingMode = ShadowCastingMode.Off;
                Kit.Mesh(g, "Roof", MeshGen.Box(new Vector3(w + 0.4f, 1.2f, d + 0.4f)), roof, p + Vector3.up * (hgt + 0.6f), Vector3.one, Quaternion.Euler(0, yaw, 0));
                if (i % 3 == 0) Kit.Mesh(g, "Warning", MeshGen.Sphere(0.6f, 8, 6), warning, p + Vector3.up * (hgt + 1.8f), Vector3.one);
            }
            // Desert ground beyond the walls.
            Kit.Mesh(g, "Outskirts", MeshGen.FloorQuad(420f, 420f, true), mFloorDark, new Vector3(0, -0.05f, 0), Vector3.one).shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildAtmosphere()
        {
            // Floating light orbs drifting on the lagoon.
            var rng = new System.Random(7);
            Color[] cols = { ExpoPalette.Gold, ExpoPalette.Blue, ExpoPalette.Emerald, ExpoPalette.Cyan };
            for (int i = 0; i < 28; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 26.5f + (float)rng.NextDouble() * 11f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, -0.45f, Mathf.Sin(a) * r);
                if (Mathf.Abs(p.x) < 5f && p.z < 0f) continue;
                var orb = Kit.Mesh(dynamicRoot, "LagoonOrb", MeshGen.Sphere(0.32f, 12, 8), ExpoMaterials.Emissive(cols[i % cols.Length], 3.5f), p, Vector3.one);
                orb.shadowCastingMode = ShadowCastingMode.Off;
                orb.gameObject.AddComponent<Bob>().Init(0.12f, 0.6f + (float)rng.NextDouble() * 0.6f);
                waterOrbs.Add(orb);
            }
            // Security searchlights sweeping the sky from behind the facades.
            var beam = ExpoMaterials.Beam(new Color(0.55f, 0.85f, 1f), 0.7f);
            Vector3[] corners = { new Vector3(-66, 0, -66), new Vector3(66, 0, 66), new Vector3(-66, 0, 66), new Vector3(66, 0, -66) };
            for (int i = 0; i < corners.Length; i++)
            {
                var pivot = Kit.Group(dynamicRoot, "Searchlight", corners[i], i * 70f);
                var tilt = Kit.Group(pivot, "Tilt", Vector3.zero);
                tilt.localRotation = Quaternion.Euler(0, 0, 24f);
                var q = Kit.Mesh(tilt, "Beam", MeshGen.BeamQuad(2.4f, 170f), beam, Vector3.zero, Vector3.one);
                q.shadowCastingMode = ShadowCastingMode.Off;
                var q2 = Kit.Mesh(tilt, "Beam2", MeshGen.BeamQuad(2.4f, 170f), beam, Vector3.zero, Vector3.one, Quaternion.Euler(0, 90f, 0));
                q2.shadowCastingMode = ShadowCastingMode.Off;
                var spin = pivot.gameObject.AddComponent<Spin>();
                spin.degreesPerSecond = 9f + i * 2.5f;
            }
            VfxFactory.AmbientMotes(new Vector3(0, 4f, 0), new Vector3(50f, 8f, 50f), new Color(0.7f, 0.85f, 1f), 18f).transform.SetParent(dynamicRoot, true);
        }
    }

    /// <summary>Keeps a pair of crossed sky-beam quads showing the side facing the camera.</summary>
    public class BeamBillboard : MonoBehaviour
    {
        Renderer a, b;
        public void Bind(Renderer first, Renderer second) { a = first; b = second; }

        void LateUpdate()
        {
            if (a == null || !a.enabled) { if (b != null) b.enabled = false; return; }
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized) * Quaternion.Euler(0, 0, 0);
            b.enabled = false;
        }
    }
}
