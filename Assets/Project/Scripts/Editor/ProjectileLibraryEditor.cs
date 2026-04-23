using UnityEditor;
using Project.EditorTools;

[CustomEditor(typeof(ProjectileLibrary))]
public class ProjectileLibraryEditor : CategorizedLibraryEditorBase
{
    protected override string CategoriesPropertyName => "categories";

    protected override bool MatchesSearch(int categoryIndex, string searchText)
    {
        ProjectileLibrary library = (ProjectileLibrary)target;
        return library != null && library.CategoryMatchesSearch(categoryIndex, searchText);
    }
}
