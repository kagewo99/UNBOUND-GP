using System.Collections.Generic;
using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.Data
{
    /// <summary>
    /// 研究ツリーの1ノード = 1つの「現実のF1では禁止された(あるいは存在しえない)技術」。
    /// ScriptableObject なのでデザイナーがエディタ上で追加・調整できる。
    /// </summary>
    [CreateAssetMenu(menuName = "UNBOUND GP/Tech Node", fileName = "TechNode")]
    public class TechNodeData : ScriptableObject
    {
        [Header("識別")]
        [Tooltip("セーブデータに保存される一意なID。後から変更しないこと。")]
        public string techId;

        public string displayName;

        [Tooltip("ツリー上の段 (0 = 基礎)。UI のグルーピングに使用。")]
        public int tier;

        [Header("解放条件")]
        [Tooltip("解放に必要な研究ポイント。0 なら最初から解放済み。")]
        public int researchCost;

        [Tooltip("このノードを解放する前に必要な技術。")]
        public List<TechNodeData> prerequisites = new List<TechNodeData>();

        [Header("効果")]
        [Tooltip("装備時にマシン性能へ適用される修正値。")]
        public List<StatModifier> modifiers = new List<StatModifier>();

        [Header("テキスト")]
        [TextArea(2, 4)]
        public string description;

        [TextArea(2, 4)]
        [Tooltip("現実のF1でこの技術がどう扱われたか。プレイヤーが“レギュレーションの意味”に気づくための重要なフレーバー。")]
        public string realWorldNote;
    }
}
