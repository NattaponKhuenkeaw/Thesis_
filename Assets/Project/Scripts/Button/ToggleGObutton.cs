using UnityEngine;

public class ToggleGObutton : MonoBehaviour
{
    public GameObject toggleGO;

    public virtual void ToggleGO()
    {
        if (toggleGO != null)
        toggleGO.SetActive(!toggleGO.activeSelf);
    }
}
