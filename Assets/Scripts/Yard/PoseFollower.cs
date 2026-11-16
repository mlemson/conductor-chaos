// Conductor Chaos — onderdeel dat met een hendel meebeweegt (issue #237): de vulpijp van de watertoren zakt mee met de
// hendel op de standaard. Cosmetisch en voor de botsvorm; de hendel zelf is al gesynchroniseerd, dus iedereen
// rekent hetzelfde uit.
using UnityEngine;

namespace ConductorChaos.Yard
{
    [DisallowMultipleComponent]
    public sealed class PoseFollower : MonoBehaviour
    {
        [SerializeField] HandLever lever;
        [SerializeField] Quaternion fromRotation = Quaternion.identity;
        [SerializeField] Quaternion toRotation = Quaternion.identity;
        [SerializeField] float smoothing = 6f;

        public void Setup(HandLever driver, Quaternion from, Quaternion to)
        {
            lever = driver;
            fromRotation = from;
            toRotation = to;
        }

        void LateUpdate()
        {
            if (!lever) return;
            var goal = Quaternion.Slerp(fromRotation, toRotation, lever.Normalized01);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, goal, 1f - Mathf.Exp(-Time.deltaTime * smoothing));
        }
    }
}
