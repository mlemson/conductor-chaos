// Conductor Chaos — problemen onderweg in level 2 (issue #224).
//   * Steenlawine: rotsblokken liggen al zichtbaar op de klifrand. Rijdt de trein een triggerzone in, dan rollen ze op
//     het spoor. TrainDrive stopt voor alles wat op de rails ligt, dus de crew moet uitstappen en ze opzij duwen.
//   * Spoorschade: een spoorstuk verderop (Track_Broken_6m met RepairPoint) breekt. Wie eroverheen rijdt ontspoort;
//     stoppen en de bout met de moersleutel vastslaan.
// De server kiest bij het begin welke incidenten deze run gebeuren, start ze en ziet wanneer ze opgelost zijn.
// Clients krijgen alleen de status (bitmaskers) en tonen een waarschuwing in de HUD.
using System;
using ConductorChaos.Track;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Stations
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class ServiceIncidents : NetworkBehaviour
    {
        [Serializable]
        public sealed class Rockfall
        {
            public string name = "Steenlawine";
            [Tooltip("Rijdt de trein (railpositie) hierin, dan vallen de rotsen. Ruim vóór de plek zelf.")]
            public BoxCollider trigger;
            [Tooltip("Rotsblokken met NetworkObject/NetworkTransform/NetworkRigidbody. Kinematisch tot de lawine.")]
            public Rigidbody[] rocks;
            [Tooltip("Waar elk rotsblok op het spoor landt (zelfde volgorde als de rotsen).")]
            public Transform[] landing;
            [Tooltip("Punt op het spoor met forward in rijrichting: hier wordt gemeten of het spoor weer vrij is.")]
            public Transform site;
            [Range(0f, 1f)] public float chance = 0.75f;
        }

        [Serializable]
        public sealed class TrackDamage
        {
            public string name = "Spoorschade";
            public BoxCollider trigger;
            public RepairPoint repair;
            [Range(0f, 1f)] public float chance = 0.6f;
        }

        [SerializeField] private Rockfall[] rockfalls = Array.Empty<Rockfall>();
        [SerializeField] private TrackDamage[] damages = Array.Empty<TrackDamage>();
        [Tooltip("Minstens zoveel incidenten per run (willekeurig gekozen als het lot er te weinig gaf).")]
        [SerializeField, Min(0)] private int minimumIncidents = 1;
        [Tooltip("Vliegtijd van een vallend rotsblok tot het spoor (s).")]
        [SerializeField, Min(0.3f)] private float fallTime = 1.15f;

        // bit i = rockfalls[i], bit 16 + j = damages[j]
        private readonly NetworkVariable<int> planned = new();
        private readonly NetworkVariable<int> triggered = new();
        private readonly NetworkVariable<int> resolved = new();
        private bool planMade;
        private float nextCheck;

        public static ServiceIncidents Active { get; private set; }

        public void Configure(Rockfall[] rockfallSites, TrackDamage[] damageSites, int minimum)
        {
            rockfalls = rockfallSites ?? Array.Empty<Rockfall>();
            damages = damageSites ?? Array.Empty<TrackDamage>();
            minimumIncidents = minimum;
        }

        public override void OnNetworkSpawn()
        {
            Active = this;
            if (!IsServer) return;
            planned.Value = triggered.Value = resolved.Value = 0;
            planMade = false;
            foreach (var site in rockfalls)
                if (site?.rocks != null)
                    foreach (var rock in site.rocks)
                        if (rock) rock.isKinematic = true;          // liggen klaar op de klifrand
        }

        public override void OnNetworkDespawn()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Waarschuwing voor de HUD, of null als er niets aan de hand is.</summary>
        public string Warning
        {
            get
            {
                if (!IsSpawned) return null;
                int open = triggered.Value & ~resolved.Value;
                if (open == 0) return null;
                for (int j = 0; j < damages.Length; j++)
                    if ((open & DamageBit(j)) != 0)
                        return "Spoorschade verderop! Stop vóór de kapotte rail en sla de gele bout vast met de moersleutel.";
                for (int i = 0; i < rockfalls.Length; i++)
                    if ((open & RockBit(i)) != 0)
                        return "Steenlawine op het spoor! Stop, stap uit en duw de rotsblokken van de rails.";
                return null;
            }
        }

        /// <summary>Aantal incidenten dat nu speelt (getriggerd en nog niet opgelost).</summary>
        public int OpenCount
        {
            get
            {
                int open = triggered.Value & ~resolved.Value, n = 0;
                for (; open != 0; open &= open - 1) n++;
                return n;
            }
        }

        private static int RockBit(int i) => 1 << i;
        private static int DamageBit(int j) => 1 << (16 + j);

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            var run = ServiceRun.Active;
            if (!run || run.gameObject.scene != gameObject.scene || !run.Train) return;
            if (!planMade && run.Phase is ServiceRun.RunPhase.Departure or ServiceRun.RunPhase.Travelling) Plan();
            if (run.Phase is not (ServiceRun.RunPhase.Travelling or ServiceRun.RunPhase.Arrival)) return;

            Vector3 rail = run.Train.ServiceRailPosition;
            for (int i = 0; i < rockfalls.Length && i < 16; i++)
                if (Due(RockBit(i), rockfalls[i]?.trigger, rail)) StartRockfall(i);
            for (int j = 0; j < damages.Length && j < 15; j++)
                if (Due(DamageBit(j), damages[j]?.trigger, rail)) StartDamage(j);

            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            int done = resolved.Value;
            for (int i = 0; i < rockfalls.Length && i < 16; i++)
                if ((triggered.Value & RockBit(i)) != 0 && TrackClear(rockfalls[i])) done |= RockBit(i);
            for (int j = 0; j < damages.Length && j < 15; j++)
                if ((triggered.Value & DamageBit(j)) != 0 && damages[j]?.repair && !damages[j].repair.IsBroken) done |= DamageBit(j);
            if (done != resolved.Value) resolved.Value = done;
        }

        private bool Due(int bit, BoxCollider trigger, Vector3 rail) =>
            (planned.Value & bit) != 0 && (triggered.Value & bit) == 0 && trigger && ServiceStation.Contains(trigger, rail);

        // Server: welke incidenten gebeuren er deze run? Altijd minstens 'minimumIncidents'.
        private void Plan()
        {
            planMade = true;
            int mask = 0, possible = 0;
            for (int i = 0; i < rockfalls.Length && i < 16; i++)
            {
                if (!Usable(rockfalls[i])) continue;
                possible |= RockBit(i);
                if (UnityEngine.Random.value < rockfalls[i].chance) mask |= RockBit(i);
            }
            for (int j = 0; j < damages.Length && j < 15; j++)
            {
                if (damages[j] == null || !damages[j].trigger || !damages[j].repair) continue;
                possible |= DamageBit(j);
                if (UnityEngine.Random.value < damages[j].chance) mask |= DamageBit(j);
            }
            for (int guard = 0; guard < 32 && CountBits(mask) < minimumIncidents && (possible & ~mask) != 0; guard++)
            {
                int bit = 1 << UnityEngine.Random.Range(0, 31);
                if ((possible & bit) != 0) mask |= bit;
            }
            planned.Value = mask;
        }

        private static bool Usable(Rockfall site) => site != null && site.trigger && site.site && site.rocks != null &&
            site.landing != null && site.rocks.Length > 0 && site.landing.Length >= site.rocks.Length;

        private static int CountBits(int mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1) n++;
            return n;
        }

        private void StartRockfall(int i)
        {
            triggered.Value |= RockBit(i);
            var site = rockfalls[i];
            for (int k = 0; k < site.rocks.Length; k++)
            {
                var rock = site.rocks[k];
                var target = site.landing[k];
                if (!rock || !target) continue;
                rock.isKinematic = false;
                rock.WakeUp();
                // Ballistische worp: na fallTime seconden ligt het midden van het blok op het landingspunt.
                float t = fallTime * (0.9f + 0.08f * k);
                Vector3 velocity = (target.position - rock.worldCenterOfMass) / t - 0.5f * Physics.gravity * t;
                rock.linearVelocity = velocity;
                rock.angularVelocity = UnityEngine.Random.insideUnitSphere * 3f;
            }
        }

        private void StartDamage(int j)
        {
            triggered.Value |= DamageBit(j);
            damages[j].repair.ServerBreak();
        }

        // Ligt er nog een rotsblok op of vlak naast de rails bij deze plek?
        private static bool TrackClear(Rockfall site)
        {
            if (site?.site == null || site.rocks == null) return true;
            Vector3 origin = site.site.position;
            Vector3 along = Vector3.ProjectOnPlane(site.site.forward, Vector3.up).normalized;
            foreach (var rock in site.rocks)
            {
                if (!rock) continue;
                Vector3 offset = rock.worldCenterOfMass - origin;
                if (offset.y < -2.5f || offset.y > 3f) continue;                          // in het ravijn of nog boven
                float lengthwise = Mathf.Abs(Vector3.Dot(offset, along));
                float sideways = Vector3.ProjectOnPlane(offset - along * Vector3.Dot(offset, along), Vector3.up).magnitude;
                if (lengthwise <= 30f && sideways < 2.3f) return false;
            }
            return true;
        }
    }
}
