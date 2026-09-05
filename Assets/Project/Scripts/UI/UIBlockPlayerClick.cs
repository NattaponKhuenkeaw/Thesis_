using UnityEngine;

namespace Project.UI
{
    /// <summary>
    /// Attach to any individual UI GameObject, panel, or canvas (such as Navigation, Admin, RouteOffer)
    /// to block player click inputs (Attack/Skill) while clicking or pressing on it or its children.
    /// This avoids having to disable raycast targets on background canvases globally.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIBlockPlayerClick : MonoBehaviour
    {
    }
}
