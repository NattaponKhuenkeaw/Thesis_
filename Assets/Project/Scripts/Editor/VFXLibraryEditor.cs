using UnityEditor;
using Project.Combat;
using Project.EditorTools;

[CustomEditor(typeof(VFXLibrary))]
public class VFXLibraryEditor : CategorizedLibraryEditorBase
{
    protected override string CategoriesPropertyName => "categories";

    protected override bool MatchesSearch(int categoryIndex, string searchText)
    {
        VFXLibrary library = (VFXLibrary)target;
        return library != null && library.CategoryMatchesSearch(categoryIndex, searchText);
    }
}
