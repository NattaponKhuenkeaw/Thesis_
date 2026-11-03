using UnityEngine;

[DisallowMultipleComponent]
public class GroundSurface : MonoBehaviour
{
	[field: SerializeField]
	public SurfaceType SurfaceType { get; private set; }
}
