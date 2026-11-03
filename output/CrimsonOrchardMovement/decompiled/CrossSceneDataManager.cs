using System.Collections.Generic;
using UnityEngine;

public class CrossSceneDataManager : MonoBehaviour
{
	[SerializeField]
	private PlayerStatus _playerStatus;

	private readonly Dictionary<string, ScriptableObject> extraPayloads = new Dictionary<string, ScriptableObject>();

	public PlayerStatus PlayerStatus => _playerStatus;

	public void CapturePlayer(PlayerData player)
	{
		PlayerStatus.CaptureFrom(player);
	}

	public void SetPayload(string key, ScriptableObject data)
	{
		extraPayloads[key] = data;
	}

	public T GetPayload<T>(string key) where T : ScriptableObject
	{
		if (!extraPayloads.TryGetValue(key, out var value))
		{
			return null;
		}
		return value as T;
	}

	public bool HasPayload(string key)
	{
		return extraPayloads.ContainsKey(key);
	}

	public void ClearPayload(string key)
	{
		extraPayloads.Remove(key);
	}
}
