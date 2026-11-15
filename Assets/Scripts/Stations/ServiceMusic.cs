using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Client-local soundtrack synchronized to the server-authoritative service timer.
    /// No music is played during the lobby, introduction or briefing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServiceMusic : MonoBehaviour
    {
        internal const string MusicEnabledKey = "ChaosUI.MusicEnabled";
        internal const string MusicVolumeKey = "ChaosUI.MusicVolume";

        private readonly Dictionary<int, AudioClip> clips = new();
        private readonly HashSet<int> warnedMissing = new();
        private AudioSource source;
        private ServiceRun currentRun;
        private double currentStart;
        private float nextSyncCheck;

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
            source.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, .65f));
            source.mute = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 0;

            var run = ServiceRun.Active;
            var network = NetworkManager.Singleton;
            if (!run || !run.IsSpawned || run.Introduction || !run.TimerRunning ||
                run.Phase is not (ServiceRun.RunPhase.Departure or ServiceRun.RunPhase.Travelling or ServiceRun.RunPhase.Arrival) ||
                run.MusicDuration <= 0 || !network || !network.IsListening)
            {
                StopAndReset();
                return;
            }

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
                nextSyncCheck = Time.unscaledTime + 2f;
                return;
            }

            // When master audio is re-enabled, resync to the current server timer.
            if (AudioListener.pause) return;
            if (!source.isPlaying)
            {
                source.time = seek;
                source.Play();
                return;
            }

            if (Time.unscaledTime < nextSyncCheck) return;
            nextSyncCheck = Time.unscaledTime + 2f;
            if (Mathf.Abs(source.time - seek) > .75f)
                source.time = seek;
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
