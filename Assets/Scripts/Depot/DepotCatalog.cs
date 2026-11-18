// Conductor Chaos — depot: welke treinen en wagons er zijn, wat ze kunnen en hoe ze vrijkomen (issue #239).
// Alleen vaste gegevens; voortgang staat in DepotProgress, de keuze voor een dienst in DepotLoadout.
using System.Collections.Generic;
using ConductorChaos.Stations;

namespace ConductorChaos.Depot
{
    public enum TrainKind : byte { Steam, Diesel, Modern, Futuristic }

    public enum WagonKind : byte { Flatbed, Regional, Cargo, Cattle, Tank, DieselCoach, ModernCoach, FuturisticCoach, DieselCoachDoors }

    public sealed class TrainInfo
    {
        public TrainKind Kind;
        public string Name, Era, Short, Description, SpecialTitle, SpecialText, SpecialIcon, Sprite;
        public int Speed, Acceleration, MaxWagons, Cost;
        public float MaximumSpeed, AccelerationRate;
        public string[] Breakdowns;
        public WagonKind SetCoach;
    }

    public sealed class WagonInfo
    {
        public WagonKind Kind;
        public string Name, Capacity, Sprite, Category;
        public int Seats, Crates, Animals, Tanks, Cost, Weight;
        public bool HasSet, Hidden;   // Hidden: model blijft bestaan, maar niet in de shop of samenstelling
        public TrainKind Set;
        public string[] Breakdowns;
    }

    public sealed class LevelInfo
    {
        public int Index;
        public string Scene, Label, Name, Summary, Distance, Photo;
        public int Difficulty;
        public string[] Highlights;
        public bool UsesDepot => Index > 0;
    }

    public static class DepotCatalog
    {
        public const string CategoryPassengers = "Passagiers";
        public const string CategoryGoods = "Goederen";
        public const string CategoryAnimals = "Dieren";
        public const string CategoryLiquid = "Vloeistof";

        public static readonly TrainInfo[] Trains =
        {
            new TrainInfo
            {
                Kind = TrainKind.Steam, Name = "Stoomloc", Era = "Stoom · loc met kolenwagen", Short = "Stoom · startloc",
                Description = "De trouwe rode stoomloc waar elke crew mee begint. Traag, maar vergevingsgezind.",
                SpecialTitle = "Kolen scheppen", SpecialText = "Houd de ketel bij vanaf de kolenwagen, anders zakt de druk.", SpecialIcon = "coal",
                Speed = 2, Acceleration = 2, MaxWagons = 3, Cost = 0, MaximumSpeed = 6f, AccelerationRate = 2f,
                Breakdowns = new[] { "Stoomleiding" }, Sprite = "steam", SetCoach = WagonKind.Regional
            },
            new TrainInfo
            {
                Kind = TrainKind.Diesel, Name = "Diesel '60", Era = "Jaren 60 · loc met dieselrijtuig", Short = "Jaren 60",
                Description = "Geen kolen meer, wel een motor die warm draait. Cabinedeur aan beide kanten.",
                SpecialTitle = "Dieseltank", SpecialText = "De tank raakt leeg: neem jerrycans mee of tank bij aan de pomp op het perron.", SpecialIcon = "speed-gauge",
                Speed = 3, Acceleration = 3, MaxWagons = 4, Cost = 1000, MaximumSpeed = 7f, AccelerationRate = 2.4f,
                Breakdowns = new[] { "Dakventilator", "Motorluik", "Aspot", "Remslang" }, Sprite = "diesel", SetCoach = WagonKind.DieselCoachDoors
            },
            new TrainInfo
            {
                Kind = TrainKind.Modern, Name = "Modern Express", Era = "2020 · stuurstand met modern rijtuig", Short = "2020",
                Description = "Snel en elektrisch, met brede schuifdeuren. Meer wagons, meer om in de gaten te houden.",
                SpecialTitle = "Stroomafnemer", SpecialText = "Valt de pantograaf uit, dan rol je uit.", SpecialIcon = "speed-gauge",
                Speed = 4, Acceleration = 4, MaxWagons = 5, Cost = 2500, MaximumSpeed = 8f, AccelerationRate = 2.8f,
                Breakdowns = new[] { "Stroomafnemer", "Omvormer", "Aspot", "Remslang" }, Sprite = "modern", SetCoach = WagonKind.ModernCoach
            },
            new TrainInfo
            {
                Kind = TrainKind.Futuristic, Name = "Futuristic Express", Era = "2040 · stuurstand met futuristisch rijtuig", Short = "2040",
                Description = "Het snelste wat het depot heeft. Automatische deuren en een gloeiende energiecel.",
                SpecialTitle = "Energiecel", SpecialText = "Laat de energiecel niet oververhitten.", SpecialIcon = "speed-gauge",
                Speed = 5, Acceleration = 5, MaxWagons = 6, Cost = 6000, MaximumSpeed = 9f, AccelerationRate = 3.2f,
                Breakdowns = new[] { "Stroomafnemer", "Energiecel", "Aspot", "Remslang" }, Sprite = "futur", SetCoach = WagonKind.FuturisticCoach
            },
        };

