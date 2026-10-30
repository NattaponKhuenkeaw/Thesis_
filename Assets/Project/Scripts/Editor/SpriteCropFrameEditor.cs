using Project.Art;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(SpriteCropFrame))]
public class SpriteCropFrameEditor : Editor
{
    private readonly BoxBoundsHandle bounds = new BoxBoundsHandle
    {
        axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y,
        handleColor = Color.cyan,
        wireframeColor = Color.cyan
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceSprite"), new GUIContent("Source Sprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cropSize"), new GUIContent("Crop Size", "Visible window size. Does not resize or repeat the image."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("imageOffset"), new GUIContent("Image Offset", "Move the original image inside the crop window."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("imagePixelScale"), new GUIContent("Image Pixel Scale", "Intentional image zoom. Leave at 1 to preserve native pixel size."));
        serializedObject.ApplyModifiedProperties();
        var frame = (SpriteCropFrame)target;
        frame.Refresh();
        EditorGUILayout.Space();
        if (GUILayout.Button("Fit Frame To Image"))
        {
            RecordUndo(frame, "Fit crop frame"); frame.FitImage(); RecordPrefabChanges(frame);
        }
        EditorGUILayout.HelpBox("Drag the cyan crop handles in Scene view. Scale changes the window, not the image. Areas beyond the source image stay transparent. Colliders are separate.", MessageType.Info);
        if (frame.Artwork == null || frame.ClippingMask == null)
            EditorGUILayout.HelpBox("Use a CropBuilding prefab or GameObject > 2D Object > Sprite Crop Frame to create the required artwork and mask.", MessageType.Warning);
    }

    private void OnSceneGUI()
    {
        var frame = (SpriteCropFrame)target;
        frame.Refresh();
        using (new Handles.DrawingScope(frame.transform.localToWorldMatrix))
        {
            bounds.center = Vector3.zero;
            bounds.size = new Vector3(frame.CropSize.x, frame.CropSize.y, 0);
            EditorGUI.BeginChangeCheck(); bounds.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                RecordUndo(frame, "Resize sprite crop");
                frame.ResizeLocalRect(new Rect((Vector2)bounds.center - (Vector2)bounds.size * .5f, bounds.size));
                RecordPrefabChanges(frame);
            }
        }
    }

    private static void RecordUndo(SpriteCropFrame frame, string label)
    {
        Undo.RecordObject(frame, label); Undo.RecordObject(frame.transform, label);
        if (frame.Artwork != null) Undo.RecordObject(frame.Artwork.transform, label);
        if (frame.ClippingMask != null) Undo.RecordObject(frame.ClippingMask.transform, label);
    }
    private static void RecordPrefabChanges(SpriteCropFrame frame)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(frame);
        PrefabUtility.RecordPrefabInstancePropertyModifications(frame.transform);
        if (frame.Artwork != null) PrefabUtility.RecordPrefabInstancePropertyModifications(frame.Artwork.transform);
        if (frame.ClippingMask != null) PrefabUtility.RecordPrefabInstancePropertyModifications(frame.ClippingMask.transform);
        SceneView.RepaintAll();
    }
}
