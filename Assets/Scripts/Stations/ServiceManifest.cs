using UnityEngine;

namespace ConductorChaos.Stations
{
    internal readonly struct ServiceManifest
    {
        private static readonly Color32[] Colors =
        {
            new(70, 111, 186, 255), new(197, 77, 62, 255), new(74, 137, 89, 255), new(226, 168, 48, 255)
        };

        internal static readonly string[] TravellerNames = { "Zakenreiziger", "Toerist", "Oudere reiziger", "Kind" };
        internal static readonly string[] FreightNames = { "Postkist", "Houten vat", "Hijskist" };
        private readonly int card;

        internal ServiceManifest(int packed) => card = packed;

        internal bool IsReady => card != 0;
        internal int FirstFare => card & 3;
        internal int SecondFare => (card >> 2) & 3;
        internal int ExcludedTraveller => (card >> 4) & 3;
        internal int RequiredFreight => (card >> 6) & 3;
        internal int ExcludedFreight => (card >> 8) & 3;
        internal int PickupStop => (card >> 10) & 1;
        internal int BonusFreight => 3 - RequiredFreight - ExcludedFreight;

        internal static int Roll(int stopCount)
        {
            int first = Random.Range(0, 4);
            int second = (first + Random.Range(1, 4)) % 4;
            int required = Random.Range(0, 3);
            int excluded = (required + Random.Range(1, 3)) % 3;
            return (1 << 11) | first | (second << 2) | (Random.Range(0, 4) << 4) | (required << 6) |
                (excluded << 8) | (Random.Range(0, stopCount) << 10);
        }

        internal bool IsFareValid(int fare) => fare == FirstFare || fare == SecondFare;

        internal int PickFare(bool valid)
        {
            if (valid) return Random.value < .5f ? FirstFare : SecondFare;
            int first = (FirstFare + 1) % 4;
            while (IsFareValid(first)) first = (first + 1) % 4;
            int second = (first + 1) % 4;
            while (IsFareValid(second)) second = (second + 1) % 4;
            return Random.value < .5f ? first : second;
        }

        internal static Color FareColor(int fare) => Colors[fare];
        internal static string FareMark(int fare) => ((char)('A' + fare)).ToString();
    }
}
