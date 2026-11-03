using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "P_State_New", menuName = "State/PlayerState/Move")]
public class PState_Move : PlayerState
{
	[SerializeField]
	private List<AudioClip> footstepLeafAudioList;

	[SerializeField]
	private List<AudioClip> footstepStoneAudioList;

	[SerializeField]
	private List<AudioClip> footstepWoodAudioList;

	private Coroutine audioPlayCo;

	public override void StateStart(PlayerController manager)
	{
		audioPlayCo = manager.StartCoroutine(AudioPlayCourantine(manager));
		base.StateStart(manager);
		manager.canFlip = true;
		manager.FlipCheck();
		manager.OnGroundReset();
	}

	public override void StateUpdate(PlayerController manager)
	{
	}

	public override void StateFixedUpdate(PlayerController manager)
	{
		manager.Move();
	}

	public override void StateExit(PlayerController manager)
	{
		if (audioPlayCo != null)
		{
			manager.StopCoroutine(audioPlayCo);
		}
	}

	public override void OnEnable()
	{
		base.stateName = PlayerData.CharState.Move;
	}

	private IEnumerator AudioPlayCourantine(PlayerController manager)
	{
		while (true)
		{
			PlayFootstepAudio(manager);
			yield return new WaitForSeconds(0.33f);
		}
	}

	private void PlayFootstepAudio(PlayerController manager)
	{
		switch (manager.CurrentGroundSurface)
		{
		case SurfaceType.Stone:
			Singleton<AudioManager>.Instance.PlaySound(PlaySoundRequest.SE(footstepStoneAudioList).WithVolume(0.7f).WithPitch(0.9f, 1.1f));
			break;
		case SurfaceType.Wood:
			Singleton<AudioManager>.Instance.PlaySound(PlaySoundRequest.SE(footstepWoodAudioList).WithVolume(0.7f).WithPitch(0.9f, 1.1f));
			break;
		default:
			Singleton<AudioManager>.Instance.PlaySound(PlaySoundRequest.SE(footstepLeafAudioList).WithVolume(0.3f).WithPitch(0.9f, 1.1f));
			break;
		}
	}
}
