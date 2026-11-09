using System;
using System.Collections.Generic;
using Project.Core.ZoneState;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>Read-only route presentation. All graphics and extra node instances come from authored prefabs.</summary>
    public sealed class RoomRouteProgressUI : MonoBehaviour
    {
        [Serializable]
        private struct KindIcon { public RoomKind kind; public Sprite sprite; }
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text positionLabel;
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform remainingLine;
        [SerializeField] private RectTransform completedLine;
        [SerializeField] private RoomRouteProgressNode nodePrefab;
        [SerializeField] private RoomRouteProgressNode[] authoredNodes;
        [SerializeField] private KindIcon[] kindIcons;

        private readonly List<RoomRouteProgressNode> nodes = new List<RoomRouteProgressNode>();
        private int nextIndex = -1;
        public int VisibleNodeCount { get; private set; }
        public int NextIndex => nextIndex;

        public void Bind(IReadOnlyList<BiomeActNode> route, int offeredIndex, int level, bool extraRoom)
        {
            if (route == null || route.Count == 0)
            {
                VisibleNodeCount = 0; nextIndex = -1; gameObject.SetActive(false); return;
            }
            gameObject.SetActive(true);
            if (nodes.Count == 0 && authoredNodes != null) nodes.AddRange(authoredNodes);
            while (nodes.Count < route.Count) nodes.Add(Instantiate(nodePrefab, track));
            VisibleNodeCount = route.Count;
            nextIndex = extraRoom ? -1 : Mathf.Clamp(offeredIndex, 0, route.Count - 1);
            levelLabel.text = "LEVEL " + Mathf.Max(1, level);
            positionLabel.text = extraRoom ? "NEXT: BONUS ROOM" : $"NEXT ROOM {nextIndex + 1} / {route.Count}";
            if (nextIndex >= 0 && route[nextIndex] == BiomeActNode.SafeRoom)
                positionLabel.text += " · SAFE ROOM";
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                node.gameObject.SetActive(i < route.Count);
                if (i >= route.Count) continue;
                var rect = (RectTransform)node.transform;
                float x = (i + .5f) / route.Count;
                rect.anchorMin = rect.anchorMax = new Vector2(x, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one * Mathf.Min(1f, 14f / route.Count);
                RoomKind kind = ToRoomKind(route[i]);
                node.Bind(i, nextIndex >= 0 && i < nextIndex, i == nextIndex, kind == RoomKind.Boss, Icon(kind));
            }
            float first = .5f / route.Count;
            SetLine(remainingLine, first, 1f - first);
            completedLine.gameObject.SetActive(nextIndex > 0);
            SetLine(completedLine, first, (Mathf.Max(0, nextIndex) + .5f) / route.Count);
        }

        public void SetNextRoom(RoomChoiceOffer offer)
        {
            if (!offer.IsValid) return;
            if (nextIndex < 0)
            {
                positionLabel.text = offer.Kind == RoomKind.Rest ? "NEXT: RECOVERY" : "NEXT: " + offer.Kind.ToString().ToUpperInvariant();
                return;
            }
            if (nextIndex >= VisibleNodeCount) return;
            nodes[nextIndex].SetIcon(offer.Room.OfferIcon != null ? offer.Room.OfferIcon : Icon(offer.Kind), offer.Kind == RoomKind.Boss);
        }

        private static void SetLine(RectTransform line, float start, float end)
        {
            line.anchorMin = new Vector2(start, .5f);
            line.anchorMax = new Vector2(end, .5f);
            line.anchoredPosition = Vector2.zero;
            line.sizeDelta = new Vector2(0f, 4f);
        }

        private Sprite Icon(RoomKind kind)
        {
            if (kindIcons != null)
                foreach (var entry in kindIcons) if (entry.kind == kind) return entry.sprite;
            return null;
        }

        private static RoomKind ToRoomKind(BiomeActNode node) => node switch
        {
            BiomeActNode.Shop => RoomKind.Shop,
            BiomeActNode.Level => RoomKind.Level,
            BiomeActNode.Rest => RoomKind.Rest,
            BiomeActNode.SafeRoom => RoomKind.Reward,
            BiomeActNode.Boss => RoomKind.Boss,
            _ => RoomKind.Combat
        };
    }
}
