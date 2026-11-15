using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Client-local soundtrack synchronized to the server-authoritative service timer.
    /// Plays a short lead-in on entering the menu, introduction or service briefing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServiceMusic : MonoBehaviour
    {
        internal const string MusicEnabledKey = "ChaosUI.MusicEnabled";
        internal const string MusicVolumeKey = "ChaosUI.MusicVolume";
        internal const float DefaultMusicVolume = .5f;
        private const float MusicGain = .7f;
        private const float LeadInSeconds = 9f;

        private readonly Dictionary<int, AudioClip> clips = new();
        private readonly HashSet<int> warnedMissing = new();
        private AudioSource source;
        private ServiceRun currentRun;
        private double currentStart;
        private bool wasListenerPaused;
        private Scene previewScene;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = 128;
        }

        private void Update()
        {
            // The existing AudioListener.volume and AudioListener.pause remain master controls.
            float volume = MusicGain * Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
            source.volume = volume;
            source.mute = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 0;
            if (AudioListener.pause) wasListenerPaused = true;

            var run = ServiceRun.Active;
            var network = NetworkManager.Singleton;
            if (run && (run.Phase is ServiceRun.RunPhase.Won or ServiceRun.RunPhase.Failed))
            {
                StopAndReset();
                return;
            }

            if (!run || run.Introduction || !run.TimerRunning)
            {
                PlayLeadIn(run ? run.gameObject.scene : SceneManager.GetActiveScene(), volume);
                return;
            }

            if (!run.IsSpawned || run.MusicDuration <= 0 || !network || !network.IsListening)
            {
                StopAndReset();
                return;
            }

            previewScene = run.gameObject.scene;
            int duration = run.MusicDuration;
            AudioClip clip = GetClip(duration);
            if (!clip)
            {
                StopAndReset();
                return;
            }

            // NGO ServerTime is synchronized. Clients play at the elapsed position,
            // including when they receive the timer a little later than the host.
            double start = run.MusicStartedAt;
            double elapsed = network.ServerTime.Time - start;
            if (elapsed < -.2 || elapsed >= Math.Min(duration, clip.length) - .08)
            {
                StopAndReset();
                return;
            }

            float seek = Mathf.Clamp((float)Math.Max(0d, elapsed), 0f, Mathf.Max(0f, clip.length - .1f));
            if (currentRun != run || Math.Abs(currentStart - start) > .05 || source.clip != clip)
            {
                source.Stop();
                currentRun = run;
                currentStart = start;
                source.clip = clip;
                source.time = seek;
                source.Play();
                return;
            }

            // Re-seek after master mute: the level timer continued while audio was paused.
            // Avoid periodic seeking on compressed MP3s, which can cause audible stutters.
            if (AudioListener.pause) return;
            if (wasListenerPaused)
            {
                wasListenerPaused = false;
                source.time = seek;
            }
            if (!source.isPlaying)
            {
                source.time = seek;
                source.Play();
            }
        }

        private void PlayLeadIn(Scene scene, float volume)
        {
            if (scene.name is not ("TrainPlayground" or ServiceRun.IntroScene or ServiceRun.FirstLevelScene))
            {
                StopAndReset();
                return;
            }

            if (previewScene != scene)
            {
                StopAndReset();
                if (AudioListener.pause) return;
                previewScene = scene;
                source.clip = GetClip(300);
                if (source.clip) source.Play();
            }

            if (!source.clip || AudioListener.pause) return;
            if (!source.isPlaying || source.time >= LeadInSeconds) StopAndReset();
            else source.volume = volume * Mathf.Clamp01((LeadInSeconds - source.time) / 2f);
        }

        private AudioClip GetClip(int duration)
        {
            if (clips.TryGetValue(duration, out var cached)) return cached;
            string name = duration switch
            {
                300 => "RustynailExpress_300",
                270 => "RustynailExpress_270",
                240 => "RustynailExpress_240",
                210 => "RustynailExpress_210",
                _ => null
            };
            if (name == null) return null;

            AudioClip clip = Resources.Load<AudioClip>("Music/" + name);
            clips[duration] = clip;
            if (!clip && warnedMissing.Add(duration))
                Debug.LogWarning($"Levelmuziek ontbreekt: Assets/Resources/Music/{name}.mp3. Kopieer het audiobestand naar die map.", this);
            return clip;
        }

        private void StopAndReset()
        {
            if (source.clip)
            {
                source.Stop();
                source.clip = null;
            }
            currentRun = null;
            currentStart = 0d;
        }

        private void OnDestroy()
        {
            if (source) source.Stop();
        }
    }
}
