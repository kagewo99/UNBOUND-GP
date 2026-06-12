using System;

namespace UnboundGP.Core
{
    /// <summary>
    /// ドライバーの生理的・能力的プロファイル。
    /// Human GP と Machine GP の違いは、突き詰めればこのクラスの数値の違いだけである。
    /// ──「ルールがないとき、最後に残る制約は人体だった」を実装で表現する中心点。
    /// </summary>
    [Serializable]
    public class DriverProfile
    {
        public string driverName = "DRIVER";
        public bool isHuman = true;

        /// <summary>持続的に耐えられる横G。超過するとブラックアウトゲージが上昇する。</summary>
        public float maxSustainedG = MachineStats.HumanSustainedGLimit;

        /// <summary>限界を大きく超えたGに意識を保てる目安秒数。</summary>
        public float blackoutOnsetSeconds = 2.5f;

        /// <summary>AI が限界に対してどれだけ攻めるか (0.8〜1.0)。人間ドライバーの個性付けにも使う。</summary>
        public float skill = 0.92f;

        /// <summary>人間ドライバーのプロファイルを生成する。</summary>
        public static DriverProfile Human(string name, float skill = 0.92f)
        {
            return new DriverProfile
            {
                driverName = name,
                isHuman = true,
                maxSustainedG = MachineStats.HumanSustainedGLimit,
                blackoutOnsetSeconds = 2.5f,
                skill = skill,
            };
        }

        /// <summary>AI ドライバーのプロファイルを生成する。G限界は事実上存在しない。</summary>
        public static DriverProfile Machine(string name)
        {
            return new DriverProfile
            {
                driverName = name,
                isHuman = false,
                maxSustainedG = 10000f, // 機械に失神はない
                blackoutOnsetSeconds = 9999f,
                skill = 0.99f,
            };
        }
    }
}
