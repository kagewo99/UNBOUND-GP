using System.Collections.Generic;
using UnityEngine;

namespace UnboundGP.Data
{
    /// <summary>研究ツリー全体。ノードの所属リストを保持する。</summary>
    [CreateAssetMenu(menuName = "UNBOUND GP/Tech Tree", fileName = "TechTree")]
    public class TechTreeData : ScriptableObject
    {
        public List<TechNodeData> nodes = new List<TechNodeData>();

        /// <summary>techId からノードを検索する。見つからなければ null。</summary>
        public TechNodeData FindById(string techId)
        {
            foreach (var n in nodes)
            {
                if (n != null && n.techId == techId) return n;
            }
            return null;
        }
    }
}
