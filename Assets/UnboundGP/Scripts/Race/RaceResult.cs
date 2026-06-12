using UnboundGP.Core;

namespace UnboundGP.Race
{
    /// <summary>
    /// レース1回分の結果と、デブリーフ画面が“気づき”を構成するための統計。
    /// </summary>
    public class RaceResult
    {
        public GameMode mode;

        /// <summary>表示用の最終順位行 ("1. NAME — BEST 1:23.456" 等)。</summary>
        public string[] standingLines;

        public int playerPlace;
        public float playerBestLap;

        // ---- ドライバー生理統計 ----
        public float peakG;
        public int blackoutCount;
        public float timeOverHumanLimit;

        // ---- 同一マシンの理論値比較 (体験の核心) ----
        public float estHumanLap;
        public float estMachineLap;

        public int pointsAwarded;
    }
}
