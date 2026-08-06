using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Project.UI
{
    /// <summary>
    /// Legacy prefab-compatible component that displays a character's role.
    /// </summary>
    public class CharacterElementRoleUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text elementRoleText;

        /// <summary>
        /// Populates the UI with the character's role data.
        /// </summary>
        /// <param name="character">The character to read details from.</param>
        public void SetCharacter(CharacterObject character)
        {
            if (character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            string roleName = character.characterRole.ToString();

            if (elementIcon != null)
            {
                elementIcon.enabled = false;
            }

            if (elementRoleText != null)
            {
                elementRoleText.text = character.characterRole == CharacterRole.None
                    ? string.Empty
                    : roleName.ToUpperInvariant();
            }
        }
    }
}
