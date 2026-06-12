using UnboundGP.Core;
using UnboundGP.Data;
using UnityEngine;
using UnityEngine.UI;

namespace UnboundGP.UI
{
    /// <summary>
    /// 研究ツリー画面。Tier ごとにノードを一覧し、研究ポイントで解放する。
    /// 各ノードには「現実のF1での顛末」(禁止された年など) を併記し、
    /// 解放の快感と同時に“なぜ禁止されたのか”という問いを植え付ける。
    /// </summary>
    public class ResearchTreeView : MonoBehaviour
    {
        GameContext ctx;
        RectTransform content;

        public void Init(GameContext context)
        {
            ctx = context;
            var scroll = UiFactory.CreateScrollView(transform, out content);
            UiFactory.SetAnchors((RectTransform)scroll.transform, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.98f));
            Rebuild();
        }

        public void Rebuild()
        {
            if (content == null) return;
            foreach (Transform child in content) Destroy(child.gameObject);

            // Tier 順に整列
            int maxTier = 0;
            foreach (var n in ctx.TechTree.nodes)
                if (n != null && n.tier > maxTier) maxTier = n.tier;

            for (int tier = 0; tier <= maxTier; tier++)
            {
                var tierHeader = UiFactory.CreateText(content, $"── TIER {tier} ──", 24, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f), true);
                UiFactory.SetLayoutHeight(tierHeader.gameObject, 36f);

                foreach (var node in ctx.TechTree.nodes)
                {
                    if (node == null || node.tier != tier) continue;
                    BuildNodeRow(node);
                }
            }
        }

        void BuildNodeRow(TechNodeData node)
        {
            bool unlocked = ctx.IsUnlocked(node);
            bool prereqOk = ctx.PrerequisitesMet(node);
            bool canUnlock = ctx.CanUnlock(node);

            Color bg = unlocked ? new Color(0.12f, 0.3f, 0.16f, 0.9f)
                : prereqOk ? new Color(0.16f, 0.18f, 0.26f, 0.9f)
                : new Color(0.1f, 0.1f, 0.12f, 0.9f);

            var row = UiFactory.CreatePanel(content, bg, "Node_" + node.techId);
            UiFactory.SetLayoutHeight(row.gameObject, 132f);

            // タイトル行
            string status = unlocked ? "<color=#7be07b>解放済み</color>"
                : !prereqOk ? "<color=#888888>前提技術が必要</color>"
                : canUnlock ? $"<color=#ffd24d>解放可能 ── {node.researchCost} RP</color>"
                : $"<color=#e07b7b>RP不足 ── {node.researchCost} RP</color>";
            var title = UiFactory.CreateText(row, $"<b>{node.displayName}</b>    {status}", 22, TextAnchor.MiddleLeft);
            title.supportRichText = true;
            UiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0f, 0.7f), new Vector2(0.8f, 1f), new Vector2(16f, 0f), Vector2.zero);

            // 効果と説明
            var desc = UiFactory.CreateText(row,
                node.description + "   <color=#9ecbff>[" + DescribeModifiers(node) + "]</color>",
                17, TextAnchor.UpperLeft, new Color(0.85f, 0.87f, 0.95f));
            desc.supportRichText = true;
            UiFactory.SetAnchors((RectTransform)desc.transform, new Vector2(0f, 0.34f), new Vector2(0.8f, 0.7f), new Vector2(16f, 0f), Vector2.zero);

            // 現実のF1での顛末 (テーマの種まき)
            var note = UiFactory.CreateText(row, "現実のF1では: " + node.realWorldNote, 15, TextAnchor.UpperLeft, new Color(0.95f, 0.7f, 0.55f));
            UiFactory.SetAnchors((RectTransform)note.transform, new Vector2(0f, 0.02f), new Vector2(0.8f, 0.34f), new Vector2(16f, 0f), Vector2.zero);

            // 前提技術の表示
            if (node.prerequisites.Count > 0)
            {
                var names = new System.Text.StringBuilder("要: ");
                for (int i = 0; i < node.prerequisites.Count; i++)
                {
                    if (i > 0) names.Append(", ");
                    names.Append(node.prerequisites[i] != null ? node.prerequisites[i].displayName : "?");
                }
                var pre = UiFactory.CreateText(row, names.ToString(), 14, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.55f));
                UiFactory.SetAnchors((RectTransform)pre.transform, new Vector2(0.55f, 0.72f), new Vector2(0.99f, 0.98f), Vector2.zero, new Vector2(-8f, 0f));
            }

            // 解放ボタン
            if (canUnlock)
            {
                var btn = UiFactory.CreateButton(row, "研究する", () => ctx.TryUnlock(node), new Color(0.85f, 0.6f, 0.1f), 20);
                UiFactory.SetAnchors((RectTransform)btn.transform, new Vector2(0.82f, 0.25f), new Vector2(0.98f, 0.75f));
            }
        }

        static string DescribeModifiers(TechNodeData node)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in node.modifiers)
            {
                if (sb.Length > 0) sb.Append("  ");
                string label = m.stat switch
                {
                    StatType.TopSpeedKmh => "最高速",
                    StatType.Acceleration => "加速",
                    StatType.MechanicalGrip => "グリップ",
                    StatType.DownforceFactor => "ダウンフォース",
                    StatType.Drag => "空気抵抗",
                    StatType.WeightKg => "車重",
                    StatType.Reliability => "信頼性",
                    _ => m.stat.ToString(),
                };
                if (!Mathf.Approximately(m.add, 0f))
                    sb.Append($"{label}{(m.add > 0 ? "+" : "")}{m.add:0.##}");
                if (!Mathf.Approximately(m.multiply, 1f))
                    sb.Append($"{label}x{m.multiply:0.##}");
            }
            return sb.ToString();
        }
    }
}
