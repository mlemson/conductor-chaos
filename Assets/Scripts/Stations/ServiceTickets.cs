using ConductorChaos.Playground;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceTickets : ServiceObjective
    {
        [SerializeField] private PassengerTicket[] passengers;

        public override int Target => passengers?.Length ?? 0;
        public override bool IsConfigured => Target > 0 && System.Array.TrueForAll(passengers, ticket => ticket);

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            int count = 0;
            foreach (var ticket in passengers)
                if (ticket && ticket.IsPunched) count++;
            return count;
        }
    }
}
