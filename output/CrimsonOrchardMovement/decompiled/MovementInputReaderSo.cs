using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class MovementInputReaderSo : ScriptableObject
{
	[SerializeField]
	private InputActionAsset inputActionAsset;

	private InputAction moveAction;

	private InputAction jumpAction;

	private InputAction attackAction;

	private InputAction dashAction;

	private InputAction crouchAction;

	private InputAction lookUpAction;

	private InputAction usePotionAction;

	private InputAction useSkillAction;

	private InputAction pauseAction;

	private InputAction stackConsumeAction;

	public Vector2 MoveInput { get; private set; }

	public bool JumpPressed { get; private set; }

	public bool JumpHold { get; private set; }

	public bool JumpRelease { get; private set; }

	public bool AttackPressed { get; private set; }

	public bool AttackHold { get; private set; }

	public bool AttackRelease { get; private set; }

	public bool DashPressed { get; private set; }

	public bool CrouchHold { get; private set; }

	public bool UsePotionPressed { get; private set; }

	public bool LookUpHold { get; private set; }

	public bool UseSkillPressed { get; private set; }

	public bool UseSkillHold { get; private set; }

	public bool UseSkillRelease { get; private set; }

	public bool StackConsumePressed { get; private set; }

	public event UnityAction PauseEvent;

	private void OnEnable()
	{
		moveAction = inputActionAsset.FindAction("Move");
		jumpAction = inputActionAsset.FindAction("Jump");
		attackAction = inputActionAsset.FindAction("Attack");
		dashAction = inputActionAsset.FindAction("Dash");
		crouchAction = inputActionAsset.FindAction("Crouch");
		lookUpAction = inputActionAsset.FindAction("LookUp");
		usePotionAction = inputActionAsset.FindAction("UsePotion");
		useSkillAction = inputActionAsset.FindAction("UseSkill");
		pauseAction = inputActionAsset.FindAction("Pause");
		stackConsumeAction = inputActionAsset.FindAction("ConsumeStack");
		pauseAction.started += OnPauseAction;
		moveAction.Enable();
		jumpAction.Enable();
		attackAction.Enable();
		dashAction.Enable();
		crouchAction.Enable();
		lookUpAction.Enable();
		usePotionAction.Enable();
		useSkillAction.Enable();
		pauseAction.Enable();
		stackConsumeAction.Enable();
	}

	private void OnDisable()
	{
		pauseAction.started -= OnPauseAction;
		moveAction.Disable();
		jumpAction.Disable();
		attackAction.Disable();
		dashAction.Disable();
		crouchAction.Disable();
		lookUpAction.Disable();
		usePotionAction.Disable();
		useSkillAction.Disable();
		pauseAction.Disable();
		stackConsumeAction.Disable();
	}

	public void UpdateInput()
	{
		MoveInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
		JumpPressed = jumpAction?.WasPressedThisFrame() ?? false;
		JumpHold = jumpAction?.IsPressed() ?? false;
		JumpRelease = jumpAction?.WasReleasedThisFrame() ?? false;
		AttackPressed = attackAction?.WasPressedThisFrame() ?? false;
		AttackHold = attackAction?.IsPressed() ?? false;
		AttackRelease = attackAction?.WasReleasedThisFrame() ?? false;
		DashPressed = dashAction?.WasPressedThisFrame() ?? false;
		CrouchHold = crouchAction?.IsPressed() ?? false;
		LookUpHold = lookUpAction?.IsPressed() ?? false;
		UsePotionPressed = usePotionAction?.WasPressedThisFrame() ?? false;
		UseSkillPressed = useSkillAction?.WasPressedThisFrame() ?? false;
		UseSkillHold = useSkillAction?.IsPressed() ?? false;
		UseSkillRelease = useSkillAction?.WasReleasedThisFrame() ?? false;
		StackConsumePressed = stackConsumeAction?.IsPressed() ?? false;
	}

	private void OnPauseAction(InputAction.CallbackContext obj)
	{
		PauseEvent?.Invoke();
	}
}
