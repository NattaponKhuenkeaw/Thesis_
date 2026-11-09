# Memory pages and Training controls

The connected Memory map now displays 18 steps at a time. Pages II and III unlock after steps 18 and 36. Saved progress and combat effects use the original step numbering. Nightmare and EX Memory remain future work.

TeamCanvas contains twelve editable generic prefab instances per page. The pages use the existing Soul, blue Aura, and purple Aura sprites. The panel binds progress and preview actions to those instances.

Training Config can clear the selected character's three Memory pages in its draft. Apply commits the reset to the practice session, and Cancel discards it. The Global Level slider now stretches its track with the footer and fills to the handle.

The Memory view centers the page tabs and keeps only the character name in the header. The controls use GNF TTF and the existing UI frames. The map uses a fixed smaller scale. Locked tabs keep the current page open and show their requirement in a GNF TTF notice that slides up and fades.

### Memory preview node switching

- Fixed Memory map clicks being treated as outside clicks while Upgrade Preview is open. Clicking another active major or generic node now keeps the preview open so it can display the newly selected node. Clicking elsewhere still dismisses the preview. Static source review completed; Play Mode verification remains for the feature playtest checkpoint.
