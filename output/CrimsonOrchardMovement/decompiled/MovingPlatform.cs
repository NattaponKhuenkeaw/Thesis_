using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
	[SerializeField]
	private Transform startPos;

	[SerializeField]
	private Transform endPos;

	[SerializeField]
	private float speed;

	private Vector3 target;

	private Rigidbody2D playerRb;

	private Rigidbody2D rb;

	private void Start()
	{
		target = endPos.position;
		rb = GetComponent<Rigidbody2D>();
	}

	private void Update()
	{
		Vector2 vector = (target - base.transform.position).normalized;
		rb.linearVelocity = vector * speed;
		if (Vector3.Distance(base.transform.position, target) < 0.1f)
		{
			if (target == startPos.position)
			{
				target = endPos.position;
			}
			else
			{
				target = startPos.position;
			}
		}
	}
}
