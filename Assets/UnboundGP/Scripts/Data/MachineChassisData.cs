using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.Data
{
    /// <summary>
    /// ベースシャシー。技術を何も装備しない状態のマシン性能 (≒現代F1相当)。
    /// 将来的に複数シャシー (重戦車型/軽量型など) を追加する拡張点。
    /// </summary>
    [CreateAssetMenu(menuName = "UNBOUND GP/Machine Chassis", fileName = "Chassis")]
    public class MachineChassisData : ScriptableObject
    {
        public string chassisName = "UNBOUND Type-0";

        [TextArea(2, 3)]
        public string description;

        public MachineStats baseStats;
    }
}
