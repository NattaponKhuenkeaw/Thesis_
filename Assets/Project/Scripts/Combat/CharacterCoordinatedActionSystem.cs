using System.Collections.Generic;
using UnityEngine;

public class CharacterCoordinatedActionSystem : MonoBehaviour
{
    private static CharacterCoordinatedActionSystem instance;
    public static CharacterCoordinatedActionSystem Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (instance == null && Application.isPlaying)
        {
            GameObject go = new GameObject("CharacterCoordinatedActionSystem");
            instance = go.AddComponent<CharacterCoordinatedActionSystem>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (CombatEventHub.Shared != null)
        {
            CombatEventHub.Shared.DamageResolved += HandleDamageResolved;
        }
    }

    private readonly Dictionary<(CharacterObject, string), float> cooldownReadyTimes = new Dictionary<(CharacterObject, string), float>();

    public static string ResolveActionKey(PassiveCoordinatedActionDefinition coordDef)
    {
        if (coordDef == null) return string.Empty;
        if (!string.IsNullOrEmpty(coordDef.actionId)) return coordDef.actionId;
        if (!string.IsNullOrEmpty(coordDef.actionName)) return coordDef.actionName;
        return "default";
    }

    public bool IsOnCooldown(CharacterObject character, PassiveCoordinatedActionDefinition coordDef)
    {
        if (character == null || coordDef == null || coordDef.internalCooldown <= 0f)
        {
            return false;
        }

        string actionKey = ResolveActionKey(coordDef);
        if (cooldownReadyTimes.TryGetValue((character, actionKey), out float readyAt))
        {
            return Time.time < readyAt;
        }

        return false;
    }

    public void TriggerCooldown(CharacterObject character, PassiveCoordinatedActionDefinition coordDef)
    {
        if (character == null || coordDef == null || coordDef.internalCooldown <= 0f)
        {
            return;
        }

        string actionKey = ResolveActionKey(coordDef);
        cooldownReadyTimes[(character, actionKey)] = Time.time + coordDef.internalCooldown;
    }

    public float GetRemainingCooldown(CharacterObject character, PassiveCoordinatedActionDefinition coordDef)
    {
        if (character == null || coordDef == null) return 0f;
        string actionKey = ResolveActionKey(coordDef);
        if (cooldownReadyTimes.TryGetValue((character, actionKey), out float readyAt))
        {
            return Mathf.Max(0f, readyAt - Time.time);
        }
        return 0f;
    }

    public void ClearCooldowns()
    {
        cooldownReadyTimes.Clear();
    }

    private void OnDisable()
    {
        if (CombatEventHub.Shared != null)
        {
            CombatEventHub.Shared.DamageResolved -= HandleDamageResolved;
        }
        cooldownReadyTimes.Clear();
    }

    private void HandleDamageResolved(DamageResolvedEvent evt)
    {
        if ((evt.acceptedPolicy & DamageProcPolicy.TriggerOrdinaryOnHit) == 0 || evt.target == null || !evt.target.IsAlive)
        {
            return;
        }

        if (TeamLoader.Instance == null)
        {
            return;
        }

        CharacterObject[] team = TeamLoader.Instance.GetTeamSnapshot();
        if (team == null || team.Length == 0)
        {
            return;
        }

        CharacterObject attacker = evt.sourceCharacter;

        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject member = team[i];
            if (member == null)
            {
                continue;
            }

            CombatRuntimeState state = TeamLoader.Instance.GetCharacterCombatRuntimeState(member);
            bool isOnField = TeamLoader.Instance.playerCharacter != null
                && TeamLoader.Instance.playerCharacter.characterObject == member;

            IReadOnlyList<PotentialNodeDefinition> nodes = TeamLoader.Instance.GetUnlockedPotentialNodes(member);
            if (nodes == null || nodes.Count == 0)
            {
                continue;
            }

            // Iterate in reverse order so higher-level potential nodes take precedence
            bool triggeredForMember = false;
            for (int n = nodes.Count - 1; n >= 0; n--)
            {
                PotentialNodeDefinition node = nodes[n];
                if (node == null || node.coordinatedActionEffects == null)
                {
                    continue;
                }

                for (int c = 0; c < node.coordinatedActionEffects.Count; c++)
                {
                    PassiveCoordinatedActionDefinition coordDef = node.coordinatedActionEffects[c];
                    if (coordDef == null)
                    {
                        continue;
                    }
                    if (member == attacker && evt.actionId == coordDef.actionId) continue;

                    // Check internal cooldown
                    if (IsOnCooldown(member, coordDef))
                    {
                        continue;
                    }

                    // Check on-field vs off-field trigger permissions
                    if (isOnField && !coordDef.canTriggerOnField)
                    {
                        continue;
                    }
                    if (!isOnField && !coordDef.canTriggerOffField)
                    {
                        continue;
                    }

                    // Check scope
                    if (coordDef.scope == PassiveEffectScope.Teammates && member == attacker)
                    {
                        // "Teammates" means triggered by other allies attacking, excluding self
                        continue;
                    }
                    if (coordDef.scope == PassiveEffectScope.Self && member != attacker)
                    {
                        continue;
                    }

                    // Check required tags
                    if (coordDef.requiredTags != null && coordDef.requiredTags.Count > 0)
                    {
                        if (state == null)
                        {
                            continue;
                        }

                        bool tagsMet = true;
                        for (int t = 0; t < coordDef.requiredTags.Count; t++)
                        {
                            RuntimeTagRequirement req = coordDef.requiredTags[t];
                            if (req != null && state.HasTag(req.tag) != req.expectedValue)
                            {
                                tagsMet = false;
                                break;
                            }
                        }

                        if (!tagsMet)
                        {
                            continue;
                        }
                    }

                    // Check required counters
                    if (coordDef.requiredCounters != null && coordDef.requiredCounters.Count > 0)
                    {
                        if (state == null)
                        {
                            continue;
                        }

                        bool countersMet = true;
                        for (int cnt = 0; cnt < coordDef.requiredCounters.Count; cnt++)
                        {
                            RuntimeCounterRequirement req = coordDef.requiredCounters[cnt];
                            if (req != null && !req.Matches(state))
                            {
                                countersMet = false;
                                break;
                            }
                        }

                        if (!countersMet)
                        {
                            continue;
                        }
                    }

                    // Check max simultaneous active count
                    int maxActive = coordDef.maxSimultaneousActive > 0 ? coordDef.maxSimultaneousActive : 1;
                    if (ShadowActionExecutor.GetActiveCount(member) >= maxActive)
                    {
                        continue;
                    }

                    // Roll trigger chance
                    float chance = Mathf.Clamp01(coordDef.triggerChance);
                    // A selected higher-rank policy owns the roll; lower ranks must not reroll a failure.
                    triggeredForMember = true;
                    if (chance <= 0f || (chance < 1f && Random.value > chance))
                    {
                        break;
                    }

                    // Trigger cooldown if configured
                    if (coordDef.internalCooldown > 0f)
                    {
                        TriggerCooldown(member, coordDef);
                    }

                    // Spawn coordinated action executor
                    ShadowActionExecutor.Spawn(member, evt.target, coordDef);
                    triggeredForMember = true;
                    break;
                }

                if (triggeredForMember)
                {
                    break;
                }
            }
        }
    }
}
