using UnboundGP.Core;
using UnboundGP.Data;
using UnboundGP.Design;
using UnityEngine;
using UnityEngine.UI;

namespace UnboundGP.UI
{
    /// <summary>
    /// 出走タブ。Human GP / Machine GP を“同じマシンに対する2つの問い”として並べる。
    /// どちらを選んでも走るのは同じ設計のマシン。違うのはコクピットの中身だけ。
    /// </summary>
    public class RaceEntryView : MonoBehaviour
    {
        GameContext ctx;
        Text humanCardText, machineCardText, trackText;

        public void Init(GameContext context)
        {
            ctx = context;

            // ---- コース情報 ----
            var trackPanel = UiFactory.CreatePanel(transform, new Color(0f, 0f, 0f, 0.35f), "TrackInfo");
            UiFactory.SetAnchors(trackPanel, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.97f));
            trackText = UiFactory.CreateText(trackPanel, "", 19, TextAnchor.UpperLeft);
            trackText.supportRichText = true;
            UiFactory.SetAnchors((RectTransform)trackText.transform, Vector2.zero, Vector2.one, new Vector2(20f, 10f), new Vector2(-20f, -10f));

            // ---- Human GP カード ----
            BuildCard(true, new Vector2(0.05f, 0.06f), new Vector2(0.48f, 0.74f), out humanCardText,
                "HUMAN GP に出走する", new Color(0.75f, 0.25f, 0.15f),
                () => StartRace(GameMode.HumanGP));

            // ---- Machine GP カード ----
            BuildCard(false, new Vector2(0.52f, 0.06f), new Vector2(0.95f, 0.74f), out machineCardText,
                "MACHINE GP を観戦する", new Color(0.2f, 0.4f, 0.75f),
                () => StartRace(GameMode.MachineGP));

            Rebuild();
        }

        void BuildCard(bool human, Vector2 min, Vector2 max, out Text bodyText,
            string buttonLabel, Color buttonColor, UnityEngine.Events.UnityAction onStart)
        {
            var card = UiFactory.CreatePanel(transform, new Color(0.12f, 0.13f, 0.19f, 0.97f), human ? "HumanCard" : "MachineCard");
            UiFactory.SetAnchors(card, min, max);

            bodyText = UiFactory.CreateText(card, "", 19, TextAnchor.UpperLeft);
            bodyText.supportRichText = true;
            bodyText.lineSpacing = 1.2f;
            UiFactory.SetAnchors((RectTransform)bodyText.transform, new Vector2(0f, 0.18f), Vector2.one, new Vector2(22f, 6f), new Vector2(-22f, -14f));

            var btn = UiFactory.CreateButton(card, buttonLabel, onStart, buttonColor, 24);
            UiFactory.SetAnchors((RectTransform)btn.transform, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.15f));
        }

        public void Rebuild()
        {
            if (trackText == null) return;

            var stats = ctx.CurrentMachineStats();
            float humanLap = LapTimeEstimator.Estimate(ctx.Track, stats, MachineStats.HumanSustainedGLimit);
            float machineLap = LapTimeEstimator.Estimate(ctx.Track, stats, 1000f);

            trackText.text =
                $"<b>{ctx.Track.trackName}</b>  ({ctx.Track.lapCount} LAPS)\n" +
                $"{ctx.Track.description}";

            var human = ctx.HumanGPMode;
            humanCardText.text =
                $"<b><size=30>{human.title}</size></b>\n" +
                $"<color=#ffd24d>{human.tagline}</color>\n\n" +
                $"{human.description}\n\n" +
                $"<b>あなたのマシンの理論ラップ: {LapTimeEstimator.Format(humanLap)}</b>\n" +
                $"<color=#aaaaaa>操作: WASD/矢印 + Space   失神したら…マシンは止まってくれない。</color>";

            var machine = ctx.MachineGPMode;
            machineCardText.text =
                $"<b><size=30>{machine.title}</size></b>\n" +
                $"<color=#9ecbff>{machine.tagline}</color>\n\n" +
                $"{machine.description}\n\n" +
                $"<b>あなたのマシンの理論ラップ: {LapTimeEstimator.Format(machineLap)}</b>\n" +
                $"<color=#aaaaaa>同じマシンで人間より {Mathf.Max(humanLap - machineLap, 0f):0.0} 秒/周 速い。その理由を見に行こう。</color>";
        }

        void StartRace(GameMode mode)
        {
            ctx.SelectedMode = mode;
            SceneFlow.Load(SceneFlow.Race);
        }
    }
}
