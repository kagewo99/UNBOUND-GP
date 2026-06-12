using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// ドライバーの生理状態シミュレーション。本作のテーマを体験に変換する装置。
    ///
    /// 横Gがプロファイルの限界を超えるとブラックアウトゲージが上昇し、
    /// ・視野狭窄 (HUD のビネット) と操作精度の低下
    /// ・ゲージ満タンで失神 → 数秒間ノーコントロール
    /// が発生する。Machine GP の AI ドライバーは限界値が事実上無限のため、
    /// 同じコードを通っても何も起きない。「違いはルールではなく肉体」を実装で示す。
    /// </summary>
    public class DriverCondition : MonoBehaviour
    {
        public DriverProfile Profile { get; private set; }

        /// <summary>0〜1。1 で失神。HUD のビネット濃度にも使う。</summary>
        public float BlackoutMeter { get; private set; }

        public bool IsBlackedOut { get; private set; }

        /// <summary>現在の横G (表示用)。</summary>
        public float CurrentG { get; private set; }

        // ---- レース統計 (デブリーフで提示し“気づき”の根拠にする) ----
        public float PeakG { get; private set; }
        public int BlackoutCount { get; private set; }
        /// <summary>人間限界 (5G) を超えていた合計時間。Machine GP でも記録する。</summary>
        public float TimeOverHumanLimit { get; private set; }

        /// <summary>意識レベルによる操作係数。1=正常、0=失神。</summary>
        public float ControlFactor => IsBlackedOut ? 0f : 1f - BlackoutMeter * 0.6f;

        public void Setup(DriverProfile profile)
        {
            Profile = profile;
        }

        /// <summary>CarController が物理ステップごとに横Gを報告する。</summary>
        public void ReportG(float lateralG, float dt)
        {
            CurrentG = lateralG;
            if (lateralG > PeakG) PeakG = lateralG;
            if (lateralG > MachineStats.HumanSustainedGLimit) TimeOverHumanLimit += dt;

            if (Profile == null) return;
            float tol = Profile.maxSustainedG;

            if (lateralG > tol)
            {
                // 限界をどれだけ超えたかに比例して意識が削れる
                BlackoutMeter += (lateralG - tol) / tol * (dt / Profile.blackoutOnsetSeconds);
            }
            else
            {
                BlackoutMeter -= dt * 0.4f; // 回復
            }
            BlackoutMeter = Mathf.Clamp01(BlackoutMeter);

            if (!IsBlackedOut && BlackoutMeter >= 1f)
            {
                IsBlackedOut = true;
                BlackoutCount++;
            }
            else if (IsBlackedOut && BlackoutMeter <= 0.35f)
            {
                IsBlackedOut = false; // 意識回復
            }
        }
    }
}
