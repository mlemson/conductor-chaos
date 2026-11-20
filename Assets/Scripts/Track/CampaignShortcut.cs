using UnityEngine;

namespace ConductorChaos.Track
{
    public sealed class CampaignShortcut : MonoBehaviour
    {
        [field: SerializeField] public TrackSwitch Switch { get; private set; }
        [field: SerializeField] public TrackPiece FastEntry { get; private set; }
        [field: SerializeField] public TrackPiece LastSafe { get; private set; }
        [field: SerializeField] public TrackPiece Shortcut { get; private set; }
        [field: SerializeField] public TrackPiece Merge { get; private set; }

        public void Configure(TrackSwitch junction, TrackPiece fast, TrackPiece lastSafe, TrackPiece connector, TrackPiece merge)
        {
            Switch = junction;
            FastEntry = fast;
            LastSafe = lastSafe;
            Shortcut = connector;
            Merge = merge;
        }

        private void Awake() { if (Shortcut) Shortcut.DangerousCurve = true; }

        internal static bool IsRouteChoice(TrackSwitch junction) => junction && junction.GetComponent<CampaignShortcut>();
    }
}
