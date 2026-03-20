using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Perform Action: Notice Exclamation — activates an "!" GameObject above the enemy
    /// and plays a punch-scale pop animation using an AnimationCurve.
    /// Returns Success after the display duration, leaving the marker deactivated.
    /// 
    /// Setup: add a child GameObject named "ExclamationMark" (or assign via Blackboard)
    /// with a SpriteRenderer showing the "!" sprite.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Notice Exclamation",
        description: "Pops an exclamation mark above the enemy to signal player detection, then returns Success.",
        story: "[Agent] notices with exclamation mark",
        category: "Action/Enemy",
        id: "c1b2c3d4e5f60007aabbccddeeff0027")]
    internal partial class NoticeExclamationAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [Tooltip("The '!' child GameObject to activate. If null, searched by name 'ExclamationMark'.")]
        [SerializeReference] public BlackboardVariable<GameObject> ExclamationMark;
        [Tooltip("Total duration to show the '!' before hiding it.")]
        [SerializeReference] public BlackboardVariable<float> DisplayDuration = new BlackboardVariable<float>(0.7f);

        // Pop animation curve: goes 0 → 1.3 → 1.0 over 0..1 time
        private static readonly Keyframe[] PopKeys =
        {
            new Keyframe(0f,   0f,   0f, 10f),
            new Keyframe(0.35f, 1.3f, 0f, 0f),
            new Keyframe(0.6f,  1.0f, 0f, 0f),
            new Keyframe(1f,    1.0f, 0f, 0f)
        };
        private static AnimationCurve s_PopCurve;

        private GameObject m_Mark;
        private float m_Timer;
        private float m_PopDuration = 0.4f; // pop anim runs for this long

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            // Resolve the exclamation mark GameObject
            if (ExclamationMark?.Value != null)
                m_Mark = ExclamationMark.Value;
            else
                m_Mark = FindChildByName(Agent.Value.transform, "ExclamationMark");

            if (m_Mark == null)
            {
                Debug.LogWarning("No ExclamationMark child found. Skipping notice animation.");
                return Status.Success; // skip gracefully
            }

            if (s_PopCurve == null) s_PopCurve = new AnimationCurve(PopKeys);

            m_Mark.SetActive(true);
            m_Mark.transform.localScale = Vector3.zero;
            m_Timer = 0f;

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            m_Timer += Time.deltaTime;

            // Drive pop scale animation
            if (m_Timer <= m_PopDuration)
            {
                float t = m_Timer / m_PopDuration;
                float s = s_PopCurve.Evaluate(t);
                m_Mark.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                // Hold at full scale
                m_Mark.transform.localScale = Vector3.one;
            }

            if (m_Timer >= DisplayDuration.Value)
            {
                m_Mark.SetActive(false);
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (m_Mark != null) m_Mark.SetActive(false);
            m_Mark = null;
        }

        private static GameObject FindChildByName(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child.gameObject;
                GameObject found = FindChildByName(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
