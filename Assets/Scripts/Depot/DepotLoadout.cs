// Conductor Chaos — de samenstelling voor een dienst (issue #239): welke loc en welke wagons in welke volgorde.
// De host kiest; de server zet precies dit treinstel op het vertrekspoor. Clients krijgen dezelfde keuze via DepotSync,
// zodat ze bij het laden van de scène dezelfde loc tonen.
using System.Collections.Generic;
using Unity.Netcode;

namespace ConductorChaos.Depot
{
    public sealed class DepotLoadout
    {
        public TrainKind Train = TrainKind.Steam;
        public readonly List<WagonKind> Wagons = new();

        /// <summary>De keuze die de host in het depot maakt (alleen lokaal).</summary>
        public static DepotLoadout Selection
        {
            get => selection ??= DepotProgress.LoadLastLoadout().Sanitized(true);
            private set => selection = value;
        }
        private static DepotLoadout selection;
        /// <summary>De keuze zoals de host die doorstuurde (clients).</summary>
        public static DepotLoadout Received { get; internal set; } = new DepotLoadout().Sanitized(false);

        /// <summary>De loadout voor de geladen dienst: de host gebruikt zijn eigen keuze, clients wat de host stuurde.</summary>
        public static DepotLoadout Active
        {
            get
            {
                var network = NetworkManager.Singleton;
                return network && network.IsListening && !network.IsServer ? Received : Selection;
            }
        }

        public static void SetSelection(DepotLoadout loadout)
        {
            Selection = loadout.Sanitized(true);
            DepotProgress.SaveLastLoadout(Selection);
            DepotSync.MarkDirty();
        }

        public DepotLoadout Clone()
        {
            var copy = new DepotLoadout { Train = Train };
            copy.Wagons.AddRange(Wagons);
            return copy;
        }

        public int MaxWagons => DepotCatalog.Train(Train).MaxWagons;

        /// <summary>Maakt de samenstelling geldig: loc en wagons in bezit (alleen als checkOwnership), set-rijtuigen achter hun
        /// eigen loc, hooguit één regionaal rijtuig en niet meer dan de loc trekt.</summary>
        public DepotLoadout Sanitized(bool checkOwnership)
        {
            var result = new DepotLoadout { Train = Train };
            if (checkOwnership && !DepotProgress.Owns(result.Train)) result.Train = TrainKind.Steam;
            foreach (var saved in Wagons)
            {
                // Oude samenstellingen met het rijtuig zonder deuren krijgen het standaard dieselrijtuig.
                var wagon = saved == WagonKind.DieselCoach ? WagonKind.DieselCoachDoors : saved;
                if (!DepotCatalog.Fits(wagon, result.Train) || DepotCatalog.Wagon(wagon).Hidden) continue;
                if (checkOwnership && !DepotProgress.Owns(wagon)) continue;
                if (DepotCatalog.IsUnique(wagon) && result.Wagons.Contains(wagon)) continue;
                result.Wagons.Add(wagon);
            }
            int max = result.MaxWagons;
            if (result.Wagons.Count > max) result.Wagons.RemoveRange(max, result.Wagons.Count - max);
            return result;
        }

        public void Write(FastBufferWriter writer)
        {
            writer.WriteValueSafe((byte)Train);
            writer.WriteValueSafe((byte)Wagons.Count);
            foreach (var wagon in Wagons) writer.WriteValueSafe((byte)wagon);
        }

        public static DepotLoadout Read(FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte train);
            reader.ReadValueSafe(out byte count);
            var loadout = new DepotLoadout { Train = (TrainKind)System.Math.Min((int)train, 3) };
            for (int i = 0; i < count && i < 8; i++)
            {
                reader.ReadValueSafe(out byte wagon);
                if (wagon < DepotCatalog.Wagons.Length) loadout.Wagons.Add((WagonKind)wagon);
            }
            return loadout.Sanitized(false);
        }

        public bool SameAs(DepotLoadout other)
        {
            if (other == null || other.Train != Train || other.Wagons.Count != Wagons.Count) return false;
            for (int i = 0; i < Wagons.Count; i++) if (Wagons[i] != other.Wagons[i]) return false;
            return true;
        }

        public const int ByteSize = 2 + 8;
    }
}
