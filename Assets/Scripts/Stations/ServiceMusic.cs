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

            WarmUp(MenuTrackName);
            WarmUp(FollowupTrackName);
            foreach (int duration in new[] { 210, 240, 270, 300 }) WarmUp(LevelTrackName(duration));
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
            AudioClip levelClip = ReadyClip(LevelTrackName(run.MusicDuration));
            if (!levelClip)
            {
                if (!currentRun) PlayMenuTheme();
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

            AudioClip followup = ReadyClip(FollowupTrackName);
            if (!followup) return;

            // Loop the follow-up until the player leaves/restarts the level. Using
            // server time also gives late clients the same position in this song.
            float followupTime = (float)((elapsed - levelClip.length) % followup.length);
            PlaySynchronized(run, start, followup, true, followupTime);
        }

        private void PlayMenuTheme()
        {
            AudioClip menuClip = ReadyClip(MenuTrackName);
            if (!menuClip) return;

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
                if (seek > .15f) source.time = seek;
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

        private static string LevelTrackName(int duration) => duration is 210 or 240 or 270
            ? $"RustynailExpress_{duration}" : "RustynailExpress_300";

        private AudioClip ReadyClip(string name) => clips.TryGetValue(name, out var clip) &&
            clip && clip.loadState == AudioDataLoadState.Loaded && clip.length > 0f ? clip : null;

        private void WarmUp(string name)
        {
            var request = Resources.LoadAsync<AudioClip>("Music/" + name);
            request.completed += _ =>
            {
                if (!this) return;
                if (request.asset is not AudioClip clip)
                {
                    Debug.LogWarning($"Muziek ontbreekt: Assets/Resources/Music/{name}.mp3.", this);
                    return;
                }
                clips[name] = clip;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            };
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
