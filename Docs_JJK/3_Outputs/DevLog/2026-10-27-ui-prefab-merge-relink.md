# 2026-10-27 - UI Prefab Merge Relink

## Bugs Found

Merge `e354d352` left the `Resources/UI` variants for `RoomChoiceUI` and `SaveSelectPanel` pointing to authored prefab GUIDs whose source assets were removed. `MainMenu.unity` also references the missing `SaveSelectPanel` source GUID.

## Changes

- Restored `Assets/Project/Prefab/UI/RoomChoiceUI.prefab` and its original `.meta` file.
- Restored `Assets/Project/Prefab/UI/SaveSelectPanel.prefab` and its original `.meta` file.
- Kept the Resources variants, runtime code, and MainMenu scene reference unchanged.

## Verification

- Confirmed every source-prefab fileID targeted by each Resources variant exists in its restored source.
- Confirmed the MainMenu `saveSelectPanel` GUID matches the restored source prefab GUID.
- The staged diff check only flags Unity-serialized empty YAML values with trailing spaces (for example, empty `m_Name` fields). Unity editor/play-mode verification was not run in this session.
