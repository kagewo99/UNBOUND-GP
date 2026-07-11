using System.Text;
using UnboundGP.Core;
using UnboundGP.Design;
using UnboundGP.Race;
using UnityEngine;

namespace UnboundGP.UI
{
    /// <summary>
    /// レース後のデブリーフ画面。本作の最重要画面。
    ///
    /// 順位やタイムではなく「あなたの身体に何が起きたか」「同じマシンをAIに渡すと何が起きるか」
    /// を突きつけ、プレイヤー自身に“レギュレーションの意味”を発見させる。
    /// 説教はせず、データと短い問いだけを置く。
    /// </summary>
    public static class DebriefView
    {
        public static void Show(RaceResult result)
        {
            var canvas = UiFactory.CreateCanvas("DebriefCanvas");
            canvas.sortingOrder = 100;

            var bg = UiFactory.CreatePanel(canvas.transform, new Color(0.02f, 0.02f, 0.05f, 0.93f), "DebriefBG");
            UiFactory.SetAnchors(bg, Vector2.zero, Vector2.one);

            var panel = UiFactory.CreatePanel(bg, new Color(0.08f, 0.09f, 0.13f, 1f), "DebriefPanel");
            UiFactory.SetAnchors(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.sizeDelta = new Vector2(1240f, 880f);

            // ---- ヘッダー ----
            string header;
            switch (result.mode)
            {
                case GameMode.MachineGP:
                    header = "DEBRIEF ── 制約が消えた世界のレースは、静かだった";
                    break;
                case GameMode.TimeAttack:
                    header = "DEBRIEF ── コースと、自分の限界とだけ向き合った";
                    break;
                default:
                    header = "DEBRIEF ── 人間の身体が、最後のレギュレーションだった";
                    break;
            }
            var title = UiFactory.CreateText(panel, header, 34, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.4f), true);
            UiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0f, 0.9f), new Vector2(1f, 1f), new Vector2(40f, 0f), new Vector2(-40f, -10f));

            // ---- 本文 ----
            var body = UiFactory.CreateText(panel, BuildBody(result), 22, TextAnchor.UpperLeft, Color.white);
            body.lineSpacing = 1.25f;
            UiFactory.SetAnchors((RectTransform)body.transform, new Vector2(0f, 0.14f), new Vector2(1f, 0.9f), new Vector2(40f, 10f), new Vector2(-40f, 0f));

            // ---- ボタン ----
            var retry = UiFactory.CreateButton(panel, "もう一度走る", () => SceneFlow.Reload(), new Color(0.25f, 0.3f, 0.4f));
            UiFactory.SetAnchors((RectTransform)retry.transform, new Vector2(0.08f, 0.03f), new Vector2(0.45f, 0.11f));

