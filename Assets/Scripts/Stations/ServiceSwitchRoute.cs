using ConductorChaos.Track;
using UnityEngine;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Level 3 (issue #228): de wissels op de route beginnen naar een zijspoor. De crew stopt ervoor en duwt de hendel om
    /// (TrackSwitch, server-autoritatief). Een wissel telt als afgevinkt zodra hij één keer goed heeft gestaan; wie hem
    /// daarna terugzet merkt het vanzelf (de trein rijdt het zijspoor in). De server telt.
    /// </summary>
    public sealed class ServiceSwitchRoute : ServiceObjective
    {
        [SerializeField] private TrackSwitch[] switches = new TrackSwitch[0];
        [Tooltip("Per wissel: loopt de route over de afbuigende tak?")]
        [SerializeField] private bool[] mainDiverges = new bool[0];

        private bool[] done;

        public override int Target => switches.Length;
        public override bool IsConfigured => switches.Length > 0 && mainDiverges.Length == switches.Length;

        public void Configure(TrackSwitch[] junctions, bool[] diverges)
        {
            switches = junctions;
            mainDiverges = diverges;
        }

        public override void BeginOnServer() => done = new bool[switches.Length];

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            if (done == null || done.Length != switches.Length) done = new bool[switches.Length];
            int count = 0;
            for (int i = 0; i < switches.Length; i++)
            {
                if (!done[i] && switches[i] && switches[i].IsDiverging == mainDiverges[i]) done[i] = true;
                if (done[i]) count++;
            }
            return count;
        }
    }
}
