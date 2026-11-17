// Conductor Chaos — depotvoortgang per speler (issue #239): kaartjes, gehaalde levels, gekochte treinen/wagons
// en de laatst gekozen samenstelling. Lokaal in PlayerPrefs; elke speler spaart in zijn eigen profiel.
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Depot
{
    public sealed class RunReward
    {
        public int Base, TimeBonus, ObjectiveBonus, RepairBonus, PassengerBonus;
        public int SecondsLeft, ObjectivesDone, ExtraPassengers;
        public int Total => Base + TimeBonus + ObjectiveBonus + RepairBonus + PassengerBonus;
        public int BalanceBefore, BalanceAfter;
        public bool FirstWin;
        public readonly List<TrainKind> NewTrains = new();
        public readonly List<WagonKind> NewWagons = new();
    }

    public static class DepotProgress
    {
        private const string Prefix = "Depot.";
        public const int TicketsPerWin = 300, TicketsPerIntro = 100, TicketsPerSecond = 2, TicketsPerObjective = 20, TicketsForRepairs = 40;
        public const int TicketsPerExtraPassenger = 15;

        public static event System.Action Changed;

        public static int Tickets
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "Tickets", 0));
            private set { PlayerPrefs.SetInt(Prefix + "Tickets", Mathf.Max(0, value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool LevelWon(int level) => level <= 0 || PlayerPrefs.GetInt(Prefix + "Won." + level, 0) != 0;
        public static bool LevelEverWon(int level) => PlayerPrefs.GetInt(Prefix + "Won." + level, 0) != 0;
        public static int BestSecondsLeft(int level) => PlayerPrefs.GetInt(Prefix + "Best." + level, -1);

        public static bool Owns(TrainKind train)
        {
            var info = DepotCatalog.Train(train);
            return LevelWon(info.UnlockLevel) || PlayerPrefs.GetInt(Prefix + "Train." + train, 0) != 0;
        }

        public static bool Owns(WagonKind wagon)
        {
            var info = DepotCatalog.Wagon(wagon);
            if (info.HasSet) return Owns(info.Set);
            return LevelWon(info.UnlockLevel) || PlayerPrefs.GetInt(Prefix + "Wagon." + wagon, 0) != 0;
        }

        /// <summary>Net vrijgekomen door een level en nog niet in het depot bekeken.</summary>
        public static bool IsNew(TrainKind train) => PlayerPrefs.GetInt(Prefix + "New.Train." + train, 0) != 0;
        public static void MarkSeen(TrainKind train) { if (IsNew(train)) { PlayerPrefs.DeleteKey(Prefix + "New.Train." + train); PlayerPrefs.Save(); } }

        public static bool CanAfford(int cost) => Tickets >= cost;

        public static bool Buy(TrainKind train)
        {
            var info = DepotCatalog.Train(train);
            if (Owns(train) || !CanAfford(info.Cost)) return false;
            PlayerPrefs.SetInt(Prefix + "Train." + train, 1);
            Tickets -= info.Cost;
            return true;
        }

        public static bool Buy(WagonKind wagon)
        {
            var info = DepotCatalog.Wagon(wagon);
            if (info.HasSet || Owns(wagon) || !CanAfford(info.Cost)) return false;
            PlayerPrefs.SetInt(Prefix + "Wagon." + wagon, 1);
            Tickets -= info.Cost;
            return true;
        }

        /// <summary>Kaartjes voor een gehaalde dienst. Per run één keer aanroepen (op elke client voor zijn eigen profiel).</summary>
        public static RunReward AwardWin(int level, int secondsLeft, int objectivesDone, bool allRepaired, int extraPassengers = 0)
        {
            var reward = new RunReward
            {
                SecondsLeft = Mathf.Max(0, secondsLeft),
                ObjectivesDone = Mathf.Max(0, objectivesDone),
                BalanceBefore = Tickets,
                FirstWin = !LevelEverWon(level)
            };
            if (level <= 0)
            {
                reward.Base = TicketsPerIntro;
                reward.ObjectiveBonus = reward.ObjectivesDone * TicketsPerObjective;
            }
            else
            {
                reward.Base = TicketsPerWin;
                reward.TimeBonus = reward.SecondsLeft * TicketsPerSecond;
                reward.ObjectiveBonus = reward.ObjectivesDone * TicketsPerObjective;
                reward.RepairBonus = allRepaired ? TicketsForRepairs : 0;
                reward.ExtraPassengers = Mathf.Max(0, extraPassengers);
                reward.PassengerBonus = reward.ExtraPassengers * TicketsPerExtraPassenger;
            }

            var trainsBefore = new List<TrainKind>();
            var wagonsBefore = new List<WagonKind>();
            foreach (var train in DepotCatalog.Trains) if (Owns(train.Kind)) trainsBefore.Add(train.Kind);
            foreach (var wagon in DepotCatalog.Wagons) if (Owns(wagon.Kind)) wagonsBefore.Add(wagon.Kind);

            if (level > 0)
            {
                PlayerPrefs.SetInt(Prefix + "Won." + level, 1);
                if (reward.SecondsLeft > BestSecondsLeft(level)) PlayerPrefs.SetInt(Prefix + "Best." + level, reward.SecondsLeft);
            }
            foreach (var train in DepotCatalog.Trains)
                if (!trainsBefore.Contains(train.Kind) && Owns(train.Kind))
                {
                    reward.NewTrains.Add(train.Kind);
                    PlayerPrefs.SetInt(Prefix + "New.Train." + train.Kind, 1);
                }
            foreach (var wagon in DepotCatalog.Wagons)
                if (!wagonsBefore.Contains(wagon.Kind) && Owns(wagon.Kind) && !wagon.HasSet) reward.NewWagons.Add(wagon.Kind);

            Tickets = reward.BalanceBefore + reward.Total;
            reward.BalanceAfter = Tickets;
            return reward;
        }

        public static DepotLoadout LoadLastLoadout()
        {
            var loadout = new DepotLoadout { Train = (TrainKind)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Last.Train", 0), 0, 3) };
            if (!PlayerPrefs.HasKey(Prefix + "Last.Wagons")) loadout.Wagons.AddRange(DepotCatalog.StarterWagons);
            string wagons = PlayerPrefs.GetString(Prefix + "Last.Wagons", "");
            foreach (string part in wagons.Split(','))
                if (byte.TryParse(part, out byte value) && value < DepotCatalog.Wagons.Length) loadout.Wagons.Add((WagonKind)value);
            return loadout.Sanitized(false);
        }

        public static void SaveLastLoadout(DepotLoadout loadout)
        {
            PlayerPrefs.SetInt(Prefix + "Last.Train", (int)loadout.Train);
            var parts = new List<string>();
            foreach (var wagon in loadout.Wagons) parts.Add(((byte)wagon).ToString());
            PlayerPrefs.SetString(Prefix + "Last.Wagons", string.Join(",", parts));
            PlayerPrefs.Save();
        }

        /// <summary>Voor de unlock-test in de editor: alles terug naar een nieuw profiel.</summary>
        public static void ResetProgress()
        {
            foreach (var train in DepotCatalog.Trains)
            {
                PlayerPrefs.DeleteKey(Prefix + "Train." + train.Kind);
                PlayerPrefs.DeleteKey(Prefix + "New.Train." + train.Kind);
            }
            foreach (var wagon in DepotCatalog.Wagons) PlayerPrefs.DeleteKey(Prefix + "Wagon." + wagon.Kind);
            for (int level = 1; level < DepotCatalog.Levels.Length; level++)
            {
                PlayerPrefs.DeleteKey(Prefix + "Won." + level);
                PlayerPrefs.DeleteKey(Prefix + "Best." + level);
            }
            PlayerPrefs.DeleteKey(Prefix + "Last.Train");
            PlayerPrefs.DeleteKey(Prefix + "Last.Wagons");
            Tickets = 0;
        }

        internal static void NotifyChanged() => Changed?.Invoke();
    }
}
