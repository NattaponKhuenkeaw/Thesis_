using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Runtime sensor on a chamber enter trigger (or opened divider).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatChamberEnterSensor : MonoBehaviour
    {
        private CombatStageController stage;
        private int chamberIndex = -1;

        public void Configure(CombatStageController owner, int index)
        {
            stage = owner;
            chamberIndex = index;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Notify(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            Notify(other);
        }

        private void Notify(Collider2D other)
        {
            if (stage == null || chamberIndex < 0)
            {
                return;
            }

            stage.NotifyPlayerEntered(chamberIndex, other);
        }
    }
}
