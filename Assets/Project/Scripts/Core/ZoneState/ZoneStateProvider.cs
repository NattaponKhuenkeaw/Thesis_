using System;
using UnityEngine;

namespace Project.Core.ZoneState
{
    [CreateAssetMenu(fileName = "ZoneStateProvider", menuName = "Project/Core/ZoneStateProvider")]
    public class ZoneStateProvider : ScriptableObject
    {
        [SerializeField] private ZoneMode currentMode = ZoneMode.CombatZone;

        public event Action<ZoneMode> OnZoneModeChanged;

        public ZoneMode CurrentMode
        {
            get => currentMode;
            set
            {
                if (currentMode != value)
                {
                    currentMode = value;
                    OnZoneModeChanged?.Invoke(currentMode);
                }
            }
        }

        private void OnEnable()
        {
            // Optional: reset to a default mode on domain reload/play mode
        }

        public void ForceBroadcast()
        {
            OnZoneModeChanged?.Invoke(currentMode);
        }
    }
}
