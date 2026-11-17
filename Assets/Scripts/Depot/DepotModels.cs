// Conductor Chaos — verwijzingen naar de treinmodellen die het depot tijdens het spelen kan neerzetten (issue #239).
// Het asset staat in Resources/DepotModels.asset zodat host en clients dezelfde modellen laden.
using UnityEngine;

namespace ConductorChaos.Depot
{
    [CreateAssetMenu(menuName = "Conductor Chaos/Depot Models", fileName = "DepotModels")]
    public sealed class DepotModels : ScriptableObject
    {
        [Header("Locomotieven (stuurstanden)")]
        public GameObject dieselLocomotive;
        public GameObject modernCabCar;
        public GameObject futuristicCabCar;

        [Header("Wagons")]
        public GameObject cargoWagon;
        public GameObject cattleWagon;
        public GameObject tankWagon;
        public GameObject dieselCoach;
        public GameObject dieselCoachDoors;
        public GameObject modernCoach;
        public GameObject futuristicCoach;

        private static DepotModels cached;

        public static DepotModels Instance
        {
            get
            {
                if (!cached) cached = Resources.Load<DepotModels>("DepotModels");
                return cached;
            }
        }

        public GameObject Locomotive(TrainKind kind) => kind switch
        {
            TrainKind.Diesel => dieselLocomotive,
            TrainKind.Modern => modernCabCar,
            TrainKind.Futuristic => futuristicCabCar,
            _ => null
        };

        public GameObject Wagon(WagonKind kind) => kind switch
        {
            WagonKind.Cargo => cargoWagon,
            WagonKind.Cattle => cattleWagon,
            WagonKind.Tank => tankWagon,
            WagonKind.DieselCoach => dieselCoach,
            WagonKind.DieselCoachDoors => dieselCoachDoors,
            WagonKind.ModernCoach => modernCoach,
            WagonKind.FuturisticCoach => futuristicCoach,
            _ => null
        };
    }
}
