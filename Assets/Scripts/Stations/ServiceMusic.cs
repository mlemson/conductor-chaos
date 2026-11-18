using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Stations
{
    [DisallowMultipleComponent]
    public sealed class ServiceMusic : MonoBehaviour
    {
        internal const string MusicEnabledKey = "ChaosUI.MusicEnabled";
        internal const string MusicVolumeKey = "ChaosUI.MusicVolume";
        internal const float DefaultMusicVolume = .5f;

        private const float MusicGain = .7f;
        private const string Theme = "RustynailExpress2";
        private const string Results = "ChugChugAlong";
        private const float StartDelay = .65f, StartTempo = .87f, EndTempo = 1.18f;
        // Matching phrases in the source recording. The last minute is outside the loop.
        private const float LoopFrom = 42.690f, LoopAt = 147.841f, FadeLength = .64f;

        private readonly Dictionary<string, AudioClip> clips = new();
        private AudioSource source, incoming;
        private ServiceRun currentRun;
        private double currentStart;
        private bool fading, wasPaused;
        private float voiceStarted = float.NegativeInfinity;

        private void Awake()
        {
            source = AddVoice();
            incoming = AddVoice();
            WarmUp(Theme);
            WarmUp(Results);
        }

        private AudioSource AddVoice()
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.priority = 128;
            return voice;
        }

        private void Update()
        {
            float volume = MusicGain * Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
            source.mute = incoming.mute = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 0;
            if (AudioListener.pause)
            {
                wasPaused = true;
                return;
            }

            var run = ServiceRun.Active;
            string sceneName = run ? run.gameObject.scene.name : SceneManager.GetActiveScene().name;
            if (sceneName != "TrainPlayground" && !ServiceRun.IsServiceScene(sceneName))
            {
                StopVoices();
                currentRun = null;
                return;
            }
            var network = NetworkManager.Singleton;
            if (!run || run.Introduction || !run.TimerRunning || !run.IsSpawned || !network || !network.IsListening ||
                run.MusicDuration <= 0)
            {
                PlayFull(ReadyClip(Theme), volume);
                return;
            }
            if (run.Phase is ServiceRun.RunPhase.Won or ServiceRun.RunPhase.Failed)
            {
                PlayFull(ReadyClip(Results), volume);
                return;
            }

            var theme = ReadyClip(Theme);
            if (!theme) return;
            double elapsed = network.ServerTime.Time - run.MusicStartedAt;
            if (elapsed < StartDelay)
            {
                StopVoices();
                currentRun = null;
                return;
            }
            float tempo = Mathf.Lerp(StartTempo, EndTempo, 1f - run.TimeLeft / (float)run.MusicDuration);
            PlayTimed(run, theme, elapsed - StartDelay, tempo, volume);
        }

        private void PlayTimed(ServiceRun run, AudioClip clip, double elapsed, float tempo, float volume)
        {
            bool hasLoop = clip.length > LoopAt + FadeLength;
            if (currentRun != run || Math.Abs(currentStart - run.MusicStartedAt) > .05 || wasPaused || Stalled)
            {
                voiceStarted = Time.unscaledTime;
                StopVoices();
                currentRun = run;
                currentStart = run.MusicStartedAt;
                wasPaused = false;
                double position = MusicalTime(elapsed, run.MusicDuration);
                float seek = hasLoop ? LoopPosition(position) : (float)(position % clip.length);
                StartVoice(source, clip, seek, tempo, volume, !hasLoop);
            }

            source.pitch = incoming.pitch = tempo;
            float positionInClip = source.timeSamples / (float)clip.frequency;
            if (hasLoop && positionInClip >= LoopAt && !fading)
            {
                StartVoice(incoming, clip, LoopFrom + positionInClip - LoopAt, tempo, 0f);
                fading = true;
            }
            if (!fading)
            {
                source.volume = volume;
                return;
            }

            float fade = Mathf.Clamp01((positionInClip - LoopAt) / FadeLength);
            source.volume = volume * Mathf.Sqrt(1f - fade);
            incoming.volume = volume * Mathf.Sqrt(fade);
            if (fade < 1f) return;
            source.Stop();
            (source, incoming) = (incoming, source);
            fading = false;
        }

        private static double MusicalTime(double elapsed, int limit)
        {
            double ramp = Math.Max(1d, limit - StartDelay);
            double within = Math.Min(elapsed, ramp);
            return within * (StartTempo + (EndTempo - StartTempo) * within / (2d * ramp)) + (elapsed - within) * EndTempo;
        }

        private static float LoopPosition(double position) => position < LoopAt
            ? (float)position
            : LoopFrom + (float)((position - LoopAt) % (LoopAt - LoopFrom));

        private void PlayFull(AudioClip clip, float volume)
        {
            if (!clip) return;
            if (currentRun || source.clip != clip || !source.loop || Stalled)
            {
                voiceStarted = Time.unscaledTime;
                StopVoices();
                StartVoice(source, clip, 0f, 1f, volume, true);
            }
            currentRun = null;
            wasPaused = false;
            source.volume = volume;
        }

        private static void StartVoice(AudioSource voice, AudioClip clip, float seek, float pitch, float volume, bool loop = false)
        {
            voice.clip = clip;
            voice.loop = loop;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.timeSamples = Mathf.Clamp(Mathf.RoundToInt(seek * clip.frequency), 0, clip.samples - 1);
            voice.Play();
        }

        private AudioClip ReadyClip(string name)
        {
            if (!clips.TryGetValue(name, out var clip) || !clip) return null;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return clip.loadState == AudioDataLoadState.Loaded && clip.samples > 0 ? clip : null;
        }

        // isPlaying can lag a few frames after Play(); restarting on every such frame kept the music silent.
        private bool Stalled => !source.isPlaying && Time.unscaledTime - voiceStarted > 1f;

        private void WarmUp(string name)
        {
            var request = Resources.LoadAsync<AudioClip>("Music/" + name);
            request.completed += _ =>
            {
                if (!this || request.asset is not AudioClip clip) return;
                clips[name] = clip;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            };
        }

        private void StopVoices()
        {
            if (source) source.Stop();
            if (incoming) incoming.Stop();
            fading = false;
        }

        private void OnDestroy() => StopVoices();
    }
}
