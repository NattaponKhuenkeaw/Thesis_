using Unity.Cinemachine;
using UnityEngine;

public class SetUp_CameraConfiner_OnEnter : MonoBehaviour
{
	[SerializeField]
	private CinemachineConfiner2D camConfinder;

	private CompositeCollider2D compoSiteColliderRef;

	private void Awake()
	{
		if (camConfinder == null)
		{
			camConfinder = Object.FindAnyObjectByType<CinemachineConfiner2D>();
		}
		compoSiteColliderRef = GetComponent<CompositeCollider2D>();
	}

	private void OnTriggerEnter2D(Collider2D col)
	{
		if (col.CompareTag("Player"))
		{
			camConfinder.BoundingShape2D = compoSiteColliderRef;
		}
	}
}
