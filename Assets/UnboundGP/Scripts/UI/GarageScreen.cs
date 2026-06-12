using UnboundGP.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UnboundGP.UI
{
    /// <summary>
    /// ガレージ = プロトタイプのハブ画面。
    /// [研究ツリー] [マシン設計] [出走] の3タブを持ち、
    /// 研究 → 設計 → 走る → 気づく → また研究、のコアループを束ねる。
    /// </summary>
    public class GarageScreen : MonoBehaviour
    {
        GameContext ctx;
        Text headerPointsText;

        ResearchTreeView researchView;
        MachineDesignView designView;
        RaceEntryView raceView;

        RectTransform researchPanel, designPanel, racePanel;

        void Start()
        {
            ctx = GameContext.I;
            ctx.StateChanged += OnStateChanged;

            var canvas = UiFactory.CreateCanvas("GarageCanvas");
            var bg = UiFactory.CreatePanel(canvas.transform, new Color(0.05f, 0.06f, 0.1f), "BG");
            UiFactory.SetAnchors(bg, Vector2.zero, Vector2.one);

            // ---- ヘッダー ----
            var header = UiFactory.CreatePanel(bg, new Color(0.1f, 0.11f, 0.16f), "Header");
            UiFactory.SetAnchors(header, new Vector2(0f, 0.91f), Vector2.one);

            var title = UiFactory.CreateText(header, "GARAGE ── UNBOUND GP", 32, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f), true);
            UiFactory.SetAnchors((RectTransform)title.transform, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(24f, 0f), Vector2.zero);

            headerPointsText = UiFactory.CreateText(header, "", 26, TextAnchor.MiddleRight, Color.white, true);
            UiFactory.SetAnchors((RectTransform)headerPointsText.transform, new Vector2(0.5f, 0f), Vector2.one, Vector2.zero, new Vector2(-24f, 0f));

            // ---- タブ ----
            var tabBar = UiFactory.CreatePanel(bg, new Color(0.08f, 0.09f, 0.13f), "TabBar");
            UiFactory.SetAnchors(tabBar, new Vector2(0f, 0.84f), new Vector2(1f, 0.91f));

            var tabResearch = UiFactory.CreateButton(tabBar, "研究ツリー", () => ShowTab(0), new Color(0.3f, 0.35f, 0.5f));
            UiFactory.SetAnchors((RectTransform)tabResearch.transform, new Vector2(0.02f, 0.1f), new Vector2(0.26f, 0.9f));
            var tabDesign = UiFactory.CreateButton(tabBar, "マシン設計", () => ShowTab(1), new Color(0.3f, 0.35f, 0.5f));
            UiFactory.SetAnchors((RectTransform)tabDesign.transform, new Vector2(0.28f, 0.1f), new Vector2(0.52f, 0.9f));
            var tabRace = UiFactory.CreateButton(tabBar, "出走 ── HUMAN GP / MACHINE GP", () => ShowTab(2), new Color(0.6f, 0.25f, 0.2f));
            UiFactory.SetAnchors((RectTransform)tabRace.transform, new Vector2(0.54f, 0.1f), new Vector2(0.86f, 0.9f));

            var back = UiFactory.CreateButton(tabBar, "タイトルへ", () => SceneFlow.Load(SceneFlow.MainMenu), new Color(0.2f, 0.2f, 0.24f), 18);
            UiFactory.SetAnchors((RectTransform)back.transform, new Vector2(0.88f, 0.1f), new Vector2(0.98f, 0.9f));

            // ---- コンテンツ ----
            researchPanel = UiFactory.CreatePanel(bg, Color.clear, "ResearchPanel");
            UiFactory.SetAnchors(researchPanel, Vector2.zero, new Vector2(1f, 0.84f));
            researchView = researchPanel.gameObject.AddComponent<ResearchTreeView>();
            researchView.Init(ctx);

            designPanel = UiFactory.CreatePanel(bg, Color.clear, "DesignPanel");
            UiFactory.SetAnchors(designPanel, Vector2.zero, new Vector2(1f, 0.84f));
            designView = designPanel.gameObject.AddComponent<MachineDesignView>();
            designView.Init(ctx);

            racePanel = UiFactory.CreatePanel(bg, Color.clear, "RacePanel");
            UiFactory.SetAnchors(racePanel, Vector2.zero, new Vector2(1f, 0.84f));
            raceView = racePanel.gameObject.AddComponent<RaceEntryView>();
            raceView.Init(ctx);

            RefreshHeader();
            ShowTab(0);
        }

        void OnDestroy()
        {
            if (ctx != null) ctx.StateChanged -= OnStateChanged;
        }

        void OnStateChanged()
        {
            RefreshHeader();
            researchView.Rebuild();
            designView.Rebuild();
            raceView.Rebuild();
        }

        void RefreshHeader()
        {
            headerPointsText.text =
                $"研究ポイント: {ctx.Save.researchPoints} RP    完走: {ctx.Save.racesCompleted} レース";
        }

        void ShowTab(int index)
        {
            researchPanel.gameObject.SetActive(index == 0);
            designPanel.gameObject.SetActive(index == 1);
            racePanel.gameObject.SetActive(index == 2);
        }
    }
}
