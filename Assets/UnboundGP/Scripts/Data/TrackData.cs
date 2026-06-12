using System.Collections.Generic;
using UnityEngine;

namespace UnboundGP.Data
{
    /// <summary>
    /// サーキット定義。制御点リストから Catmull-Rom スプラインでコース形状を生成する。
    /// "Reality Track" = 実在サーキット風レイアウト。制御点を差し替えるだけで新コースを追加できる。
    /// </summary>
    [CreateAssetMenu(menuName = "UNBOUND GP/Track", fileName = "Track")]
    public class TrackData : ScriptableObject
    {
        public string trackName = "Reality Track";

        [TextArea(2, 4)]
        public string description;

        [Tooltip("路面の幅 [m]")]
        public float roadWidth = 14f;

        [Tooltip("レースの周回数")]
        public int lapCount = 3;

        [Tooltip("コース中心線の制御点 (閉ループ)。y は標高で、立体交差を表現できる。")]
        public List<Vector3> controlPoints = new List<Vector3>();
    }
}
