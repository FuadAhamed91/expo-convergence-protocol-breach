using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    /// <summary>
    /// A skyline landmark display (Burj Khalifa, Eiffel Tower, Big Ben). Starts dark; restoring it
    /// plays a cinematic while its light climbs from base to spire, fires a sky beam and fireworks.
    /// </summary>
    public class LandmarkController : MonoBehaviour
    {
        public int index;
        public PassportType nation;
        public float height = 80f;
        public string displayName = "BURJ DISPLAY";
        public bool Restored { get; private set; }

        struct Glow { public Material mat; public float height; public float intensity; }
        readonly List<Glow> glows = new List<Glow>();
        readonly List<Light> lights = new List<Light>();
        Renderer beam;
        Material beamMat;
        float restoreTime = -1f;

        public void AddGlow(Material instanceMat, float atHeight, float intensity)
        {
            glows.Add(new Glow { mat = instanceMat, height = atHeight, intensity = intensity });
            ExpoMaterials.SetEmission(instanceMat, ExpoPalette.ForPassport(nation), intensity * 0.06f);
        }

        public void AddLight(Light l)
        {
            lights.Add(l);
            l.enabled = false;
        }

        public void SetBeam(Renderer r, Material m)
        {
            beam = r;
            beamMat = m;
            beam.enabled = false;
        }

        public void Restore(Vector3 viewFrom, Action onDone)
        {
            if (Restored) return;
            Restored = true;
            restoreTime = Time.time;
            Game.Audio?.PlaySfx("landmark");
            StartCoroutine(LightUp());

            Vector3 basePos = transform.position;
            Vector3 dir = (basePos - viewFrom); dir.y = 0f; dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 camA = viewFrom - dir * 4f + Vector3.up * 2.2f + side * 2f;
            Vector3 camB = viewFrom + dir * 10f + Vector3.up * 5f - side * 6f;
            Vector3 camC = basePos - dir * (height * 0.9f) + Vector3.up * (height * 0.25f) + side * (height * 0.35f);
            var shots = new List<CinematicDirector.Shot>
            {
                CinematicDirector.Shot.Move(camA, camB, basePos + Vector3.up * height * 0.15f, basePos + Vector3.up * height * 0.45f, 2.6f, 55f, 50f),
                CinematicDirector.Shot.Move(camB, camC, basePos + Vector3.up * height * 0.45f, basePos + Vector3.up * height * 0.6f, 3.4f, 50f, 46f),
                CinematicDirector.Shot.Move(camC, camC + Vector3.up * 6f - dir * 8f, basePos + Vector3.up * height * 0.6f, Vector3.zero + Vector3.up * 8f, 2.4f, 46f, 52f),
            };
            Game.Cinematics?.Play(shots, onDone);
        }

        IEnumerator LightUp()
        {
            Color c = ExpoPalette.ForPassport(nation);
            const float climb = 3.4f;
            float t = 0f;
            bool beamOn = false;
            int fireworks = 0;
            while (t < climb + 1.5f)
            {
                t += Time.deltaTime;
                float front = (t / climb) * height * 1.05f;
                foreach (var g in glows)
                {
                    float k = Mathf.Clamp01((front - g.height) / (height * 0.12f));
                    float flash = k > 0f && k < 1f ? 1.6f : 1f;
                    ExpoMaterials.SetEmission(g.mat, c, Mathf.Lerp(g.intensity * 0.06f, g.intensity * flash, k));
                }
                foreach (var l in lights) if (!l.enabled && front >= l.transform.position.y - transform.position.y) l.enabled = true;
                if (!beamOn && t >= climb * 0.95f && beam != null) { beam.enabled = true; beamOn = true; Game.CameraRig?.AddShake(0.4f); }
                if (t > climb * 0.7f && fireworks < 6 && Mathf.Repeat(t, 0.32f) < Time.deltaTime)
                {
                    fireworks++;
                    Vector3 p = transform.position + new Vector3(UnityEngine.Random.Range(-18f, 18f), height * UnityEngine.Random.Range(0.55f, 0.95f), UnityEngine.Random.Range(-18f, 18f));
                    VfxFactory.Firework(p, fireworks % 2 == 0 ? c : Color.Lerp(c, Color.white, 0.6f));
                    Game.Audio?.PlaySfxAt("firework", p, 0.7f);
                }
                yield return null;
            }
            foreach (var g in glows) ExpoMaterials.SetEmission(g.mat, c, g.intensity);
        }

        void Update()
        {
            if (!Restored || restoreTime < 0f || Time.time - restoreTime < 5f) return;
            // Gentle living shimmer once restored.
            float s = 0.85f + 0.15f * Mathf.Sin(Time.time * 1.3f + index);
            if (beamMat != null) beamMat.SetFloat("_Alpha", 0.55f * s);
        }
    }

    /// <summary>
    /// The Al Wasl Dome and its seal. Each restored landmark lights a third of the dome's projection
    /// shell; when all three are restored the seal opens and the pedestal starts the finale.
    /// </summary>
    public class AlWaslDome : MonoBehaviour
    {
        readonly Material[] sectorMats = new Material[3];
        Material latticeMat, oculusMat, crownMat;
        readonly Renderer[] sealLocks = new Renderer[3];
        readonly Material[] sealLockMats = new Material[3];
        Material sealFieldMat;
        Collider sealBlocker;
        Light domeLight;
        float[] sectorTarget = new float[3];
        float[] sectorValue = new float[3];
        bool finale;
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int OpenId = Shader.PropertyToID("_Open");

        public void Bind(Material[] sectors, Material lattice, Material oculus, Material crown, Light light)
        {
            for (int i = 0; i < 3; i++) { sectorMats[i] = sectors[i]; sectorTarget[i] = 0.035f; sectorValue[i] = 0.035f; }
            latticeMat = lattice;
            oculusMat = oculus;
            crownMat = crown;
            domeLight = light;
        }

        public void BindSeal(Renderer[] locks, Material[] lockMats, Material fieldMat, Collider blocker)
        {
            for (int i = 0; i < 3; i++) { sealLocks[i] = locks[i]; sealLockMats[i] = lockMats[i]; }
            sealFieldMat = fieldMat;
            sealBlocker = blocker;
        }

        public void SetSectorRestored(int i)
        {
            sectorTarget[i] = 0.38f;
            ExpoMaterials.SetEmission(sealLockMats[i], ExpoPalette.ForPassport((PassportType)i), 6f);
            VfxFactory.Burst(sealLocks[i].transform.position, ExpoPalette.ForPassport((PassportType)i), 40, 3f);
        }

        public void OpenSeal()
        {
            if (sealBlocker != null) sealBlocker.enabled = false;
            Game.Audio?.PlaySfxAt("door", sealBlocker != null ? sealBlocker.transform.position : transform.position, 1f);
            StartCoroutine(OpenSealRoutine());
        }

        IEnumerator OpenSealRoutine()
        {
            float t = 0f;
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                if (sealFieldMat != null) sealFieldMat.SetFloat(OpenId, Mathf.SmoothStep(0f, 1f, t / 1.5f));
                yield return null;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < 3; i++)
            {
                if (sectorMats[i] == null) continue;
                sectorValue[i] = Mathf.MoveTowards(sectorValue[i], sectorTarget[i], dt * 0.25f);
                float pulse = finale ? 1f : 0.85f + 0.15f * Mathf.Sin(Time.time * 1.7f + i * 2f);
                sectorMats[i].SetFloat(AlphaId, sectorValue[i] * pulse);
            }
        }

        /// <summary>The convergence finale: dome illumination, fireworks, then victory.</summary>
        public void BeginFinale(Vector3 pedestal)
        {
            if (finale) return;
            finale = true;
            Game.Audio?.PlayMusic(MusicMood.Finale);
            Game.Audio?.PlaySfx("finale");
            StartCoroutine(FinaleRoutine());
            var shots = new List<CinematicDirector.Shot>
            {
                CinematicDirector.Orbit(pedestal, 7f, 2.2f, 250f, 330f, 3.2f, pedestal + Vector3.up * 1.6f, pedestal + Vector3.up * 6f, 55f, 60f),
                CinematicDirector.Orbit(pedestal, 34f, 9f, 330f, 450f, 7.5f, pedestal + Vector3.up * 8f, pedestal + Vector3.up * 9f, 58f, 52f),
                CinematicDirector.Shot.Move(new Vector3(0, 40, -70), new Vector3(0, 55, -95), pedestal + Vector3.up * 8f, pedestal + Vector3.up * 12f, 4f, 52f, 48f),
            };
            Game.Cinematics?.Play(shots, () => Game.State?.OnVictory(), true, false, true);
        }

        IEnumerator FinaleRoutine()
        {
            Color[] seq = { ExpoPalette.Gold, ExpoPalette.Blue, ExpoPalette.Emerald, ExpoPalette.Cyan };
            float t = 0f;
            const float total = 14f;
            float nextFirework = 1.5f;
            while (t < total)
            {
                t += Time.deltaTime;
                float phase = t / 2.4f;
                int a = Mathf.FloorToInt(phase) % seq.Length;
                int b = (a + 1) % seq.Length;
                Color c = Color.Lerp(seq[a], seq[b], Mathf.SmoothStep(0f, 1f, phase - Mathf.Floor(phase)));
                for (int i = 0; i < 3; i++)
                {
                    if (sectorMats[i] == null) continue;
                    sectorMats[i].SetColor(ColorId, ExpoPalette.Hdr(Color.Lerp(ExpoPalette.ForPassport((PassportType)i), c, 0.55f), 1.5f));
                    sectorTarget[i] = 0.5f;
                }
                ExpoMaterials.SetEmission(latticeMat, c, 1.2f + Mathf.Sin(t * 3f) * 0.4f);
                ExpoMaterials.SetEmission(oculusMat, c, 5f);
                ExpoMaterials.SetEmission(crownMat, c, 3.5f);
                if (domeLight != null) { domeLight.color = c; domeLight.intensity = 4f; }
                if (t >= nextFirework)
                {
                    nextFirework = t + UnityEngine.Random.Range(0.18f, 0.45f);
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    float r = UnityEngine.Random.Range(14f, 42f);
                    Vector3 p = new Vector3(Mathf.Cos(ang) * r, UnityEngine.Random.Range(22f, 45f), Mathf.Sin(ang) * r);
                    VfxFactory.Firework(p, seq[UnityEngine.Random.Range(0, seq.Length)]);
                    Game.Audio?.PlaySfxAt("firework", p, 0.6f);
                }
                yield return null;
            }
        }
    }
}
