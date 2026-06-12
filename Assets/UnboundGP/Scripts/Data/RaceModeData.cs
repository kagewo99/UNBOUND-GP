using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.Data
{
    /// <summary>
    /// レースモード (Human GP / Machine GP) の演出・パラメータ定義。
    /// ゲームの最重要テーマ「人間という制約の有無」をデータとして外部化する。
    /// </summary>
    [CreateAssetMenu(menuName = "UNBOUND GP/Race Mode", fileName = "RaceMode")]
    public class RaceModeData : ScriptableObject
    {
        public GameMode mode;

        public string title;

        [Tooltip("モード選択カードに表示する一行コピー")]
        public string tagline;

        [TextArea(3, 6)]
        public string description;

        [Header("ルール")]
        [Tooltip("true ならプレイヤーが自分で運転する。false なら AI が運転し、プレイヤーは観戦する。")]
        public bool playerDrives = true;

        [Tooltip("ライバル AI の台数")]
        public int aiOpponentCount = 3;

        [Header("デブリーフ")]
        [TextArea(3, 8)]
        [Tooltip("レース後に表示される“気づき”の核心テキスト。")]
        public string debriefInsight;
    }
}
