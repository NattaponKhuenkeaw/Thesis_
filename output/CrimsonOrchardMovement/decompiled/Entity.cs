using UnityEngine;

public abstract class Entity : MonoBehaviour
{
	[SerializeField]
	protected float speed;

	[SerializeField]
	private float atk;

	[SerializeField]
	private float vulnerablePercentage;

	[field: Header("Entity Base Stats")]
	[field: SerializeField]
	public float maxHealth { get; protected set; } = 100f;

	public float Speed
	{
		get
		{
			return speed;
		}
		set
		{
			speed = value;
		}
	}

	public float Atk
	{
		get
		{
			return atk;
		}
		set
		{
			atk = value;
		}
	}

	[field: Header("GroundCheck")]
	[field: SerializeField]
	public Vector2 boxSize { get; protected set; } = new Vector2(0.5f, 0.1f);

	[field: SerializeField]
	public float boxOffsetY { get; protected set; } = -0.5f;

	[field: SerializeField]
	public LayerMask groundLayer { get; protected set; }

	[field: SerializeField]
	public float coyoteTime { get; protected set; } = 0.2f;

	[field: Header("Attack")]
	[field: SerializeField]
	public Transform damagedBoxPos { get; protected set; }

	[field: Header("Falling")]
	[field: SerializeField]
	public float baseGravityScale { get; protected set; }

	public float VulnerablePercentage
	{
		get
		{
			return vulnerablePercentage;
		}
		set
		{
			vulnerablePercentage = value;
		}
	}

	public Rigidbody2D rb { get; protected set; }

	public Collider2D col { get; set; }

	public abstract void Init();
}
