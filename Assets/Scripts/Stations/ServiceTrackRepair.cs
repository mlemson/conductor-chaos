using ConductorChaos.Track;
using UnityEngine;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Level 2 (issue #224): een kapot spoorstuk (Track_Broken_6m met RepairPoint) op de route moet met de moersleutel
    /// gerepareerd worden. Rijdt de trein erop terwijl het kapot is, dan ontspoort hij (TrainDrive). De server telt.
    /// </summary>
    public sealed class ServiceTrackRepair : ServiceObjective
    {
        [SerializeField] private RepairPoint repair;
        [Tooltip("Kapot bij het begin van de dienst.")]
        [SerializeField] private bool breakAtStart = true;

        public RepairPoint Repair => repair;
        public override int Target => 1;
        public override bool IsConfigured => repair;
        public bool IsBroken => repair && repair.IsBroken;
        public int Hits => repair ? Mathf.RoundToInt(repair.Progress * repair.RequiredHits) : 0;
        public int RequiredHits => repair ? repair.RequiredHits : 0;

        public void Configure(RepairPoint point, bool brokenAtStart)
        {
            repair = point;
            breakAtStart = brokenAtStart;
        }

        public override void BeginOnServer()
        {
            if (breakAtStart && repair) repair.ServerBreak();
        }

        public override int ReadProgress(bool trainStoppedAtDestination) => repair && !repair.IsBroken ? 1 : 0;
    }
}
