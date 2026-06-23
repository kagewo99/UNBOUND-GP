using System;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// レーシングホイール(HORI APEX 等)の軸割り当てとキャリブレーション値。
    ///
    /// 旧 Input Manager に注入した汎用軸 "Joy_Axis_1".."Joy_Axis_10"(InputAxisInstaller)を、
    /// ステア・アクセル・ブレーキへどう対応づけるかを保持する。デバイスごとに軸配置が違うため、
    /// 値はゲーム内キャリブレーション(WheelCalibrationOverlay)で決め、PlayerPrefs に保存する。
    ///
    /// 一切ハードコードしないので、APEX に限らずどのホイール/ペダルでも合わせられる。
    /// </summary>
    [Serializable]
    public class WheelMapping
    {
        const string Key = "UNBOUND_GP_WHEEL_V1";

        // 割り当てる軸番号(1..10)。0 = 未割り当て。
        public int steerAxis = 0;
        public int accelAxis = 0;
        public int brakeAxis = 0;

        public bool steerInvert = false;
        public float steerDeadzone = 0.06f;
        public float steerRange = 1.0f;     // ステア軸の最大振れ(正規化用)

        // ペダルの素値の校正(離した状態 rest → 踏み切った状態 full)
        public float accelRest = -1f, accelFull = 1f;
        public float brakeRest = -1f, brakeFull = 1f;

        public bool IsConfigured => steerAxis > 0 || accelAxis > 0 || brakeAxis > 0;

        // ---- 永続化 ----
        public static WheelMapping Load()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                try { return JsonUtility.FromJson<WheelMapping>(PlayerPrefs.GetString(Key)) ?? new WheelMapping(); }
                catch { return new WheelMapping(); }
            }
            return new WheelMapping();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        // ---- 生軸の読み取り(未定義軸でも例外を出さない) ----
        public static float ReadRawAxis(int index1Based)
        {
            if (index1Based < 1 || index1Based > EditorAxisCount) return 0f;
            try { return Input.GetAxis(AxisName(index1Based)); }
            catch { return 0f; } // InputAxisInstaller 未実行など
        }

        public const int EditorAxisCount = 10;
        public static string AxisName(int index1Based) => "Joy_Axis_" + index1Based;

        // ---- 入力値(IDriverInput と同じ規約) ----
        /// <summary>ステア -1(左)〜+1(右)。未割り当てなら 0。</summary>
        public float ReadSteer()
        {
            if (steerAxis <= 0) return 0f;
            float raw = ReadRawAxis(steerAxis);
            if (steerInvert) raw = -raw;
            float v = Mathf.Clamp(raw / Mathf.Max(steerRange, 0.2f), -1f, 1f);
            // デッドゾーン後に再スケール(中央付近の遊びを除去)
            float dz = steerDeadzone;
            if (Mathf.Abs(v) < dz) return 0f;
            return Mathf.Sign(v) * (Mathf.Abs(v) - dz) / (1f - dz);
        }

        /// <summary>アクセル 0〜1。未割り当てなら 0。</summary>
        public float ReadAccel() => ReadPedal(accelAxis, accelRest, accelFull);

        /// <summary>ブレーキ 0〜1。未割り当てなら 0。</summary>
        public float ReadBrake() => ReadPedal(brakeAxis, brakeRest, brakeFull);

        const float PedalDeadzone = 0.08f;

        static float ReadPedal(int axis, float rest, float full)
        {
            if (axis <= 0) return 0f;
            float raw = ReadRawAxis(axis);
            if (Mathf.Abs(full - rest) < 0.05f) return 0f; // 校正不足
            float v = Mathf.Clamp01((raw - rest) / (full - rest));
            // 離した付近の遊び(較正ズレ由来の“幽霊入力”)を除去して再スケール
            if (v < PedalDeadzone) return 0f;
            return (v - PedalDeadzone) / (1f - PedalDeadzone);
        }
    }
}
