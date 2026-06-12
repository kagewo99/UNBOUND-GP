using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.UI
{
    /// <summary>タイトル画面。コンセプトの提示とガレージへの導線のみを持つ。</summary>
    public class MainMenuScreen : MonoBehaviour
    {
        void Start()
        {
            var ctx = GameContext.I; // コンテキストを起動しておく

            var canvas = UiFactory.CreateCanvas("MainMenuCanvas");

            var bg = UiFactory.CreatePanel(canvas.transform, new Color(0.03f, 0.04f, 0.08f), "BG");
            UiFactory.SetAnchors(bg, Vector2.zero, Vector2.one);

            var title = UiFactory.CreateText(bg, "UNBOUND GP", 110, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f), true);
            UiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0f, 0.68f), new Vector2(1f, 0.92f));

            var sub = UiFactory.CreateText(bg,
                "── もし、F1にマシンのレギュレーションが存在しなかったら?",
                30, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchors((RectTransform)sub.transform, new Vector2(0f, 0.58f), new Vector2(1f, 0.68f));

            var concept = UiFactory.CreateText(bg,
                "禁止された技術をすべて解放し、理論上最速のマシンを設計せよ。\n" +
                "ただし思い出してほしい。コクピットに座るのが人間である限り、\n" +
                "このレースにはたったひとつだけ、書き換えられないルールが残っている。",
                22, TextAnchor.MiddleCenter, new Color(0.8f, 0.82f, 0.9f));
            UiFactory.SetAnchors((RectTransform)concept.transform, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.56f));

            var start = UiFactory.CreateButton(bg, "ガレージへ (研究 / 設計 / 出走)", () => SceneFlow.Load(SceneFlow.Garage),
                new Color(0.75f, 0.2f, 0.15f), 26);
            UiFactory.SetAnchors((RectTransform)start.transform, new Vector2(0.34f, 0.24f), new Vector2(0.66f, 0.33f));

            var reset = UiFactory.CreateButton(bg, "進行データをリセット", () =>
            {
                ctx.ResetSave();
                Debug.Log("セーブデータを初期化しました。");
            }, new Color(0.25f, 0.27f, 0.33f), 18);
            UiFactory.SetAnchors((RectTransform)reset.transform, new Vector2(0.41f, 0.13f), new Vector2(0.59f, 0.19f));

            var quit = UiFactory.CreateButton(bg, "終了", () => Application.Quit(), new Color(0.2f, 0.2f, 0.24f), 18);
            UiFactory.SetAnchors((RectTransform)quit.transform, new Vector2(0.45f, 0.05f), new Vector2(0.55f, 0.11f));

            var version = UiFactory.CreateText(bg, "Vertical Slice Prototype", 16, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.4f));
            UiFactory.SetAnchors((RectTransform)version.transform, new Vector2(0.7f, 0f), new Vector2(1f, 0.05f), Vector2.zero, new Vector2(-12f, 0f));
        }
    }
}
