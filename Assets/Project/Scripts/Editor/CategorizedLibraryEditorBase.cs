using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    public abstract class CategorizedLibraryEditorBase : UnityEditor.Editor
    {
        private string search = string.Empty;

        protected abstract string CategoriesPropertyName { get; }
        protected abstract bool MatchesSearch(int categoryIndex, string searchText);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSearchToolbar();

            SerializedProperty categories = serializedObject.FindProperty(CategoriesPropertyName);
            if (categories == null)
            {
                EditorGUILayout.HelpBox($"Missing serialized property '{CategoriesPropertyName}'.", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawCategories(categories);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSearchToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Search", GUILayout.Width(45f));
                search = GUILayout.TextField(search ?? string.Empty, GUI.skin.FindStyle("ToolbarSeachTextField") ?? GUI.skin.textField);
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45f)))
                {
                    search = string.Empty;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawCategories(SerializedProperty categories)
        {
            bool drewAny = false;

            for (int i = 0; i < categories.arraySize; i++)
            {
                if (!MatchesSearch(i, search))
                {
                    continue;
                }

                drewAny = true;
                SerializedProperty category = categories.GetArrayElementAtIndex(i);
                DrawCategory(categories, i, category);
                EditorGUILayout.Space(4f);
            }

            if (!drewAny)
            {
                EditorGUILayout.HelpBox("No categories matched the current search.", MessageType.Info);
            }

            if (GUILayout.Button("Add Category"))
            {
                categories.arraySize++;
                SerializedProperty newCategory = categories.GetArrayElementAtIndex(categories.arraySize - 1);
                SerializedProperty nameProperty = newCategory.FindPropertyRelative("categoryName");
                if (nameProperty != null && string.IsNullOrWhiteSpace(nameProperty.stringValue))
                {
                    nameProperty.stringValue = "New Category";
                }
            }
        }

        private void DrawCategory(SerializedProperty categories, int categoryIndex, SerializedProperty category)
        {
            SerializedProperty nameProperty = category.FindPropertyRelative("categoryName");
            SerializedProperty entriesProperty = category.FindPropertyRelative("entries");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (nameProperty != null)
                    {
                        EditorGUILayout.PropertyField(nameProperty, GUIContent.none);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                    {
                        categories.DeleteArrayElementAtIndex(categoryIndex);
                        return;
                    }
                }

                if (entriesProperty != null)
                {
                    EditorGUILayout.PropertyField(entriesProperty, true);
                }
            }
        }
    }
}
