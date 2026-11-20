using UnityEngine;

namespace ConductorChaos.Stations
{
    public abstract class ServiceObjective : MonoBehaviour
    {
        [SerializeField] private string title;
        [SerializeField, TextArea] private string instructions;
        [SerializeField] private string icon = "route";

        public string Title => title;
        public string Instructions => instructions;
        public string Icon => icon;
        internal void SetBrief(string label, string hint, string iconKey)
        {
            title = label;
            instructions = hint;
            icon = iconKey;
        }
        public abstract int Target { get; }
        public abstract bool IsConfigured { get; }
        public virtual bool ReadyToDepart => true;
        public virtual void BeginOnServer() { }
        public virtual void ObserveTravelOnServer() { }

        // ServiceRun calls this on the server. Implementations observe actual world state.
        public abstract int ReadProgress(bool trainStoppedAtDestination);
    }
}
