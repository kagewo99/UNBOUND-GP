using UnboundGP.Core;
using UnboundGP.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnboundGP.Race
{
    /// <summary>
    /// レース中の HUD。速度・ラップ・順位に加えて、本作固有の表示として
    /// ・Gメーター + 「人間限界 5G」の基準線的表示
    /// ・意識ゲージとブラックアウト時の視野狭窄ビネット
    /// を持つ。Machine GP では「現在G = 人間限界の何%か」を常時表示し、
    /// 観戦しながら“人間には不可能な走り”であることを意識させる。
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        Text modeText, lapText, timeText, speedText, gText, humanRatioText;
        Text countdownText, standingsText, hintText, blackoutText;
        Text gearText, rpmText, shiftModeText;
        Image gBar, consciousnessBar, vignette, rpmBar;

        public static RaceHUD Create()
        {
            var canvas = UiFactory.CreateCanvas("RaceHUD");
            var hud = canvas.gameObject.AddComponent<RaceHUD>();
            hud.Build(canvas.transform);
            return hud;
        }

        void Build(Transform root)
        {
            // ---- 視野狭窄ビネット (最背面に置き、他HUDより先に生成) ----
            var vinRt = UiFactory.CreatePanel(root, Color.clear, "Vignette");
            UiFactory.SetAnchors(vinRt, Vector2.zero, Vector2.one, new Vector2(-200f, -200f), new Vector2(200f, 200f));
            vignette = vinRt.GetComponent<Image>();
            vignette.sprite = UiFactory.VignetteSprite();
            vignette.color = new Color(1f, 1f, 1f, 0f);
            vignette.raycastTarget = false;

            // ---- 左上: モード ----
            var modeRt = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0.55f), "ModePanel");
            UiFactory.SetAnchors(modeRt, new Vector2(0f, 1f), new Vector2(0f, 1f));
            modeRt.pivot = new Vector2(0f, 1f);
            modeRt.anchoredPosition = new Vector2(16f, -16f);
            modeRt.sizeDelta = new Vector2(340f, 64f);
            modeText = UiFactory.CreateText(modeRt, "", 26, TextAnchor.MiddleCenter, Color.white, true);
            UiFactory.SetAnchors((RectTransform)modeText.transform, Vector2.zero, Vector2.one);

            // ---- 上中央: ラップ / タイム ----
            var lapRt = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0.55f), "LapPanel");
            UiFactory.SetAnchors(lapRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            lapRt.pivot = new Vector2(0.5f, 1f);
            lapRt.anchoredPosition = new Vector2(0f, -16f);
            lapRt.sizeDelta = new Vector2(420f, 64f);
            lapText = UiFactory.CreateText(lapRt, "LAP -/-", 24, TextAnchor.MiddleLeft, Color.white, true);
            UiFactory.SetAnchors((RectTransform)lapText.transform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), Vector2.zero);
            timeText = UiFactory.CreateText(lapRt, "0:00.000", 24, TextAnchor.MiddleRight, Color.white);
            UiFactory.SetAnchors((RectTransform)timeText.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-18f, 0f));

            // ---- 左: 順位表 ----
            var standRt = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0.45f), "Standings");
            UiFactory.SetAnchors(standRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            standRt.pivot = new Vector2(0f, 0.5f);
            standRt.anchoredPosition = new Vector2(16f, 0f);
            standRt.sizeDelta = new Vector2(360f, 200f);
            standingsText = UiFactory.CreateText(standRt, "", 20, TextAnchor.UpperLeft, Color.white);
            UiFactory.SetAnchors((RectTransform)standingsText.transform, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-8f, -8f));

            // ---- 右下: 速度 & Gメーター & 意識 ----
            var telRt = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0.55f), "Telemetry");
            UiFactory.SetAnchors(telRt, new Vector2(1f, 0f), new Vector2(1f, 0f));
            telRt.pivot = new Vector2(1f, 0f);
            telRt.anchoredPosition = new Vector2(-16f, 16f);
            telRt.sizeDelta = new Vector2(420f, 190f);

            speedText = UiFactory.CreateText(telRt, "0 km/h", 40, TextAnchor.MiddleRight, Color.white, true);
            UiFactory.SetAnchors((RectTransform)speedText.transform, new Vector2(0f, 0.66f), Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, -4f));

            gText = UiFactory.CreateText(telRt, "0.0 G", 24, TextAnchor.MiddleLeft, Color.white);
            UiFactory.SetAnchors((RectTransform)gText.transform, new Vector2(0f, 0.42f), new Vector2(0.35f, 0.66f), new Vector2(14f, 0f), Vector2.zero);

            gBar = UiFactory.CreateBar(telRt, new Color(0.95f, 0.65f, 0.1f), "GBar");
            UiFactory.SetAnchors((RectTransform)gBar.transform.parent, new Vector2(0.35f, 0.46f), new Vector2(1f, 0.62f), Vector2.zero, new Vector2(-14f, 0f));

            humanRatioText = UiFactory.CreateText(telRt, "", 18, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.7f));
            UiFactory.SetAnchors((RectTransform)humanRatioText.transform, new Vector2(0f, 0.24f), new Vector2(1f, 0.42f), new Vector2(14f, 0f), new Vector2(-14f, 0f));

            var consLabel = UiFactory.CreateText(telRt, "意識", 18, TextAnchor.MiddleLeft, Color.white);
            UiFactory.SetAnchors((RectTransform)consLabel.transform, new Vector2(0f, 0.04f), new Vector2(0.2f, 0.24f), new Vector2(14f, 0f), Vector2.zero);
            consciousnessBar = UiFactory.CreateBar(telRt, new Color(0.3f, 0.85f, 0.4f), "ConsciousnessBar");
            UiFactory.SetAnchors((RectTransform)consciousnessBar.transform.parent, new Vector2(0.2f, 0.06f), new Vector2(1f, 0.2f), Vector2.zero, new Vector2(-14f, 0f));

            // ---- 右下(テレメトリの左隣): ギア & タコメータ ----
            var gearRt = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0.55f), "GearRpm");
            UiFactory.SetAnchors(gearRt, new Vector2(1f, 0f), new Vector2(1f, 0f));
            gearRt.pivot = new Vector2(1f, 0f);
            gearRt.anchoredPosition = new Vector2(-448f, 16f);
            gearRt.sizeDelta = new Vector2(220f, 190f);

            var gearLabel = UiFactory.CreateText(gearRt, "GEAR", 16, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.6f));
            UiFactory.SetAnchors((RectTransform)gearLabel.transform, new Vector2(0f, 0.82f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);
            gearText = UiFactory.CreateText(gearRt, "1", 64, TextAnchor.MiddleCenter, Color.white, true);
            UiFactory.SetAnchors((RectTransform)gearText.transform, new Vector2(0f, 0.34f), new Vector2(1f, 0.85f));
            shiftModeText = UiFactory.CreateText(gearRt, "AUTO", 14, TextAnchor.MiddleCenter, new Color(0.6f, 0.8f, 1f));
            UiFactory.SetAnchors((RectTransform)shiftModeText.transform, new Vector2(0f, 0.28f), new Vector2(1f, 0.4f));
            rpmText = UiFactory.CreateText(gearRt, "0 RPM", 16, TextAnchor.MiddleRight, Color.white);
            UiFactory.SetAnchors((RectTransform)rpmText.transform, new Vector2(0f, 0.16f), new Vector2(1f, 0.28f), new Vector2(8f, 0f), new Vector2(-12f, 0f));
            rpmBar = UiFactory.CreateBar(gearRt, new Color(0.3f, 0.85f, 0.4f), "RpmBar");
            UiFactory.SetAnchors((RectTransform)rpmBar.transform.parent, new Vector2(0f, 0.04f), new Vector2(1f, 0.14f), new Vector2(10f, 0f), new Vector2(-10f, 0f));

            // ---- 中央: カウントダウン / BLACKOUT ----
            countdownText = UiFactory.CreateText(root, "", 110, TextAnchor.MiddleCenter, Color.white, true);
            UiFactory.SetAnchors((RectTransform)countdownText.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ((RectTransform)countdownText.transform).sizeDelta = new Vector2(900f, 200f);

            blackoutText = UiFactory.CreateText(root, "", 64, TextAnchor.MiddleCenter, new Color(0.9f, 0.15f, 0.1f), true);
            UiFactory.SetAnchors((RectTransform)blackoutText.transform, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f));
            ((RectTransform)blackoutText.transform).sizeDelta = new Vector2(900f, 110f);

            // ---- 下中央: 操作ヒント ----
            hintText = UiFactory.CreateText(root, "", 18, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));
            UiFactory.SetAnchors((RectTransform)hintText.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var hintRt = (RectTransform)hintText.transform;
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 10f);
            hintRt.sizeDelta = new Vector2(1200f, 30f);
        }

        public void SetMode(string title) => modeText.text = title;
        public void SetHint(string hint) => hintText.text = hint;
        public void SetCountdown(string s) => countdownText.text = s;
        public void SetStandings(string s) => standingsText.text = s;

        /// <summary>ギア・回転数(タコメータ)を更新する。</summary>
        public void SetGearRPM(string gearLabel, float rpm, float redline, float maxRpm, bool isCVT, bool autoShift, bool atLimiter)
        {
            gearText.text = gearLabel;
            if (isCVT)
            {
                gearText.fontSize = 40;
                gearText.color = Color.white;
                rpmText.text = "無段変速";
                shiftModeText.text = "CVT";
                rpmBar.fillAmount = Mathf.Clamp01(rpm / maxRpm);
                rpmBar.color = new Color(0.4f, 0.7f, 1f);
            }
            else
            {
                gearText.fontSize = 64;
                // リミッター時はギアを点滅赤に=「シフトアップしろ」のサイン
                gearText.color = atLimiter && (Time.unscaledTime % 0.2f < 0.1f)
                    ? new Color(1f, 0.25f, 0.2f) : Color.white;
                rpmText.text = $"{rpm:0} RPM";
                shiftModeText.text = autoShift ? "AUTO" : "MANUAL";
                rpmBar.fillAmount = Mathf.Clamp01(rpm / maxRpm);
                rpmBar.color = rpm >= redline
                    ? new Color(0.95f, 0.2f, 0.15f)
                    : Color.Lerp(new Color(0.3f, 0.85f, 0.4f), new Color(0.95f, 0.7f, 0.1f), Mathf.Clamp01((rpm / redline - 0.6f) / 0.4f));
            }
        }

        public void SetLap(string lap, string time)
        {
            lapText.text = lap;
            timeText.text = time;
        }

        /// <summary>注視中のマシンのテレメトリを更新する。</summary>
        public void SetTelemetry(float speedKmh, float g, float blackoutMeter, bool blackedOut, bool watchingHuman)
        {
            speedText.text = $"{speedKmh:0} km/h";
            gText.text = $"{g:0.0} G";
            gBar.fillAmount = Mathf.Clamp01(g / 12f);
            gBar.color = g > MachineStats.HumanSustainedGLimit
                ? new Color(0.95f, 0.2f, 0.15f)
                : new Color(0.95f, 0.65f, 0.1f);

            float ratio = g / MachineStats.HumanSustainedGLimit * 100f;
            humanRatioText.text = watchingHuman
                ? $"人間限界 {MachineStats.HumanSustainedGLimit:0}G に対し {ratio:0}%"
                : $"現在 {ratio:0}% ── 人間ならとうに意識がない";

            // 意識ゲージ (AI観戦時は常に満タン = “何も起こらない”ことが演出になる)
            consciousnessBar.fillAmount = 1f - blackoutMeter;
            consciousnessBar.color = blackoutMeter > 0.6f
                ? new Color(0.9f, 0.25f, 0.2f)
                : new Color(0.3f, 0.85f, 0.4f);

            // 視野狭窄
            vignette.color = new Color(1f, 1f, 1f, blackedOut ? 1f : Mathf.Pow(blackoutMeter, 1.3f));
            blackoutText.text = blackedOut ? "BLACKOUT" : "";
        }
    }
}
