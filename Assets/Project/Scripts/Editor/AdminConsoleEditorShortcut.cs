using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

internal static class AdminConsoleEditorShortcut
{
    [Shortcut("Admin Control/Toggle", KeyCode.C, ShortcutModifiers.Control | ShortcutModifiers.Alt)]
    private static void ToggleAdminControl()
    {
        Debug.Log($"[AdminConsoleShortcut] Unity Editor received Ctrl+Alt+C. isPlaying={EditorApplication.isPlaying}, focusedWindow={EditorWindow.focusedWindow?.GetType().Name ?? "none"}.");
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[AdminConsoleShortcut] Admin Control can only be toggled in Play Mode.");
            return;
        }

        AdminConsole adminConsole = Object.FindFirstObjectByType<AdminConsole>(FindObjectsInactive.Include);
        if (adminConsole == null)
        {
            Debug.LogWarning("[AdminConsoleShortcut] Ctrl+Alt+C was received, but no AdminConsole was found in the loaded scenes.");
            return;
        }

        Debug.Log($"[AdminConsoleShortcut] Found '{adminConsole.name}' in scene '{adminConsole.gameObject.scene.name}', activeSelf={adminConsole.gameObject.activeSelf}, activeInHierarchy={adminConsole.gameObject.activeInHierarchy}.");
        adminConsole.ToggleAdminControl();
    }
}
