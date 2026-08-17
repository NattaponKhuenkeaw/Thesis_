using Project.InventorySystem.Artifacts;
using Project.InventorySystem.Items;

namespace Project.TeamBuilder.UI.Interfaces
{
    public interface IInspectSubPanel
    {
        /// <summary>
        /// Populates the panel with data from the currently inspected character and their equipped artifacts.
        /// </summary>
        void PopulateData(CharacterItem character, ArtifactInstance[] equippedArtifacts);

        /// <summary>
        /// Called when the panel should become visible.
        /// </summary>
        void Show();

        /// <summary>
        /// Called when the panel should be hidden.
        /// </summary>
        void Hide();
    }
}
