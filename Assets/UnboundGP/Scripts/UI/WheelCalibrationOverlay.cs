using UnboundGP.Race;
using UnboundGP.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnboundGP.UI
{
    /// <summary>
    /// レーシングホイールのゲーム内キャリブレーション画面。
    ///
    /// 10本のジョイスティック軸(Joy_Axis_1..10)の値をリアルタイム表示し、
    /// 「ハンドルを回す/ペダルを踏む」操作で最も動いた軸を自動検出して
    /// ステア・アクセル・ブレーキへ割り当てる。デバイス固有の軸配置を知らなくても合わせられる。
    ///
    /// 開いている間は Time.timeScale=0 で車を止める。結果は WheelMapping(PlayerPrefs)へ保存し、
    /// 走行中のプレイヤー入力へ即時反映する。
    /// </summary>
    public class WheelCalibrationOverlay : MonoBehaviour
    {
        enum Capture { None, Steer, Accel, Brake }

        WheelMapping map;
        PlayerInputDriver player;        // 走行中なら反映先(ガレージからは null)
        System.Action onClose;

        Image[] axisBars = new Image[WheelMapping.EditorAxisCount];
        Text[] axisLabels = new Text[WheelMapping.EditorAxisCount];
        Text steerLabel, accelLabel, brakeLabel, statusText;

        Capture capturing = Capture.None;
        float captureTimer;
        float[] capMin = new float[WheelMapping.EditorAxisCount];
        float[] capMax = new float[WheelMapping.EditorAxisCount];
        float[] capStart = new float[WheelMapping.EditorAxisCount];
        const float CaptureSeconds = 3f;

        public static WheelCalibrationOverlay Open(PlayerInputDriver player, System.Action onClose = null)
        {
            var canvas = UiFactory.CreateCanvas("WheelCalibration");
            canvas.sortingOrder = 200;
            var ov = canvas.gameObject.AddComponent<WheelCalibrationOverlay>();
            ov.player = player;
            ov.onClose = onClose;
            ov.map = WheelMapping.Load();
            ov.Build(canvas.transform);
            Time.timeScale = 0f;
            return ov;
        }

        void Build(Transform root)
        {
            var bg = UiFactory.CreatePanel(root, new Color(0.03f, 0.04f, 0.07f, 0.97f), "BG");
            UiFactory.SetAnchors(bg, Vector2.zero, Vector2.one);

            var title = UiFactory.CreateText(bg, "ハンドル設定 ── HORI レーシングホイール APEX 等", 30, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f), true);
            UiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0f, 0.92f), new Vector2(1f, 1f), new Vector2(36f, 0f), new Vector2(-36f, -8f));

            var help = UiFactory.CreateText(bg,
                "下のバーは接続中のコントローラの各軸です。各操作の[登録]を押し、3秒以内に\n" +
                "ハンドルを左右いっぱいに回す/ペダルを踏み切ると、その軸を自動で割り当てます。",
                18, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.95f));
            UiFactory.SetAnchors((RectTransform)help.transform, new Vector2(0f, 0.84f), new Vector2(1f, 0.92f), new Vector2(36f, 0f), new Vector2(-36f, 0f));

            // ---- 軸モニタ(左半分) ----
            var monitor = UiFactory.CreatePanel(bg, new Color(0f, 0f, 0f, 0.3f), "Monitor");
            UiFactory.SetAnchors(monitor, new Vector2(0.04f, 0.12f), new Vector2(0.5f, 0.83f));
            for (int i = 0; i < WheelMapping.EditorAxisCount; i++)
            {
                float t1 = 1f - (i + 1) / (float)WheelMapping.EditorAxisCount;
                float t0 = 1f - i / (float)WheelMapping.EditorAxisCount;
                var label = UiFactory.CreateText(monitor, $"軸{i + 1}", 16, TextAnchor.MiddleLeft, Color.white);
                UiFactory.SetAnchors((RectTransform)label.transform, new Vector2(0.01f, t1), new Vector2(0.16f, t0), new Vector2(8f, 1f), new Vector2(0f, -1f));
                axisLabels[i] = label;
                var bar = UiFactory.CreateBar(monitor, new Color(0.3f, 0.7f, 1f), $"AxisBar{i}");
                UiFactory.SetAnchors((RectTransform)bar.transform.parent, new Vector2(0.17f, t1), new Vector2(0.99f, t0), new Vector2(0f, 2f), new Vector2(-6f, -2f));
                axisBars[i] = bar;
            }

            // ---- 割り当て(右半分) ----
            statusText = UiFactory.CreateText(bg, "", 18, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.4f), true);
            UiFactory.SetAnchors((RectTransform)statusText.transform, new Vector2(0.53f, 0.74f), new Vector2(0.97f, 0.82f));

            BuildAssignRow(bg, 0.6f, "ステアリング", () => steerLabel, l => steerLabel = l, Capture.Steer, "(左右いっぱいに回す)");
            BuildAssignRow(bg, 0.46f, "アクセル", () => accelLabel, l => accelLabel = l, Capture.Accel, "(踏み切る)");
            BuildAssignRow(bg, 0.32f, "ブレーキ", () => brakeLabel, l => brakeLabel = l, Capture.Brake, "(踏み切る)");

            // ステア反転トグル
            var invBtn = UiFactory.CreateButton(bg, "ステア左右反転", () =>
            {
                map.steerInvert = !map.steerInvert;
                RefreshLabels();
            }, new Color(0.3f, 0.35f, 0.5f), 18);
            UiFactory.SetAnchors((RectTransform)invBtn.transform, new Vector2(0.53f, 0.20f), new Vector2(0.73f, 0.27f));

            // 保存して閉じる / キーボードのみ
            var saveBtn = UiFactory.CreateButton(bg, "保存して閉じる", Close, new Color(0.2f, 0.55f, 0.35f), 22);
            UiFactory.SetAnchors((RectTransform)saveBtn.transform, new Vector2(0.53f, 0.04f), new Vector2(0.73f, 0.13f));

            var clearBtn = UiFactory.CreateButton(bg, "割り当て解除(キーボードのみ)", () =>
            {
                map = new WheelMapping();
                RefreshLabels();
            }, new Color(0.4f, 0.25f, 0.22f), 18);
            UiFactory.SetAnchors((RectTransform)clearBtn.transform, new Vector2(0.76f, 0.04f), new Vector2(0.97f, 0.13f));

            RefreshLabels();
            SetStatus("F1 でいつでも開閉できます。");
        }

        void BuildAssignRow(Transform parent, float yCenter, string name,
            System.Func<Text> getLabel, System.Action<Text> setLabel, Capture cap, string hint)
        {
            var row = UiFactory.CreatePanel(parent, new Color(0.1f, 0.12f, 0.18f, 0.95f), "Row_" + name);
            UiFactory.SetAnchors(row, new Vector2(0.53f, yCenter - 0.055f), new Vector2(0.97f, yCenter + 0.055f));

            var label = UiFactory.CreateText(row, "", 20, TextAnchor.MiddleLeft, Color.white);
            UiFactory.SetAnchors((RectTransform)label.transform, new Vector2(0f, 0f), new Vector2(0.62f, 1f), new Vector2(18f, 0f), Vector2.zero);
            setLabel(label);

            var btn = UiFactory.CreateButton(row, "登録 " + hint, () => StartCapture(cap), new Color(0.6f, 0.45f, 0.15f), 17);
            UiFactory.SetAnchors((RectTransform)btn.transform, new Vector2(0.63f, 0.15f), new Vector2(0.98f, 0.85f));
        }

        void StartCapture(Capture cap)
        {
            capturing = cap;
            captureTimer = CaptureSeconds;
            for (int i = 0; i < WheelMapping.EditorAxisCount; i++)
            {
                float raw = WheelMapping.ReadRawAxis(i + 1);
                capStart[i] = raw;
                capMin[i] = raw;
                capMax[i] = raw;
            }
            SetStatus($"記録中… {(cap == Capture.Steer ? "ハンドルを左右いっぱいに回して" : "ペダルを踏み切って")}ください ({CaptureSeconds:0}s)");
        }

        void Update()
        {
            // 軸モニタ更新(timeScale=0 でも Update は回る)
            for (int i = 0; i < WheelMapping.EditorAxisCount; i++)
            {
                float raw = WheelMapping.ReadRawAxis(i + 1);
                if (axisBars[i] != null) axisBars[i].fillAmount = (raw + 1f) * 0.5f;
                if (axisLabels[i] != null) axisLabels[i].text = $"軸{i + 1}\n{raw:+0.00;-0.00}";
            }

            if (capturing != Capture.None)
            {
                for (int i = 0; i < WheelMapping.EditorAxisCount; i++)
                {
                    float raw = WheelMapping.ReadRawAxis(i + 1);
                    if (raw < capMin[i]) capMin[i] = raw;
                    if (raw > capMax[i]) capMax[i] = raw;
                }
                captureTimer -= Time.unscaledDeltaTime;
                if (captureTimer <= 0f) FinishCapture();
            }
        }

        /// <summary>外部(RaceManager の F1 トグル等)から閉じる。</summary>
        public void CloseExternally() => Close();

        void FinishCapture()
        {
            // 最も大きく動いた軸を選ぶ
            int best = -1; float bestRange = 0.15f; // 最低限の動きがないと未検出
            for (int i = 0; i < WheelMapping.EditorAxisCount; i++)
            {
                float range = capMax[i] - capMin[i];
                if (range > bestRange) { bestRange = range; best = i; }
            }

            if (best < 0)
            {
                SetStatus("動きを検出できませんでした。もう一度しっかり動かしてください。");
                capturing = Capture.None;
                return;
            }

            int axis = best + 1;
            switch (capturing)
            {
                case Capture.Steer:
                    map.steerAxis = axis;
                    map.steerRange = Mathf.Max(Mathf.Abs(capMin[best]), Mathf.Abs(capMax[best]));
                    SetStatus($"ステアリングを 軸{axis} に割り当てました。");
                    break;
                case Capture.Accel:
                    map.accelAxis = axis;
                    // 開始値(離した状態)を rest、最も離れた値を full とする
                    map.accelRest = capStart[best];
                    map.accelFull = (Mathf.Abs(capMax[best] - capStart[best]) >= Mathf.Abs(capMin[best] - capStart[best])) ? capMax[best] : capMin[best];
                    SetStatus($"アクセルを 軸{axis} に割り当てました。");
                    break;
                case Capture.Brake:
                    map.brakeAxis = axis;
                    map.brakeRest = capStart[best];
                    map.brakeFull = (Mathf.Abs(capMax[best] - capStart[best]) >= Mathf.Abs(capMin[best] - capStart[best])) ? capMax[best] : capMin[best];
                    SetStatus($"ブレーキを 軸{axis} に割り当てました。");
                    break;
            }
            capturing = Capture.None;
            RefreshLabels();
        }

        void RefreshLabels()
        {
            if (steerLabel != null) steerLabel.text = $"ステアリング: {AxisDesc(map.steerAxis)}{(map.steerInvert ? "  [反転]" : "")}";
            if (accelLabel != null) accelLabel.text = $"アクセル: {AxisDesc(map.accelAxis)}";
            if (brakeLabel != null) brakeLabel.text = $"ブレーキ: {AxisDesc(map.brakeAxis)}";
        }

        static string AxisDesc(int axis) => axis <= 0 ? "<color=#888888>未割り当て</color>" : $"<color=#7be07b>軸{axis}</color>";

        void SetStatus(string s) { if (statusText != null) statusText.text = s; }

        void Close()
        {
            map.Save();
            if (player != null) player.SetWheelMapping(map);
            Time.timeScale = 1f;
            if (onClose != null) onClose();
            Destroy(gameObject);
        }
    }
}
