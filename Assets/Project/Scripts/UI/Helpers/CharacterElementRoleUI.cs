using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Project.UI
{
    /// <summary>
    /// UI component to visualize a character's Element and Role.
    /// Reads from CharacterObject and ElementDatabase to display Element Name/Icon and Role Name.
    /// </summary>
    public class CharacterElementRoleUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text elementRoleText;

        /// <summary>
        /// Populates the UI with the character's element and role data.
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

            // Fetch element icon & display name from ElementDatabase
            var db = ElementDatabase.Load();
            string elementName = "";
            Sprite icon = null;

            if (db != null)
            {
                icon = db.GetIcon(character.elementType);
                var def = db.GetDefinition(character.elementType);
                if (def != null)
                {
                    elementName = def.displayName;
                }
            }

            if (string.IsNullOrEmpty(elementName))
            {
                elementName = character.elementType.ToString();
            }

            // Get role name
            string roleName = character.characterRole.ToString();

            // Apply to UI references
            if (elementIcon != null)
            {
                elementIcon.sprite = icon;
                elementIcon.enabled = icon != null;
            }

            if (elementRoleText != null)
            {
                if (character.characterRole == CharacterRole.None)
                {
                    elementRoleText.text = elementName.ToUpper();
                }
                else
                {
                    elementRoleText.text = $"{elementName.ToUpper()} - {roleName.ToUpper()}";
                }
            }
        }
    }
}