            var garage = UiFactory.CreateButton(panel, "ガレージへ戻る (研究を進める)", () => SceneFlow.Load(SceneFlow.Garage), new Color(0.2f, 0.55f, 0.35f));
            UiFactory.SetAnchors((RectTransform)garage.transform, new Vector2(0.55f, 0.03f), new Vector2(0.92f, 0.11f));
        }

        static string BuildBody(RaceResult r)
        {
            var sb = new StringBuilder();

            // ---- リザルト ----
            if (r.mode == GameMode.TimeAttack)
            {
                int laps = r.standingLines != null ? r.standingLines.Length : 0;
                sb.AppendLine($"<b>RESULT</b>   周回数 {laps}   ベストラップ {LapTimeEstimator.Format(r.playerBestLap)}   獲得 {r.pointsAwarded} RP");
            }
            else
            {
                sb.AppendLine($"<b>RESULT</b>   あなたのマシン: {Ordinal(r.playerPlace)}位   ベストラップ {LapTimeEstimator.Format(r.playerBestLap)}   獲得 {r.pointsAwarded} RP");
            }
            if (r.standingLines != null)
            {
                // タイムアタックのラップ履歴が長い場合は直近8周に絞る
                int start = r.mode == GameMode.TimeAttack ? Mathf.Max(0, r.standingLines.Length - 8) : 0;
                for (int i = start; i < r.standingLines.Length; i++) sb.AppendLine("   " + r.standingLines[i]);
            }
            sb.AppendLine();

            // ---- 生理データ ----
            sb.AppendLine("<b>TELEMETRY ── コクピットの中で起きていたこと</b>");
            sb.AppendLine($"   最大横G: {r.peakG:0.0} G  (人間の持続限界: {MachineStats.HumanSustainedGLimit:0} G)");
            sb.AppendLine($"   人間限界を超えていた時間: {r.timeOverHumanLimit:0.0} 秒");
            if (r.mode != GameMode.MachineGP)
                sb.AppendLine($"   ブラックアウト: {r.blackoutCount} 回");
            else
                sb.AppendLine("   ブラックアウト: 0 回 ── AIに失神はない。恐怖も、ためらいも。");
            sb.AppendLine();

            // ---- 同一マシンの理論比較 ----
            sb.AppendLine("<b>同じマシン、違うドライバー</b>");
            sb.AppendLine($"   人間が乗った場合の理論ラップ:  {LapTimeEstimator.Format(r.estHumanLap)}");
            sb.AppendLine($"   AIが乗った場合の理論ラップ:    {LapTimeEstimator.Format(r.estMachineLap)}");
            float gap = r.estHumanLap - r.estMachineLap;
            if (gap > 0.5f)
                sb.AppendLine($"   その差 {gap:0.0} 秒。この差はドライバーの腕ではなく、<b>血液が脳に届くかどうか</b>の差である。");
            sb.AppendLine();

            // ---- 動的な気づき ----
            sb.AppendLine("<b>INSIGHT</b>");
            if (r.mode == GameMode.TimeAttack)
            {
                float toTheory = r.playerBestLap > 0f ? r.playerBestLap - r.estHumanLap : -1f;
                if (r.playerBestLap <= 0f)
                    sb.AppendLine("   計測ラップが残らなかった。まずは1周、コースと対話することから。");
                else if (toTheory <= 1.5f)
                    sb.AppendLine($"   人間理論値との差 {Mathf.Max(toTheory, 0f):0.0} 秒。あなたはこのマシンの“人間に許された速さ”を\n   ほぼ使い切っている。ここから先は、肉体の設計図の外側だ。");
                else
                    sb.AppendLine($"   人間理論値まであと {toTheory:0.0} 秒。マシンでもAIでもなく、\n   まだあなた自身の中に縮められる余地がある。");
            }
            else if (r.mode == GameMode.HumanGP)
            {
                if (r.blackoutCount > 0)
                    sb.AppendLine("   マシンはまだ余力を残していた。先に限界が来たのはあなたの身体だった。\n   規制のない世界では、勝つための設計が、人間を壊す設計になっていく。");
                else if (gap > 3f)
                    sb.AppendLine("   あなたは意識を保って走り切った。だがこのマシンの本当の速さは、\n   人間がコクピットに居る限り、永遠に引き出せない。");
                else
                    sb.AppendLine("   いまはまだ、人間とマシンの限界は釣り合っている。\n   研究ツリーを進めたとき、その均衡がどう崩れるかを見届けてほしい。");
            }
            else
            {
                sb.AppendLine("   ミスがなければ逆転はなく、恐怖がなければ勇気もない。\n   性能差がそのまま着順になった。レースは出走前に終わっていた。");
            }
            sb.AppendLine();

            // ---- モード定義側の核心テキスト ----
            var ctx = GameContext.I;
            var modeData = r.mode == GameMode.MachineGP ? ctx.MachineGPMode
                : r.mode == GameMode.TimeAttack ? ctx.TimeAttackMode
                : ctx.HumanGPMode;
            sb.AppendLine($"<i>{modeData.debriefInsight}</i>");

            return sb.ToString();
        }

        static string Ordinal(int place) => place <= 0 ? "-" : place.ToString();
    }
}
