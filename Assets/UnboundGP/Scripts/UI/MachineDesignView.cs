using UnboundGP.Core;
using UnboundGP.Data;
using UnboundGP.Design;
using UnityEngine;
using UnityEngine.UI;

namespace UnboundGP.UI
{
    /// <summary>
    /// マシン設計画面。解放済み技術の装備/取り外しで最終性能がリアルタイムに変化する。
    ///
    /// 右側の性能パネルには「人間が乗った場合」「AIが乗った場合」の理論ラップを併記し、
    /// マシンを強化するほど両者の差が開いていく=人間がボトルネックになっていく様子を
    /// レース前から数字で予感させる。
    /// </summary>
    public class MachineDesignView : MonoBehaviour
    {
        GameContext ctx;
        RectTransform listContent;
        Text statsText;

        public void Init(GameContext context)
        {
            ctx = context;

            // 左: 装備リスト
            var scroll = UiFactory.CreateScrollView(transform, out listContent);
            UiFactory.SetAnchors((RectTransform)scroll.transform, new Vector2(0.03f, 0.02f), new Vector2(0.5f, 0.98f));

            // 右: 性能パネル
            var statsPanel = UiFactory.CreatePanel(transform, new Color(0f, 0f, 0f, 0.35f), "StatsPanel");
            UiFactory.SetAnchors(statsPanel, new Vector2(0.52f, 0.02f), new Vector2(0.97f, 0.98f));
            statsText = UiFactory.CreateText(statsPanel, "", 20, TextAnchor.UpperLeft);
            statsText.supportRichText = true;
            statsText.lineSpacing = 1.18f;
            UiFactory.SetAnchors((RectTransform)statsText.transform, Vector2.zero, Vector2.one, new Vector2(24f, 16f), new Vector2(-24f, -16f));

            Rebuild();
        }

        public void Rebuild()
        {
            if (listContent == null) return;
            foreach (Transform child in listContent) Destroy(child.gameObject);

            var header = UiFactory.CreateText(listContent, $"シャシー: {ctx.Chassis.chassisName}\n解放済み技術 (クリックで装備切替)", 20, TextAnchor.MiddleLeft, Color.white, true);
            UiFactory.SetLayoutHeight(header.gameObject, 64f);

            foreach (var node in ctx.TechTree.nodes)
            {
                if (node == null || !ctx.IsUnlocked(node)) continue;
                BuildEquipRow(node);
            }

            RefreshStats();
        }

        void BuildEquipRow(TechNodeData node)
        {
            bool equipped = ctx.IsEquipped(node);
            var row = UiFactory.CreatePanel(listContent,
                equipped ? new Color(0.15f, 0.32f, 0.2f, 0.95f) : new Color(0.14f, 0.15f, 0.2f, 0.95f),
                "Equip_" + node.techId);
            UiFactory.SetLayoutHeight(row.gameObject, 64f);

            var label = UiFactory.CreateText(row,
                (equipped ? "<color=#7be07b>[装備中]</color> " : "<color=#777777>[未装備]</color> ") + node.displayName,
                20, TextAnchor.MiddleLeft);
            label.supportRichText = true;
            UiFactory.SetAnchors((RectTransform)label.transform, Vector2.zero, new Vector2(0.7f, 1f), new Vector2(16f, 0f), Vector2.zero);

            var btn = UiFactory.CreateButton(row, equipped ? "外す" : "装備", () => ctx.SetEquipped(node, !equipped),
                equipped ? new Color(0.5f, 0.25f, 0.2f) : new Color(0.2f, 0.45f, 0.3f), 18);
            UiFactory.SetAnchors((RectTransform)btn.transform, new Vector2(0.74f, 0.15f), new Vector2(0.97f, 0.85f));
        }

        void RefreshStats()
        {
            var baseStats = ctx.Chassis.baseStats;
            var stats = ctx.CurrentMachineStats();
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("<b>━━ 最終性能 (ベース → 現在) ━━</b>");
            sb.AppendLine(Row("最高速度", baseStats.EffectiveTopSpeedKmh, stats.EffectiveTopSpeedKmh, "0", " km/h"));
            sb.AppendLine(Row("加速力", baseStats.acceleration, stats.acceleration, "0.0", " m/s²"));
            sb.AppendLine(Row("機械的グリップ", baseStats.mechanicalGrip, stats.mechanicalGrip, "0.00", " G"));
            sb.AppendLine(Row("ダウンフォース係数", baseStats.downforceFactor, stats.downforceFactor, "0.00", " G@300"));
            sb.AppendLine(Row("車重", baseStats.weightKg, stats.weightKg, "0", " kg", lowerIsBetter: true));
            sb.AppendLine(Row("信頼性", baseStats.reliability * 100f, stats.reliability * 100f, "0", " %"));
            sb.AppendLine();

            float g150 = stats.MaxCorneringG(150f / 3.6f);
            float g300 = stats.MaxCorneringG(300f / 3.6f);
            sb.AppendLine("<b>━━ 旋回限界G ━━</b>");
            sb.AppendLine($"  150 km/h: {g150:0.0} G     300 km/h: {g300:0.0} G");
            sb.AppendLine($"  <color=#aaaaaa>(人間の持続限界: {MachineStats.HumanSustainedGLimit:0.0} G)</color>");
            sb.AppendLine();

            // ---- 理論ラップ比較: 設計画面の中で最も重要な2行 ----
            float humanLap = LapTimeEstimator.Estimate(ctx.Track, stats, MachineStats.HumanSustainedGLimit);
            float machineLap = LapTimeEstimator.Estimate(ctx.Track, stats, 1000f);
            sb.AppendLine($"<b>━━ 理論ラップ ({ctx.Track.trackName}) ━━</b>");
            sb.AppendLine($"  人間ドライバー (HUMAN GP):  <b>{LapTimeEstimator.Format(humanLap)}</b>");
            sb.AppendLine($"  AIドライバー (MACHINE GP):  <b>{LapTimeEstimator.Format(machineLap)}</b>");
            float gap = humanLap - machineLap;
            if (gap > 0.2f)
                sb.AppendLine($"  <color=#ffd24d>このマシンの速さのうち {gap:0.0} 秒/周 は、人間には扱えない領域にある。</color>");
            sb.AppendLine();

            // ---- 警告 ----
            if (g300 > MachineStats.HumanSustainedGLimit)
            {
                sb.AppendLine("<color=#ff6655><b>⚠ 警告:</b> 高速コーナーで人間の持続G限界を超えます。</color>");
                sb.AppendLine("<color=#ff6655>HUMAN GP ではブラックアウト (失神) の危険があります。</color>");
            }
            else
            {
                sb.AppendLine("<color=#7be07b>このマシンは人間の限界の内側に収まっています。…今のところは。</color>");
            }

            statsText.text = sb.ToString();
        }

        static string Row(string label, float before, float after, string fmt, string unit, bool lowerIsBetter = false)
        {
            float delta = after - before;
            string deltaStr = Mathf.Abs(delta) < 0.005f
                ? ""
                : $"  <color={(delta > 0 != lowerIsBetter ? "#7be07b" : "#e07b7b")}>({(delta > 0 ? "+" : "")}{delta.ToString(fmt)})</color>";
            return $"  {label}: {before.ToString(fmt)}{unit} → <b>{after.ToString(fmt)}{unit}</b>{deltaStr}";
        }
    }
}