        public static readonly WagonInfo[] Wagons =
        {
            new WagonInfo { Kind = WagonKind.Regional, Name = "Regionale wagon", Capacity = "Hier zitten de reizigers met kaartjes", Category = CategoryPassengers,
                Seats = 8, Sprite = "regional", Weight = 1, Breakdowns = new string[0] },
            new WagonInfo { Kind = WagonKind.Flatbed, Name = "Open goederenwagon", Capacity = "Kratten in de open bak", Category = CategoryGoods,
                Crates = 4, Sprite = "flatbed", Weight = 1, Breakdowns = new string[0] },
            new WagonInfo { Kind = WagonKind.Cargo, Name = "Gesloten goederenwagon", Capacity = "Kratten binnen, doorloopbaar", Category = CategoryGoods,
                Crates = 4, Sprite = "cargo", Cost = 400, Weight = 1, Breakdowns = new[] { "Grendelkast", "Aspot", "Remslang" } },
            new WagonInfo { Kind = WagonKind.Cattle, Name = "Veewagen", Capacity = "Hooi en ruimte voor dieren", Category = CategoryAnimals,
                Animals = 2, Sprite = "cattle", Cost = 600, Weight = 1, Breakdowns = new string[0] },
            new WagonInfo { Kind = WagonKind.Tank, Name = "Ketelwagon", Capacity = "Eén tank voor water of olie", Category = CategoryLiquid,
                Tanks = 1, Sprite = "tank", Cost = 1200, Weight = 2, Breakdowns = new[] { "Afsluiter", "Aspot", "Remslang" } },
            new WagonInfo { Kind = WagonKind.DieselCoach, Name = "Dieselrijtuig zonder deuren", Hidden = true, Capacity = "Extra zitplaatsen", Category = CategoryPassengers,
                Seats = 10, Sprite = "diesel_coach", HasSet = true, Set = TrainKind.Diesel, Weight = 1, Breakdowns = new[] { "Dakventilator", "Aspot", "Remslang" } },
            new WagonInfo { Kind = WagonKind.DieselCoachDoors, Name = "Dieselrijtuig", Capacity = "Hier zitten de reizigers met kaartjes", Category = CategoryPassengers,
                Seats = 10, Sprite = "diesel_coach", HasSet = true, Set = TrainKind.Diesel, Weight = 1, Breakdowns = new[] { "Dakventilator", "Aspot", "Remslang" } },
            new WagonInfo { Kind = WagonKind.ModernCoach, Name = "Modern rijtuig", Capacity = "Extra zitplaatsen", Category = CategoryPassengers,
                Seats = 12, Sprite = "modern_coach", HasSet = true, Set = TrainKind.Modern, Weight = 1, Breakdowns = new[] { "Aircon", "Aspot", "Remslang" } },
            new WagonInfo { Kind = WagonKind.FuturisticCoach, Name = "Futuristisch rijtuig", Capacity = "Extra zitplaatsen", Category = CategoryPassengers,
                Seats = 12, Sprite = "futur_coach", HasSet = true, Set = TrainKind.Futuristic, Weight = 1, Breakdowns = new[] { "Koeler", "Aspot", "Remslang" } },
        };

