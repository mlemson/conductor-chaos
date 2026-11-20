// Conductor Chaos — het depot in co-op (issue #239). De host kiest; clients zien live wat hij kiest, kunnen een voorstel
// doen en zich klaar melden. Eenvoudige named messages zoals de lobbystatus in PlaygroundHost: de host stuurt de hele
// toestand, clients sturen alleen hun intentie (voorstel / klaar). De server beslist; voorstellen veranderen niets vanzelf.
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

namespace ConductorChaos.Depot
{
    public static class DepotSync
    {
        private const string StateMessage = "Depot.State";
        private const string SuggestMessage = "Depot.Suggest";
        private const string ReadyMessage = "Depot.Ready";

        public const byte SuggestTrain = 0, SuggestWagon = 1, SuggestNone = 255;

        public struct Suggestion
        {
            public ulong Client;
            public byte Type, Value;
        }

        private static NetworkManager registered;
        private static bool dirty;
        private static int lastClientCount = -1;

        /// <summary>Host: het depot is open voor dit level. Clients: wat de host meldt.</summary>
        public static bool IsOpen { get; private set; }
        public static int LevelIndex { get; private set; } = 1;
        public static int Step { get; private set; }
        public static readonly Dictionary<ulong, Suggestion> Suggestions = new();
        public static readonly HashSet<ulong> ReadyClients = new();
        public static bool LocalReady { get; private set; }
        public static event System.Action Changed;

        public static void MarkDirty() => dirty = true;

        public static void Open(int level)
        {
            IsOpen = true;
            LevelIndex = level;
            Step = 0;
            Suggestions.Clear();
            ReadyClients.Clear();
            dirty = true;
            Changed?.Invoke();
        }

        public static void SetStep(int step)
        {
            if (Step == step) return;
            Step = step;
            dirty = true;
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            LocalReady = false;
            dirty = true;
            Changed?.Invoke();
        }

        /// <summary>Clients: stel een loc of wagon voor aan de host.</summary>
        public static void Suggest(byte type, byte value)
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsConnectedClient || network.IsServer) return;
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe(type);
            writer.WriteValueSafe(value);
            network.CustomMessagingManager.SendNamedMessage(SuggestMessage, NetworkManager.ServerClientId, writer);
            Suggestions[network.LocalClientId] = new Suggestion { Client = network.LocalClientId, Type = type, Value = value };
            Changed?.Invoke();
        }

        public static void SetReady(bool ready)
        {
            var network = NetworkManager.Singleton;
            LocalReady = ready;
            if (!network || !network.IsConnectedClient || network.IsServer) { Changed?.Invoke(); return; }
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(ready);
            network.CustomMessagingManager.SendNamedMessage(ReadyMessage, NetworkManager.ServerClientId, writer);
            Changed?.Invoke();
        }

        public static bool TryGetSuggestion(byte type, byte value, out ulong client)
        {
            foreach (var pair in Suggestions)
                if (pair.Value.Type == type && pair.Value.Value == value) { client = pair.Key; return true; }
            client = 0;
            return false;
        }

        public static string PlayerName(ulong client) => $"Speler {client + 1}";

        /// <summary>Elke frame vanuit het menu: registreert de berichten bij een (nieuwe) netwerksessie en verstuurt wijzigingen.</summary>
        public static void Tick()
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsListening || network.CustomMessagingManager == null)
            {
                if (!ReferenceEquals(registered, null))
                {
                    registered = null;
                    Suggestions.Clear();
                    ReadyClients.Clear();
                    LocalReady = false;
                    Close();
                }
                return;
            }
            if (registered != network)
            {
                registered = network;
                lastClientCount = -1;
                var messages = network.CustomMessagingManager;
                messages.RegisterNamedMessageHandler(StateMessage, ReceiveState);
                messages.RegisterNamedMessageHandler(SuggestMessage, ReceiveSuggestion);
                messages.RegisterNamedMessageHandler(ReadyMessage, ReceiveReady);
            }
            if (!network.IsServer) return;
            int count = network.ConnectedClientsIds.Count;
            if (count != lastClientCount)
            {
                lastClientCount = count;
                PruneDisconnected(network);
                dirty = true;
            }
            if (dirty) SendNow();
        }

        /// <summary>Host: stuur de huidige keuze direct (ook vlak voor het laden van een dienst).</summary>
        public static void SendNow()
        {
            dirty = false;
            var network = NetworkManager.Singleton;
            if (!network || !network.IsListening || !network.IsServer || network.CustomMessagingManager == null) return;
            if (registered != network) Tick();
            using var writer = new FastBufferWriter(64 + DepotLoadout.ByteSize + Suggestions.Count * 10 + ReadyClients.Count * 8, Allocator.Temp);
            writer.WriteValueSafe(IsOpen);
            writer.WriteValueSafe((byte)LevelIndex);
            writer.WriteValueSafe((byte)Step);
            DepotLoadout.Selection.Write(writer);
            writer.WriteValueSafe((byte)Suggestions.Count);
            foreach (var pair in Suggestions)
            {
                writer.WriteValueSafe(pair.Key);
                writer.WriteValueSafe(pair.Value.Type);
                writer.WriteValueSafe(pair.Value.Value);
            }
            writer.WriteValueSafe((byte)ReadyClients.Count);
            foreach (ulong client in ReadyClients) writer.WriteValueSafe(client);
            foreach (ulong client in network.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId)
                    network.CustomMessagingManager.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.ReliableFragmentedSequenced);
            Changed?.Invoke();
        }

        private static void PruneDisconnected(NetworkManager network)
        {
            var gone = new List<ulong>();
            foreach (ulong client in Suggestions.Keys) if (!network.ConnectedClients.ContainsKey(client)) gone.Add(client);
            foreach (ulong client in gone) Suggestions.Remove(client);
            ReadyClients.RemoveWhere(client => !network.ConnectedClients.ContainsKey(client));
        }

        private static void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool open);
            reader.ReadValueSafe(out byte level);
            reader.ReadValueSafe(out byte step);
            DepotLoadout.Received = DepotLoadout.Read(reader);
            reader.ReadValueSafe(out byte suggestions);
            Suggestions.Clear();
            for (int i = 0; i < suggestions; i++)
            {
                reader.ReadValueSafe(out ulong client);
                reader.ReadValueSafe(out byte type);
                reader.ReadValueSafe(out byte value);
                Suggestions[client] = new Suggestion { Client = client, Type = type, Value = value };
            }
            reader.ReadValueSafe(out byte ready);
            ReadyClients.Clear();
            for (int i = 0; i < ready; i++)
            {
                reader.ReadValueSafe(out ulong client);
                ReadyClients.Add(client);
            }
            if (!open) LocalReady = false;
            IsOpen = open;
            LevelIndex = level;
            Step = step;
            Changed?.Invoke();
        }

        private static void ReceiveSuggestion(ulong sender, FastBufferReader reader)
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsServer || !IsOpen) return;
            reader.ReadValueSafe(out byte type);
            reader.ReadValueSafe(out byte value);
            if (type == SuggestNone) Suggestions.Remove(sender);
            else if ((type == SuggestTrain && value < DepotCatalog.Trains.Length) || (type == SuggestWagon && value < DepotCatalog.Wagons.Length))
                Suggestions[sender] = new Suggestion { Client = sender, Type = type, Value = value };
            dirty = true;
        }

        private static void ReceiveReady(ulong sender, FastBufferReader reader)
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsServer) return;
            reader.ReadValueSafe(out bool ready);
            if (ready && IsOpen) ReadyClients.Add(sender);
            else ReadyClients.Remove(sender);
            dirty = true;
        }
    }
}
