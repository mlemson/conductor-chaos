// Conductor Chaos — omroep: houd V ingedrukt (of pak de microfoon in de diesel-cabine vast) en je stem klinkt bij de crew
// door de treinluidsprekers.
// Microfoon → 8 kHz mono → mu-law (8 kB/s) → server → andere spelers → bandfilter, vervorming en galm.
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent]
    public sealed class TrainAnnouncer : MonoBehaviour
    {
        private const string UpMessage = "ChaosPA.Up", DownMessage = "ChaosPA.Down";
        private const int Rate = 8000, PacketSamples = 800;     // 100 ms per pakket, past ruim in één UDP-pakket
        private const int RingSize = Rate * 2, Prebuffer = Rate * 15 / 100;

        private NetworkManager registered;
        private string micDevice;
        private AudioClip micClip;
        private int micRead;
        private float decimatePhase, decimateSum;
        private int decimateCount;
        private readonly List<byte> pending = new(PacketSamples);
        private readonly Dictionary<ulong, Voice> voices = new();
        private bool noMicrophone;

        private sealed class Voice
        {
            public AudioSource Source;
            public readonly float[] Ring = new float[RingSize];
            public int Read, Write, Count;
            public bool Primed;
            public float LastHeard = float.NegativeInfinity;

            public void Push(float sample)
            {
                lock (Ring)
                {
                    if (Count == RingSize) { Read = (Read + 1) % RingSize; Count--; }   // te ver achter: oudste weg
                    Ring[Write] = sample;
                    Write = (Write + 1) % RingSize;
                    Count++;
                }
            }

            public void Fill(float[] data)
            {
                lock (Ring)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (!Primed && Count >= Prebuffer) Primed = true;
                        if (Primed && Count > 0)
                        {
                            data[i] = Ring[Read];
                            Read = (Read + 1) % RingSize;
                            Count--;
                        }
                        else
                        {
                            data[i] = 0f;
                            Primed = false;     // leeg: eerst weer even bufferen tegen haperen
                        }
                    }
                }
            }
        }

        private void Update()
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsListening || network.CustomMessagingManager == null)
            {
                registered = null;
                StopMicrophone();
                return;
            }
            if (registered != network)
            {
                registered = network;
                noMicrophone = false;
                network.CustomMessagingManager.RegisterNamedMessageHandler(UpMessage, ReceiveUp);
                network.CustomMessagingManager.RegisterNamedMessageHandler(DownMessage, ReceiveDown);
            }

            var keyboard = Keyboard.current;
            // V ingedrukt, of de microfoon in de diesel-cabine vastgepakt (TrainDrive houdt bij wie hem vastheeft).
            bool wantsTalk = !PlaygroundMenu.BlocksInput && Application.isFocused &&
                ((keyboard != null && keyboard.vKey.isPressed) || TrainDrive.MicrophoneHeldBy(network.LocalClientId));
            if (wantsTalk && !micClip) StartMicrophone();
            else if (!wantsTalk && micClip) StopMicrophone();
            if (micClip) Capture(network);
        }

        private void StartMicrophone()
        {
            if (noMicrophone) return;
            if (Microphone.devices.Length == 0)
            {
                noMicrophone = true;    // pas opnieuw zoeken in een nieuwe sessie; elke frame zoeken hapert
                return;
            }
            micDevice = Microphone.devices[0];
            Microphone.GetDeviceCaps(micDevice, out int min, out int max);
            int frequency = min == 0 && max == 0 ? 16000 : Mathf.Clamp(16000, min, max);
            micClip = Microphone.Start(micDevice, true, 1, frequency);
            micRead = 0;
            decimatePhase = decimateSum = 0f;
            decimateCount = 0;
            pending.Clear();
        }

        private void StopMicrophone()
        {
            if (!micClip) return;
            var network = NetworkManager.Singleton;
            if (network && network.IsListening) Capture(network);
            if (pending.Count > 0 && network && network.IsListening) Send(network, pending.ToArray());
            pending.Clear();
            Microphone.End(micDevice);
            micClip = null;
        }

        private void Capture(NetworkManager network)
        {
            int position = Microphone.GetPosition(micDevice);
            int available = (position - micRead + micClip.samples) % micClip.samples;
            if (available <= 0) return;
            var buffer = new float[available * micClip.channels];
            micClip.GetData(buffer, micRead);       // looping clip: GetData leest over het einde heen door
            micRead = position;

            // Gemiddelde over elk blok = eenvoudig laagdoorlaatfilter, daarna naar 8 kHz.
            float step = micClip.frequency / (float)Rate;
            for (int i = 0; i < available; i++)
            {
                decimateSum += buffer[i * micClip.channels];
                decimateCount++;
                decimatePhase += 1f;
                if (decimatePhase < step) continue;
                decimatePhase -= step;
                pending.Add(MuLawEncode(decimateSum / decimateCount));
                decimateSum = 0f;
                decimateCount = 0;
                if (pending.Count < PacketSamples) continue;
                Send(network, pending.ToArray());
                pending.Clear();
            }
        }

        private void Send(NetworkManager network, byte[] packet)
        {
            if (network.IsServer)
            {
                Relay(network, network.LocalClientId, packet);
                return;
            }
            using var writer = new FastBufferWriter(packet.Length + 4, Allocator.Temp);
            writer.WriteValueSafe((ushort)packet.Length);
            writer.WriteBytesSafe(packet, packet.Length);
            network.CustomMessagingManager.SendNamedMessage(UpMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveUp(ulong sender, FastBufferReader reader)
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsServer || !TryReadPacket(reader, out var packet)) return;
            Relay(network, sender, packet);
        }

        /// <summary>Server: naar iedereen behalve de spreker. Alleen in een sessie zonder anderen hoor je jezelf (om te testen).</summary>
        private void Relay(NetworkManager network, ulong speaker, byte[] packet)
        {
            bool alone = network.ConnectedClientsIds.Count <= 1;
            if (network.IsHost && (speaker != network.LocalClientId || alone)) Play(speaker, packet);
            using var writer = new FastBufferWriter(packet.Length + 12, Allocator.Temp);
            writer.WriteValueSafe(speaker);
            writer.WriteValueSafe((ushort)packet.Length);
            writer.WriteBytesSafe(packet, packet.Length);
            foreach (ulong client in network.ConnectedClientsIds)
                if (client != speaker && client != NetworkManager.ServerClientId)
                    network.CustomMessagingManager.SendNamedMessage(DownMessage, client, writer, NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveDown(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || !reader.TryBeginRead(sizeof(ulong))) return;
            reader.ReadValue(out ulong speaker);
            if (TryReadPacket(reader, out var packet)) Play(speaker, packet);
        }

        private static bool TryReadPacket(FastBufferReader reader, out byte[] packet)
        {
            packet = null;
            if (!reader.TryBeginRead(sizeof(ushort))) return false;
            reader.ReadValue(out ushort length);
            if (length == 0 || length > PacketSamples * 2 || !reader.TryBeginRead(length)) return false;
            packet = new byte[length];
            reader.ReadBytes(ref packet, length);
            return true;
        }

        private void Play(ulong speaker, byte[] packet)
        {
            if (!voices.TryGetValue(speaker, out var voice) || !voice.Source) voices[speaker] = voice = CreateVoice(speaker);
            foreach (byte value in packet)
            {
                float sample = MuLawDecode(value);
                voice.Push((float)Math.Tanh(sample * 2.2f));    // harder en platter, zoals een omroepversterker
            }
            voice.LastHeard = Time.unscaledTime;
        }

        private Voice CreateVoice(ulong speaker)
        {
            var voice = new Voice();
            var holder = new GameObject("Omroep " + Depot.DepotSync.PlayerName(speaker));
            holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.clip = AudioClip.Create("Omroep", Rate, 1, Rate, true, voice.Fill);
            source.loop = true;
            source.spatialBlend = 0f;   // omroep klinkt overal even hard, net als luidsprekers door de hele trein
            source.priority = 32;
            var highPass = holder.AddComponent<AudioHighPassFilter>();
            highPass.cutoffFrequency = 450f;
            highPass.highpassResonanceQ = 1.6f;
            var lowPass = holder.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 3200f;
            lowPass.lowpassResonanceQ = 1.4f;
            holder.AddComponent<AudioDistortionFilter>().distortionLevel = .55f;
            var echo = holder.AddComponent<AudioEchoFilter>();
            echo.delay = 85f;
            echo.decayRatio = .3f;
            echo.wetMix = .35f;
            echo.dryMix = 1f;
            source.Play();
            voice.Source = source;
            return voice;
        }

        private void OnGUI()
        {
            string text = null;
            if (micClip) text = "● Omroep aan: je crew hoort je";
            else if (noMicrophone && Keyboard.current != null && Keyboard.current.vKey.isPressed) text = "Geen microfoon gevonden";
            else
                foreach (var pair in voices)
                    if (Time.unscaledTime - pair.Value.LastHeard < .4f) { text = "Omroep: " + Depot.DepotSync.PlayerName(pair.Key); break; }
            if (text == null) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width / 2f - 220f, 24f, 440f, 44f), text, style);
        }

        private void OnDestroy() => StopMicrophone();

        // G.711 mu-law: 8 bits per sample met genoeg bereik voor spraak.
        private static byte MuLawEncode(float sample)
        {
            int pcm = Mathf.Clamp(Mathf.RoundToInt(sample * 32767f), -32767, 32767);
            int sign = pcm < 0 ? 0x80 : 0;
            if (sign != 0) pcm = -pcm;
            pcm = Math.Min(pcm + 132, 32635);
            int exponent = 7;
            for (int mask = 0x4000; (pcm & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        private static float MuLawDecode(byte value)
        {
            int u = ~value & 0xFF;
            int magnitude = (((u & 0x0F) << 3) + 132) << ((u >> 4) & 0x07);
            magnitude -= 132;
            return ((u & 0x80) != 0 ? -magnitude : magnitude) / 32768f;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void SelfCheck()
        {
            foreach (float sample in new[] { 0f, .01f, -.2f, .5f, -.9f })
                Debug.Assert(Mathf.Abs(MuLawDecode(MuLawEncode(sample)) - sample) < .03f, "TrainAnnouncer: mu-law klopt niet");
        }
#endif
    }
}
