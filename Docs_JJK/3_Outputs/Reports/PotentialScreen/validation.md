# Potential Screen validation

- Final scope: no left character/profile section; existing scrollable node layout with I at bottom and IV at top; right-side upgrade preview; blank background; no currency/legend.
- Focused EditMode tests: **5 passed, 0 failed**. See `tests.xml` and `test-summary.txt`.
- Screen and preview visually inspected at 16:9 and 4:3. Screenshots use disposable progression and do not modify saved fragments or Potential levels.
- Unity compilation: no errors.
- Broader existing regression: Camera Girl skill-data expectation is 33, authored value is 43. UI tests pass; unrelated asset/test left unchanged.
- Human review: check wheel/drag feel in the actual Game view, preview placement and selected-state readability.
