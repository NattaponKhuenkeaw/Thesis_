using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerData : Entity, IJumpable, IDashable, IDataPersistence
{
	public enum CharState
	{
		Idle,
		Move,
		Jump,
		AirJump,
		Falling,
		SlowFalling,
		Dash,
		Crouch,
		DrinkPotion,
		BasicAttack1,
		BasicAttack2,
		BasicAttack3,
		Parry,
		UpperAttack,
		AirAttack1,
		AirAttack2,
		AirPlunge,
		CounterAttack,
		DashAttack,
		ChargeAttack,
		WallSlide,
		WallJump,
		Hurt,
		InstantDrop,
		DownAttack,
		UseSkill,
		VeinStackConsuming
	}

	[Header("Mana")]
	[SerializeField]
	private int maxMana;

	[Header("IDashable")]
	[SerializeField]
	private float dashForce;

	[SerializeField]
	private float dashCoolDown;

	[SerializeField]
	private int maxDashCounter = 1;

	[Header("IJumpable")]
	[SerializeField]
	private float jumpForce;

	[SerializeField]
	private int maxAirJump = 1;

	[Header("PlayerMoveMent Upgrade")]
	public float runAcceleration;

	[HideInInspector]
	public float runAccelAmount;

	public float runDecceleration;

	[HideInInspector]
	public float runDeccelAmount;

	[Space(10f)]
	[Range(0.01f, 1f)]
	public float accelInAir;

	[Range(0.01f, 1f)]
	public float deccelInAir;

	public bool doConserveMomentum;

	[SerializeField]
	private CharacterStatSO curCharacter;

	public PlayerController playerController { get; private set; }

	public PlayerHealth playerHealth { get; private set; }

	public PlayerMana playerMana { get; private set; }

	public PlayerInventory playerInventory { get; private set; }

	public CinemachineImpulseSource impulseSource { get; private set; }

	public VisualEffectManager visualEffectManager { get; private set; }

	[field: SerializeField]
	public HurtBoxManager hurtBoxManager { get; private set; }

	public PlayerPassiveSkillData playerPassiveSkillData { get; private set; }

	public PlayerActiveSkillData playerActiveSkillData { get; private set; }

	public int MaxMana
	{
		get
		{
			return maxMana;
		}
		set
		{
			maxMana = value;
		}
	}

	[field: Header("ChargeAttackVariable")]
	[field: SerializeField]
	public float minAtkCharge { get; private set; }

	[field: Header("Max Health Upgrade")]
	[field: SerializeField]
	public float baseMaxHealthUpgradePercentage { get; protected set; }

	[field: SerializeField]
	public float maxHealthUpgradePercentage { get; protected set; }

	[field: SerializeField]
	public float currentMaxHealth { get; protected set; }

	public float DashForce
	{
		get
		{
			return dashForce;
		}
		set
		{
			dashForce = value;
		}
	}

	public float DashCoolDown
	{
		get
		{
			return dashCoolDown;
		}
		set
		{
			dashCoolDown = value;
		}
	}

	public int MaxDashCounter
	{
		get
		{
			return maxDashCounter;
		}
		set
		{
			maxDashCounter = value;
		}
	}

	public float JumpForce
	{
		get
		{
			return jumpForce;
		}
		set
		{
			jumpForce = value;
		}
	}

	public int MaxAirjump
	{
		get
		{
			return maxAirJump;
		}
		set
		{
			maxAirJump = value;
		}
	}

	public CharacterStatSO CurCharacter => curCharacter;

	[field: Header("Hurt")]
	[field: SerializeField]
	public float stundelay { get; private set; } = 0.25f;

	public bool isDeath { get; private set; }

	[field: Header("PlayerMat")]
	[field: SerializeField]
	public PhysicsMaterial2D groundMat { get; private set; }

	[field: SerializeField]
	public PhysicsMaterial2D airMat { get; private set; }

	[field: Header("Other")]
	[field: SerializeField]
	public CombatStatsData combatStats { get; private set; }

	[field: SerializeField]
	public ModifierManager modifierManager { get; private set; }

	[field: SerializeField]
	public Transform vfxSpawnPos { get; private set; }

	private void OnValidate()
	{
		runAccelAmount = 50f * runAcceleration / base.Speed;
		runDeccelAmount = 50f * runDecceleration / base.Speed;
		runAcceleration = Mathf.Clamp(runAcceleration, 0.01f, base.Speed);
		runDecceleration = Mathf.Clamp(runDecceleration, 0.01f, base.Speed);
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube((Vector2)base.transform.position + new Vector2(0f, base.boxOffsetY), base.boxSize);
	}

	public override void Init()
	{
		base.transform.SetParent(null);
		SetupVariable();
		SetUpStat();
	}

	public void OnDisableReset()
	{
		playerPassiveSkillData.ClearSkillList();
	}

	private void SetUpStat()
	{
		currentMaxHealth = base.maxHealth;
		base.rb.gravityScale = base.baseGravityScale;
		isDeath = false;
		MaxHealthEdit(baseMaxHealthUpgradePercentage + maxHealthUpgradePercentage);
		playerHealth.Init();
		GetStatusFromLastScene(Singleton<GameManager>.Instance.CrossSceneDataManager.PlayerStatus);
		playerMana.Init(this);
		playerController.Init();
		playerPassiveSkillData.Init(this);
		playerActiveSkillData.Init(this);
	}

	private void SetupVariable()
	{
		base.rb = GetComponent<Rigidbody2D>();
		base.col = GetComponent<Collider2D>();
		playerHealth = GetComponent<PlayerHealth>();
		playerMana = GetComponent<PlayerMana>();
		playerInventory = GetComponent<PlayerInventory>();
		impulseSource = GetComponent<CinemachineImpulseSource>();
		playerController = GetComponent<PlayerController>();
		visualEffectManager = GetComponentInChildren<VisualEffectManager>();
		playerPassiveSkillData = GetComponent<PlayerPassiveSkillData>();
		playerActiveSkillData = GetComponent<PlayerActiveSkillData>();
	}

	public void LoadData(GameData data)
	{
		baseMaxHealthUpgradePercentage = data.PlayerMaxHealthUpgradePercentage;
	}

	public void SaveData(ref GameData data)
	{
		data.PlayerMaxHealthUpgradePercentage = baseMaxHealthUpgradePercentage;
	}

	public void BaseMaxHealthPercentageEdit(float amout)
	{
		baseMaxHealthUpgradePercentage += amout;
		MaxHealthEdit(amout);
	}

	public void MaxHealthEdit(float amout)
	{
		float num = playerHealth.health * 100f / currentMaxHealth;
		float num2 = base.maxHealth * amout / 100f;
		currentMaxHealth += num2;
		playerHealth.ForceSetHealth(num * base.maxHealth);
	}

	public void GetStatusFromLastScene(PlayerStatus status)
	{
		maxHealthUpgradePercentage = curCharacter.MaxHealthUpgrade * (status.MaxHealthUpgradePercent / 100f);
		playerHealth.ForceSetHealth(currentMaxHealth * (status.CurrentHealthPercent / 100f));
		base.Atk = curCharacter.ATK * (status.ATKPercent / 100f);
		base.Speed = curCharacter.Speed * (status.SpeedPercent / 100f);
		playerInventory.Setpotion(status.PotionAmount);
		playerInventory.Money = status.Money;
	}
}
