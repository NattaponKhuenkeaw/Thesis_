using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Panels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    public sealed partial class CharacterMemoryPanel
    {
        private static readonly Color LockedLine = new Color32(83, 70, 116, 255);
        private static readonly Color EarnedLine = new Color32(188, 136, 255, 255);
        private RectTransform _connectedRows;
        private RectTransform[] _genericPages;
        private CharacterPotentialNodeUI[][] _genericNodes;
        private const float BoardScale = .85f;

        internal bool IsScreenPointInsideNode(Vector2 screenPosition)
        {
            if (_connectedRows != null)
            {
                CharacterPotentialNodeUI[] majorNodes = _connectedRows.GetComponentsInChildren<CharacterPotentialNodeUI>();
                for (int i = 0; i < majorNodes.Length; i++)
                    if (IsScreenPointInside(majorNodes[i], screenPosition)) return true;
            }

            if (_genericNodes == null) return false;
            for (int page = 0; page < _genericNodes.Length; page++)
            {
                CharacterPotentialNodeUI[] nodes = _genericNodes[page];
                if (nodes == null) continue;
                for (int i = 0; i < nodes.Length; i++)
                    if (IsScreenPointInside(nodes[i], screenPosition)) return true;
            }
            return false;
        }

        private static bool IsScreenPointInside(CharacterPotentialNodeUI node, Vector2 screenPosition)
        {
            if (node == null || !node.gameObject.activeInHierarchy) return false;
            RectTransform rect = node.transform as RectTransform;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera);
        }

        internal static CharacterMemoryPanel CreateBoard(ScrollRect scroll, RectTransform[] genericPages)
        {
            var board = new GameObject("Memory Node Map", typeof(RectTransform), typeof(CharacterMemoryPanel));
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            board.layer = viewport.gameObject.layer;
            board.transform.SetParent(viewport, false);
            var panel = board.GetComponent<CharacterMemoryPanel>();
            panel._scroll = scroll;
            panel._list = (RectTransform)board.transform;
            panel._list.localScale = Vector3.one * BoardScale;
            panel._connectedRows = (RectTransform)new GameObject("Connections and Major Nodes", typeof(RectTransform)).transform;
            panel._connectedRows.SetParent(panel._list, false);
            Stretch(panel._connectedRows, Vector2.zero, Vector2.zero);
            panel._genericPages = genericPages;
            panel._genericNodes = new CharacterPotentialNodeUI[genericPages.Length][];
            for (int page = 0; page < genericPages.Length; page++)
            {
                genericPages[page].SetParent(panel._list, false);
                panel._genericNodes[page] = genericPages[page].GetComponentsInChildren<CharacterPotentialNodeUI>(true);
                Array.Sort(panel._genericNodes[page], (a, b) =>
                    ((RectTransform)a.transform).anchoredPosition.y.CompareTo(((RectTransform)b.transform).anchoredPosition.y));
            }
            return panel;
        }

        internal void ShowConnectedTrack(CharacterItem character, CharacterMemoryProgress memory, Selectable[] headerButtons,
            CharacterPotentialNodeUI majorTemplate, int page, float originalWidth, TMP_FontAsset font,
            Func<PotentialTrack, Sprite> potentialIcon,
            Action<Button, string, int, PotentialTrack, string, string, Sprite> preview,
            Selectable[][] footerRows = null, (int step, PotentialTrack family)? focusNode = null, Vector2? scrollPosition = null)
        {
            ClearRows();
            for (int index = 0; index < _genericPages.Length; index++)
                _genericPages[index].gameObject.SetActive(index == page);
            CharacterPotentialNodeUI[] genericNodes = _genericNodes[page];
            Vector2 majorSize = majorTemplate != null ? ((RectTransform)majorTemplate.transform).sizeDelta : new Vector2(175f, 175f);
            Vector2 genericSize = ((RectTransform)genericNodes[0].transform).sizeDelta;
            float branchHeight = majorSize.y * .56f;
            float stepSpacing = majorSize.y + genericSize.y * .6f;
            float endPadding = stepSpacing * .68f;
            RectTransform viewport = (RectTransform)_list.parent;
            _list.anchorMin = Vector2.zero;
            _list.anchorMax = Vector2.right;
            _list.pivot = new Vector2(.5f, 0f);
            _list.sizeDelta = new Vector2(Mathf.Max(majorSize.x, originalWidth - viewport.rect.width),
                (CharacterMemoryProgress.StepsPerPage - 1) * stepSpacing + 2f * endPadding);
            _list.anchoredPosition = Vector2.zero;
            _scroll.content = _list;
            _scroll.horizontal = _scroll.vertical = true;
            _scroll.scrollSensitivity = stepSpacing;

            var links = new GameObject("Connections", typeof(RectTransform));
            links.transform.SetParent(_connectedRows, false);
            Stretch((RectTransform)links.transform, Vector2.zero, Vector2.zero);
            var rows = new List<Selectable[]>(CharacterMemoryProgress.StepsPerPage + 2);
            Button focused = null;
            int genericIndex = 0;
            for (int localStep = 0; localStep < CharacterMemoryProgress.StepsPerPage; localStep++)
            {
                int step = page * CharacterMemoryProgress.StepsPerPage + localStep + 1;
                bool major = CharacterMemoryProgress.IsChoiceStep(step);
                CharacterPotentialNodeUI genericNode = major ? null : genericNodes[genericIndex++];
                float y = major ? endPadding + localStep * stepSpacing : ((RectTransform)genericNode.transform).anchoredPosition.y;
                bool stepEarned = step <= memory.CompletedSteps;
                bool centerEarned = stepEarned && (!major || memory.GetChoice(step) == PotentialTrack.Ultimate);
                AddLink(links.transform, .5f, .5f, y - branchHeight, y + branchHeight,
                    centerEarned ? EarnedLine : LockedLine);
                if (localStep > 0)
                    AddLink(links.transform, .5f, .5f, y - stepSpacing + branchHeight, y - branchHeight,
                        stepEarned ? EarnedLine : LockedLine);

                var row = new GameObject($"Step {step}", typeof(RectTransform));
                row.transform.SetParent(_connectedRows, false);
                var rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = Vector2.zero;
                rowRect.anchorMax = Vector2.right;
                rowRect.pivot = new Vector2(.5f, .5f);
                rowRect.anchoredPosition = new Vector2(0, y);
                rowRect.sizeDelta = new Vector2(0, stepSpacing);

                TMP_Text number = AddText(row.transform, $"{step:00}", 24, font);
                Place(number.rectTransform, .5f, 0, -22, 0, 22, .035f, .1f);
                number.color = stepEarned ? EarnedLine : Color.white;

                var rowButtons = new Button[major ? CharacterMemoryProgress.ChoiceCount : 1];
                rows.Add(rowButtons);
                for (int lane = 0; lane < rowButtons.Length; lane++)
                {
                    var family = (PotentialTrack)lane;
                    float x = major ? (lane + .5f) / CharacterMemoryProgress.ChoiceCount : .5f;
                    bool earned = IsComplete(memory, step, family);
                    int rank = memory.GetPreviewRank(step, family);
                    bool full = rank > CharacterMemoryProgress.UpgradesPerFamily;
                    bool next = step == memory.CompletedSteps + 1 && !full;
                    string status = earned ? "Earned" : full ? "Unavailable"
                        : stepEarned ? "Other family chosen" : next ? "Available from a Memory reward" : "Locked";
                    var state = earned ? CharacterPotentialNodeState.Upgraded : next
                        ? CharacterPotentialNodeState.Upgradeable : CharacterPotentialNodeState.Locked;
                    if (major && lane != 1)
                        AddBranch(links.transform, x, y, branchHeight, earned ? EarnedLine : LockedLine);

                    var node = genericNode;
                    if (major)
                    {
                        node = majorTemplate != null ? Instantiate(majorTemplate, row.transform)
                            : CharacterPotentialNodeUI.CreateRuntimeNode(row.transform);
                        var nodeRect = (RectTransform)node.transform;
                        nodeRect.anchorMin = nodeRect.anchorMax = new Vector2(x, .5f);
                        nodeRect.pivot = new Vector2(.5f, .5f);
                        nodeRect.anchoredPosition = Vector2.zero;
                        nodeRect.sizeDelta = majorSize;
                    }
                    string title = CharacterMemoryProgress.DescribeStep(step, family);
                    if (major) node.name = title;
                    if (major && !full) title += $" Lv.{rank}";
                    Sprite icon = major ? potentialIcon?.Invoke(family) ?? MemoryNodeIcon(character, family)
                        : CharacterMemoryKit.GetGenericPageIcon(character?.characterObjectData, step)
                            ?? MemoryNodeIcon(character);
                    string caption = major ? family.ToString() : "Generic";
                    if (major && !full) caption += $" Lv.{rank}";
                    if (earned) caption += " · Earned";
                    else if (next) caption += " · Next";
                    string description = DescribeMemoryEffect(character, step, family, rank);
                    Button button = node.GetComponent<Button>();
                    rowButtons[lane] = node.BindMemoryPreview(caption, icon, state, () =>
                        preview(button, title, step, family, description, status, icon), full, font);
                    if (focusNode.HasValue && focusNode.Value.step == step && focusNode.Value.family == family)
                        focused = button;
                    node.gameObject.SetActive(true);
                }
            }

            Selectable focus = focused != null ? focused : rows[0][1];
            rows.Reverse();
            if (headerButtons.Length > 0) rows.Insert(0, headerButtons);
            if (footerRows != null)
                foreach (Selectable[] footer in footerRows)
                    if (footer.Length > 0) rows.Add(footer);
            BindNavigation(rows, focus);
            Canvas.ForceUpdateCanvases();
            _scroll.normalizedPosition = scrollPosition ?? new Vector2(.5f, 0f);
        }

        private static void AddBranch(Transform links, float lane, float y, float branchHeight, Color color)
        {
            float left = Mathf.Min(.5f, lane);
            float right = Mathf.Max(.5f, lane);
            AddLink(links, left, right, y + branchHeight, y + branchHeight, color);
            AddLink(links, lane, lane, y - branchHeight, y + branchHeight, color);
            AddLink(links, left, right, y - branchHeight, y - branchHeight, color);
        }

        private static void AddLink(Transform parent, float left, float right, float bottom, float top, Color color)
        {
            var host = new GameObject("Connector", typeof(RectTransform), typeof(Image));
            host.transform.SetParent(parent, false);
            var image = host.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Place(image.rectTransform, 0f, -2, bottom - 2, 2, top + 2, left, right);
        }

        internal static Sprite MemoryNodeIcon(CharacterItem item, PotentialTrack? family = null)
        {
            CharacterObject character = item?.characterObjectData;
            var art = character?.potentialArtwork;
            var kit = character?.kitDefinition;
            Sprite portrait = item?.icon ?? character?.metaData?.portrait;
            return family switch
            {
                PotentialTrack.Skill => art?.skill ?? kit?.skill?.icon ?? portrait,
                PotentialTrack.Ultimate => art?.ultimate ?? kit?.ultimate?.icon ?? portrait,
                PotentialTrack.Passive => art?.passive ?? kit?.passive?.icon ?? portrait,
                _ => portrait
            };
        }

        internal static string DescribeMemoryEffect(CharacterItem character, int step, PotentialTrack family, int rank)
        {
            bool major = CharacterMemoryProgress.IsChoiceStep(step);
            if (major && rank > CharacterMemoryProgress.UpgradesPerFamily)
                return $"This row is beyond the {CharacterMemoryProgress.UpgradesPerFamily} {family} Memory ranks.";
            if (CharacterMemoryKit.AppliesTo(character?.characterObjectData))
            {
                if (!major) return CharacterMemoryKit.DescribeGeneric(character.characterObjectData, step);
                return CharacterMemoryKit.GetNode(character.characterObjectData, family, rank)?.description
                    ?? $"{family} Memory Lv.{rank}";
            }
            return major ? $"Raises {family} Memory to Lv.{rank} and advances the track to step {step}."
                : $"Advances the Memory track to step {step} / {CharacterMemoryProgress.StepCount}.";
        }
    }
}