        public static readonly LevelInfo[] Levels =
        {
            new LevelInfo { Index = 0, Scene = ServiceRun.IntroScene, Label = "INTRO", Name = "Je eerste werkdag", Photo = "Whistleton",
                Summary = "Oefen de bediening zonder tijdslimiet.", Distance = "Korte oefenrit", Difficulty = 1,
                Highlights = new[] { "Briefingruimte", "Oefenen met de crew" } },
            new LevelInfo { Index = 1, Scene = ServiceRun.FirstLevelScene, Label = "LEVEL 1", Name = "Eerste dienst", Photo = "Bumblewick",
                Summary = "Eén opdracht per rit.", Distance = "Korte rit", Difficulty = 2,
                Highlights = new[] { "Emplacement met kraan", "Wissel kan kapot" } },
            new LevelInfo { Index = 2, Scene = ServiceRun.SecondLevelScene, Label = "LEVEL 2", Name = "Langs de kloof", Photo = "Puddleford",
                Summary = "Meerdere opdrachten en incidenten onderweg.", Distance = "± 690 m", Difficulty = 3,
                Highlights = new[] { "Diepe kloof", "Stenen boogbrug" } },
            new LevelInfo { Index = 3, Scene = ServiceRun.ThirdLevelScene, Label = "LEVEL 3", Name = "Over de hoogvlakte", Photo = "Craggleton",
                Summary = "Over de viaducten en de bergkam, met een laatste klim naar Eaglecrest.", Distance = "± 1,9 km", Difficulty = 4,
                Highlights = new[] { "Viaduct van 72 m", "Sneeuwgrens" } },
            new LevelInfo { Index = 4, Scene = ServiceRun.FourthLevelScene, Label = "LEVEL 4", Name = "Over de pas", Photo = "Frostbury",
                Summary = "Klim door de sneeuw naar de tunnel en daal af naar het ijsdal.", Distance = "± 1,7 km", Difficulty = 5,
                Highlights = new[] { "Haarspeldbochten", "Sneeuwlawines" } },
            new LevelInfo { Index = 5, Scene = ServiceRun.FifthLevelScene, Label = "LEVEL 5", Name = "Door het woud", Photo = "Fernwood",
                Summary = "Dicht loofbos, stammen op het spoor en twee bruggen onder tijdsdruk.", Distance = "± 950 m", Difficulty = 6,
                Highlights = new[] { "Rollende boomstammen", "Beukenbos" } },
        };

        /// <summary>Treinstel voor een nieuwe speler. Niets is verplicht: alles mag in of op elke wagon mee.</summary>
        public static readonly WagonKind[] StarterWagons = { WagonKind.Regional, WagonKind.Flatbed };

        public static TrainInfo Train(TrainKind kind) => Trains[(int)kind];

        public static WagonInfo Wagon(WagonKind kind)
        {
            foreach (var wagon in Wagons) if (wagon.Kind == kind) return wagon;
            return Wagons[0];
        }

        public static LevelInfo LevelForScene(string scene)
        {
            foreach (var level in Levels) if (level.Scene == scene) return level;
            return null;
        }

        /// <summary>Het regionale rijtuig is het ene rijtuig van de lijn, met de zittende reizigers erin: maximaal één.</summary>
        public static bool IsUnique(WagonKind kind) => kind is WagonKind.Regional or WagonKind.DieselCoachDoors;

        public static int PassengerCoachIndex(List<WagonKind> wagons)
        {
            int regional = wagons.IndexOf(WagonKind.Regional);
            return regional >= 0 ? regional : wagons.IndexOf(WagonKind.DieselCoachDoors);
        }

        /// <summary>Set-rijtuigen passen alleen achter hun eigen loc; de rest overal.</summary>
        public static bool Fits(WagonKind wagon, TrainKind train)
        {
            var info = Wagon(wagon);
            return !info.HasSet || info.Set == train;
        }

        public static IEnumerable<string> BreakdownsOf(TrainKind train, IEnumerable<WagonKind> wagons)
        {
            foreach (var part in Train(train).Breakdowns) yield return part;
            foreach (var wagon in wagons)
                foreach (var part in Wagon(wagon).Breakdowns) yield return part;
        }

        public static string FormatTickets(int amount)
        {
            string digits = System.Math.Abs(amount).ToString();
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) text.Append('.');
                text.Append(digits[i]);
            }
            return (amount < 0 ? "-" : "") + text;
        }
    }
}
