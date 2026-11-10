# Camera Player Reference and Look-Ahead Follow-Up

The compile fix removed the stale `CameraLookAhead.ResolvePlayerBody` calls. Follow-up camera testing exposed that the replacement `CameraManager.EnsurePlayerReferences` also overwrote the authored `_playerTransform` with the PlayerMovement Rigidbody transform every frame and on room entry. Removed that manager-side rebinding; `CameraManager` now retains its configured `CameraLookAheadTarget`, while `CameraLookAhead` resolves and binds its own Rigidbody.

Reverted the experimental vertical dead-zone/fall compensation. The look-ahead proxy again mirrors player Y directly, and its stale serialized tuning fields were removed from the camera prefab. Static source inspection and `git diff --check` passed; Unity compilation and in-editor room-entry/character-swap playtest were not run.
