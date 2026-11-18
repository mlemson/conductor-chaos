using ConductorChaos.Art;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
    public sealed class RowboatFloat : NetworkBehaviour
    {
        Rigidbody body;

        public override void OnNetworkSpawn() => body = GetComponent<Rigidbody>();

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !body || body.isKinematic) return;
            foreach (var water in StylizedWater.Active)
            {
                if (!water || water.gameObject.scene != gameObject.scene ||
                    !water.Contains(new Vector3(body.position.x, water.SurfaceY - .1f, body.position.z))) continue;
                float height = water.SurfaceY - body.position.y;
                if (height < -.4f || height > water.depth) continue;
                float lift = Mathf.Clamp(-Physics.gravity.y + height * 35f - body.linearVelocity.y * 5f, 0f, 35f);
                body.AddForce(Vector3.up * lift, ForceMode.Acceleration);
                Vector3 tip = Vector3.Cross(body.rotation * Vector3.up, Vector3.up);
                Vector3 pitchAndRoll = Vector3.ProjectOnPlane(body.angularVelocity, Vector3.up);
                body.AddTorque(tip * 16f - pitchAndRoll * 3f, ForceMode.Acceleration);
                break;
            }
        }
    }
}
