# Character Inspector folder layout — 2026-10-29

The user requested the supplied identity-card/folder Inspector design using game assets. TeamCanvas now has a vertical portrait rail, large identity card with the existing city background, INFO/SKILLS tabs and a yellow-framed document. Info binds shared Run Global Level, primary/secondary stats and twelve Potential indicators to existing data. Equipment and the original full-screen Potential workflow remain available.

Decorative sprite/texture references are sourced only from `Assets/Project/UI`; character portraits and ability icons remain sourced from character definitions, with the existing project font. Skills use a dedicated wrapping/content-sized entry template; long descriptions remain scrollable. Empty inventory hides stale identity/document content.

Folder tab styling and the unscaled slide/fade live on InspectScreenUI. The initial separate CharacterInspectorFolder type failed to enter Unity's compiler source list, producing the reported CS0246. Removing that dependency and keeping the presentation on the existing controller resolved the error; both runtime and Editor assemblies were recompiled successfully. No project/compiler settings or package dependencies were changed.

Character selection preserves rail objects and scroll position instead of recreating the roster. Selected cards use a yellow border; opening another character preserves the current Info/Skills page. Drawer motion translates/fades its document and never changes actual content scale. Existing MetaProgression, inventory and equipment authorities are retained.

Author/apply and validation run through the installed Unity MCP bridge. The authoring and validation menu commands are retained for future edits. The validation uses temporary prefab contents and inventory, without saving/replacing the user's scene. Final test counts and reviewed renders are stored under `3_Outputs/Reports/CharacterInspectorFolder/`. Play Mode timing/visual feel remains for human review.
