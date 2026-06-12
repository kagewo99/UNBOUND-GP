using System;
using System.Collections.Generic;

namespace UnboundGP.Core
{
    /// <summary>
    /// プレイヤーの進行状況。JsonUtility で PlayerPrefs にシリアライズされる。
    /// (Vertical Slice ではセーブスロットは1つ)
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>研究ポイント (RP)。レース完走で獲得し、技術の解放に消費する。</summary>
        public int researchPoints = 60;

        /// <summary>解放済み技術の ID リスト。</summary>
        public List<string> unlockedTechIds = new List<string>();

        /// <summary>現在マシンに装備している技術の ID リスト。</summary>
        public List<string> equippedTechIds = new List<string>();

        /// <summary>完走したレース数 (統計用)。</summary>
        public int racesCompleted = 0;
    }
}
