using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;

namespace Unity.Behavior
{
    /// <summary>
    /// Perform Action: Notice Exclamation — activates an "!" GameObject above the enemy
    /// prompting player detection, and plays a punch-scale pop animation.
    /// Returns Success after the display duration.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Notice Exclamation",
        description: "Pops an exclamation mark above the enemy to signal player detection, then returns Success.",
        story: "[Agent] notices with exclamation mark",
        category: "Action/Enemy",
        id: "c1b2c3d4e5f60007aabbccddeeff0027")]
    public partial class NoticeExclamationAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
        
        [Tooltip("The '!' child GameObject or Prefab instance to activate. If null, searched by name 'ExclamationMark' on Agent.")]
        [SerializeReference] public BlackboardVariable<GameObject> ExclamationMark = new BlackboardVariable<GameObject>();
        
        [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();

        [Tooltip("Total duration to show the '!' before hiding it.")]
        [SerializeReference] public BlackboardVariable<float> DisplayDuration = new BlackboardVariable<float>(0.7f);

        [Tooltip("If true, the exclamation mark faces the camera every frame.")]
        public bool UseBillboard = true;

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
            if (Agent.Value == null)
                return Status.Failure;

            if (Target != null && Target.Value != null)
            {
                float dirX = Target.Value.transform.position.x - Agent.Value.transform.position.x;
                EnemyFacingUtil.FaceHorizontal(Agent.Value.transform, dirX);
            }

            // Enemies already know the player. Skip the search tell.
            return Status.Success;
        }

        protected override Status OnUpdate()
        {
            if (m_Mark == null) return Status.Failure;
            
            m_Timer += Time.deltaTime;

            // Optional billboard
            UpdateBillboard();

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

        private void UpdateBillboard()
        {
            if (UseBillboard && Camera.main != null && m_Mark != null)
            {
                // Simple billboard to match camera rotation
                m_Mark.transform.rotation = Camera.main.transform.rotation;
            }
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
