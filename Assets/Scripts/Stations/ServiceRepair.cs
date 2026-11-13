using ConductorChaos.Track;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceRepair : ServiceObjective
    {
        [SerializeField] private RepairPoint repair;
        [SerializeField] private TrackSwitch turnout;

        public override int Target => 1;
        public override bool IsConfigured => repair && turnout;
        public bool IsBroken => repair.IsBroken;
        public int Hits => Mathf.RoundToInt(repair.Progress * repair.RequiredHits);
        public int RequiredHits => repair.RequiredHits;
        public override void BeginOnServer() => repair.ServerBreak();
        public override int ReadProgress(bool trainStoppedAtDestination) => repair.IsBroken || turnout.IsDiverging ? 0 : 1;
    }
}
