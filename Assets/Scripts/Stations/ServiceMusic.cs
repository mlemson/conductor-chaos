using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Client-local soundtrack. Menu, introduction and briefing use a continuous
    /// looping theme. Timed levels use the server-selected Rustynail Express version,
    /// followed by Chug Chug Along after that clip has finished.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServiceMusic : MonoBehaviour
    {
        internal const string MusicEnabledKey = "ChaosUI.MusicEnabled";
        internal const string MusicVolumeKey = "ChaosUI.MusicVolume";
        internal const float DefaultMusicVolume = .5f;

        private const float MusicGain = .7f;
        private const string MenuTrackName = "RustynailExpress2";
        private const string FollowupTrackName = "ChugChugAlong";

        private readonly Dictionary<string, AudioClip> clips = new();
        private readonly HashSet<string> warnedMissing = new();
        private AudioSource source;
        private ServiceRun currentRun;
        private double currentStart;
        private bool wasListenerPaused;

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
            // Master volume and master pause remain controlled by PlaygroundMenu.
            source.volume = MusicGain * Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
            source.mute = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 0;
            if (AudioListener.pause) wasListenerPaused = true;

            ServiceRun run = ServiceRun.Active;
            string sceneName = run ? run.gameObject.scene.name : SceneManager.GetActiveScene().name;
            if (sceneName != "TrainPlayground" && !ServiceRun.IsServiceScene(sceneName))
            {
                StopAndReset();
                return;
            }

            // Keep the same intro theme playing across menu, loading, training and
            // briefing scene changes. The actual level begins at the server timer.
            if (!run || run.Introduction || !run.TimerRunning)
            {
                PlayMenuTheme();
                return;
            }

            NetworkManager network = NetworkManager.Singleton;
            if (!run.IsSpawned || run.MusicDuration <= 0 || !network || !network.IsListening)
            {
                PlayMenuTheme();
                return;
            }

            // Langere levels (level 2: 7-10 min) hebben geen eigen versie: dan de 5-minutenversie, daarna de vervolgsong.
            AudioClip levelClip = GetLevelClip(run.MusicDuration);
            if (!levelClip && run.MusicDuration > 300) levelClip = GetLevelClip(300);
            if (!levelClip)
            {
                StopAndReset();
                return;
            }

            // ServerTime lets late-joining clients seek to the matching point.
            // Do not stop at the deadline: result screens can outlive the timer
            // and should reach the next song when Rustynail Express finishes.
            double start = run.MusicStartedAt;
            double elapsed = network.ServerTime.Time - start;
            if (elapsed < 0d)
            {
                PlayMenuTheme();
                return;
            }

            if (elapsed < levelClip.length)
            {
                PlaySynchronized(run, start, levelClip, false,
                    Mathf.Clamp((float)elapsed, 0f, Mathf.Max(0f, levelClip.length - .01f)));
                return;
            }

            AudioClip followup = LoadClip(FollowupTrackName);
            if (!followup || followup.length <= 0f)
            {
                StopAndReset();
                return;
            }

            // Loop the follow-up until the player leaves/restarts the level. Using
            // server time also gives late clients the same position in this song.
            float followupTime = (float)((elapsed - levelClip.length) % followup.length);
            PlaySynchronized(run, start, followup, true, followupTime);
        }

        private void PlayMenuTheme()
        {
            AudioClip menuClip = LoadClip(MenuTrackName);
            if (!menuClip)
            {
                StopAndReset();
                return;
            }

            if (currentRun || source.clip != menuClip || !source.loop)
            {
                source.Stop();
                currentRun = null;
                currentStart = 0d;
                source.clip = menuClip;
                source.loop = true;
                if (!AudioListener.pause) source.Play();
                return;
            }

            // The theme does not have a nine-second fade-out. Let the full song
            // play and loop naturally until the level timer actually starts.
            if (!AudioListener.pause && !source.isPlaying) source.Play();
        }

        private void PlaySynchronized(ServiceRun run, double start, AudioClip clip, bool loop, float seek)
        {
            bool changed = currentRun != run || Math.Abs(currentStart - start) > .05 ||
                           source.clip != clip || source.loop != loop;
            if (changed)
            {
                source.Stop();
                currentRun = run;
                currentStart = start;
                source.clip = clip;
                source.loop = loop;
                source.time = seek;
                if (!AudioListener.pause) source.Play();
                return;
            }

            if (AudioListener.pause) return;
            if (wasListenerPaused)
            {
                wasListenerPaused = false;
                source.time = seek;
            }

            // Avoid repeated seeking while a compressed MP3 plays normally.
            if (!source.isPlaying)
            {
                source.time = seek;
                source.Play();
            }
        }

        private AudioClip GetLevelClip(int duration)
        {
            string name = duration switch
            {
                300 => "RustynailExpress_300",
                270 => "RustynailExpress_270",
                240 => "RustynailExpress_240",
                210 => "RustynailExpress_210",
                _ => null
            };
            return name == null ? null : LoadClip(name);
        }

        private AudioClip LoadClip(string name)
        {
            if (clips.TryGetValue(name, out AudioClip cached)) return cached;

            AudioClip clip = Resources.Load<AudioClip>("Music/" + name);
            clips[name] = clip;
            if (!clip && warnedMissing.Add(name))
                Debug.LogWarning($"Muziek ontbreekt: Assets/Resources/Music/{name}.mp3. Voeg het audiobestand toe aan het Unity-project.", this);
            return clip;
        }

        private void StopAndReset()
        {
            if (source.clip)
            {
                source.Stop();
                source.clip = null;
            }

            source.loop = false;
            currentRun = null;
            currentStart = 0d;
            wasListenerPaused = false;
        }

        private void OnDestroy()
        {
            if (source) source.Stop();
        }
    }
}
