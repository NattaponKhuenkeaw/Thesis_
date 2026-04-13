using UnityEngine;

namespace CombatCore
{
    public interface ISkillComponent
    {
        /// <summary>
        /// Standardized initialization for skill-spawned prefabs.
        /// </summary>
        /// <param name="owner">Reference to the character that spawned it.</param>
        /// <param name="step">The attack step that triggered this spawn (holds logic values).</param>
        /// <param name="direction">The facing direction of the owner.</param>
        void Initialize(PlayerCharacter owner, AttackStepData step, Vector2 direction);
    }
}
