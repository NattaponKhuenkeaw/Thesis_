# Save Select Biome Action Panel

Connected the existing Biome value in the shared save action panel to the selected save header, displaying the player-facing 1-based biome number. The save Window now stays at alpha `1` when actions open; interaction and raycast blocking still disable behind the panel.

Root cause: `SaveSelectPanel.SetActionsVisible` explicitly assigned `.45` to the Window CanvasGroup on open, and the serialized Biome value was being treated as action currency.

No Unity Play Mode validation or test run was requested.
