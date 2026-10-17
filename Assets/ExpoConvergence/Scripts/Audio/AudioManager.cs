using System.Collections.Generic;
using UnityEngine;

namespace ExpoConvergence
{
    public enum MusicMood { None, Explore, Tension, Finale }

    /// <summary>
    /// Music crossfades by mood, 2D UI sounds, pooled 3D effects, a security alarm loop, footsteps and voice.
    /// Clips load from Resources/ExpoAudio by name; anything missing is silently skipped.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        AudioSource musicA, musicB, ambience, alarm, ui, voice;
        bool musicOnA = true;
        MusicMood mood = MusicMood.None;
        float alarmTarget;
        int stepToggle;

        public float MasterVolume { get; set; } = 1f;

        void Awake()
        {
            Game.Audio = this;
            foreach (var c in Resources.LoadAll<AudioClip>("ExpoAudio")) clips[c.name] = c;
            musicA = Make2D("MusicA", true, 0f);
            musicB = Make2D("MusicB", true, 0f);
            ambience = Make2D("Ambience", true, 0.32f);
            alarm = Make2D("Alarm", true, 0f);
            ui = Make2D("UI", false, 0.8f);
            voice = Make2D("Voice", false, 1f);
            for (int i = 0; i < 18; i++)
            {
                var s = new GameObject("Sfx3D").AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = 38f;
                s.dopplerLevel = 0f;
                pool.Add(s);
            }
            if (clips.TryGetValue("amb_city", out var amb)) { ambience.clip = amb; ambience.Play(); }
            if (clips.TryGetValue("alarm", out var al)) { alarm.clip = al; alarm.Play(); }
        }

        AudioSource Make2D(string name, bool loop, float volume)
        {
            var s = new GameObject(name).AddComponent<AudioSource>();
            s.transform.SetParent(transform, false);
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = volume;
            s.ignoreListenerPause = name == "UI";
            return s;
        }

        AudioClip Clip(string name) => name != null && clips.TryGetValue(name, out var c) ? c : null;

        public void PlayMusic(MusicMood next)
        {
            if (next == mood) return;
            mood = next;
            string clipName = next == MusicMood.Explore ? "music_explore" : next == MusicMood.Tension ? "music_tension" : next == MusicMood.Finale ? "music_finale" : null;
            var incoming = musicOnA ? musicB : musicA;
            musicOnA = !musicOnA;
            var clip = Clip(clipName);
            if (clip != null)
            {
                // Keep the musical position when switching between layered loops of equal length.
                var outgoing = musicOnA ? musicB : musicA;
                incoming.clip = clip;
                incoming.loop = next != MusicMood.Finale;
                if (outgoing.clip != null && outgoing.isPlaying && outgoing.clip.length == clip.length && next != MusicMood.Finale)
                    incoming.timeSamples = Mathf.Min(outgoing.timeSamples, clip.samples - 1);
                else incoming.time = 0f;
                incoming.Play();
            }
            else incoming.Stop();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float musicLevel = mood == MusicMood.Finale ? 0.85f : 0.5f;
            var current = musicOnA ? musicA : musicB;
            var other = musicOnA ? musicB : musicA;
            current.volume = Mathf.MoveTowards(current.volume, mood == MusicMood.None ? 0f : musicLevel * MasterVolume, dt * 0.6f);
            other.volume = Mathf.MoveTowards(other.volume, 0f, dt * 0.6f);
            if (other.volume <= 0f && other.isPlaying) other.Stop();
            alarm.volume = Mathf.MoveTowards(alarm.volume, alarmTarget * 0.42f * MasterVolume, dt * 1.5f);
            bool paused = Game.State != null && Game.State.Current == GameState.Paused;
            AudioListener.pause = paused;
        }

        public void SetAlarm(bool on) => alarmTarget = on ? 1f : 0f;

        public void PlayUI(string name)
        {
            var c = Clip(name);
            if (c != null) ui.PlayOneShot(c, MasterVolume);
        }

        public void PlaySfx(string name, float volume = 1f)
        {
            var c = Clip(name);
            if (c != null) ui.PlayOneShot(c, volume * MasterVolume);
        }

        public void PlayVoice(string name)
        {
            var c = Clip(name);
            if (c == null) return;
            voice.Stop();
            voice.clip = c;
            voice.volume = MasterVolume;
            voice.Play();
        }

        public void PlaySfxAt(string name, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var c = Clip(name);
            if (c == null) return;
            AudioSource free = null;
            foreach (var s in pool) if (!s.isPlaying) { free = s; break; }
            if (free == null) free = pool[0];
            free.transform.position = position;
            free.clip = c;
            free.volume = volume * MasterVolume;
            free.pitch = pitch;
            free.Play();
        }

        public void PlayFootstep(Vector3 position, bool sprint)
        {
            stepToggle++;
            string name = (stepToggle & 1) == 0 ? "step_a" : "step_b";
            PlaySfxAt(name, position, sprint ? 0.55f : 0.32f, Random.Range(0.92f, 1.08f));
        }

        public void StartLoop(string name, Vector3 position)
        {
            var c = Clip(name);
            if (c == null) return;
            if (!loops.TryGetValue(name, out var s) || s == null)
            {
                s = new GameObject("Loop_" + name).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.loop = true;
                s.spatialBlend = 0.8f;
                s.maxDistance = 25f;
                s.rolloffMode = AudioRolloffMode.Linear;
                loops[name] = s;
            }
            s.transform.position = position;
            s.clip = c;
            s.volume = 0.6f * MasterVolume;
            if (!s.isPlaying) s.Play();
        }

        public void StopLoop(string name)
        {
            if (loops.TryGetValue(name, out var s) && s != null) s.Stop();
        }
    }
}
