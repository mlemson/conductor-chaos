using ConductorChaos.Playground;
using ConductorChaos.Yard;
using UnityEngine;

namespace ConductorChaos.Stations
{
    internal sealed class ServiceWaterDelivery : ServiceObjective
    {
        private const float RequiredFill = .4f;
        private TrainDrive train;
        private LiquidSource tower;
        private LiquidTank filledAtTower;

        public override int Target => 1;
        public override bool IsConfigured => train && tower;

        internal void Setup(TrainDrive locomotive, LiquidSource source)
        {
            train = locomotive;
            tower = source;
            SetBrief("Bezorg water in Fernwood", "Stop met de ketelwagon onder de watertoren in de beukenlaan. Open het tankluik, laat de pijp zakken en draai de kraan open. Neem minstens 40% water mee naar Fernwood.", "route");
        }

        internal bool HasTank()
        {
            foreach (var wagon in train.CoupledWagons)
                if (wagon && wagon.GetComponent<LiquidTank>()) return true;
            return false;
        }

        internal bool RecordFill(LiquidSource source, LiquidTank tank)
        {
            if (source != tower || train.ServiceSpeed >= .22f || !tank || tank.Kind != LiquidKind.Water || !IsCoupled(tank)) return false;
            if (tank.Level >= RequiredFill) filledAtTower = tank;
            return true;
        }

        private bool IsFilledAndCoupled(LiquidTank tank)
        {
            if (!tank || tank.Kind != LiquidKind.Water || tank.Level < RequiredFill) return false;
            return IsCoupled(tank);
        }

        private bool IsCoupled(LiquidTank tank)
        {
            foreach (var wagon in train.CoupledWagons)
                if (wagon && wagon.gameObject == tank.gameObject) return true;
            return false;
        }

        public override int ReadProgress(bool trainStoppedAtDestination) =>
            trainStoppedAtDestination && IsFilledAndCoupled(filledAtTower) ? 1 : 0;
    }
}
