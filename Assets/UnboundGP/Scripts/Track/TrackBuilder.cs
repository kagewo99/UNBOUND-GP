using UnboundGP.Data;
using UnityEngine;

namespace UnboundGP.Track
{
    /// <summary>
    /// TrackData から走行可能なコースを実行時に組み立てる。
    /// 路面メッシュ・ウォール(コリジョン)・スタートライン・地面を生成し、TrackPath を返す。
    /// アセットを一切要求しないため、Race シーンは空でも成立する。
    /// </summary>
    public static class TrackBuilder
    {
        public static TrackPath Build(TrackData data)
        {
            var root = new GameObject("RealityTrack_" + data.trackName);

            var path = root.AddComponent<TrackPath>();
            path.Init(data, 1000);

            BuildGround(root.transform);
            BuildRoad(root.transform, path, data.roadWidth);
            BuildWalls(root.transform, path, data.roadWidth * 0.5f + 3f);
            BuildStartLine(root.transform, path, data.roadWidth);

            return path;
        }

        // ----------------------------------------------------------------
        // 路面
        // ----------------------------------------------------------------
        static void BuildRoad(Transform parent, TrackPath path, float width)
        {
            int n = path.SampleCount;
            float hw = width * 0.5f;

            var verts = new Vector3[n * 2];
            var tris = new int[n * 6];

            for (int i = 0; i < n; i++)
            {
                Vector3 fwd = path.Forwards[i];
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                verts[i * 2] = path.Points[i] - right * hw;     // 左端
                verts[i * 2 + 1] = path.Points[i] + right * hw; // 右端
            }

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                int l0 = i * 2, r0 = i * 2 + 1;
                int l1 = next * 2, r1 = next * 2 + 1;
                int t = i * 6;
                // 上向きの面になる巻き順
                tris[t] = l0; tris[t + 1] = l1; tris[t + 2] = r0;
                tris[t + 3] = r0; tris[t + 4] = l1; tris[t + 5] = r1;
            }

            var mesh = new Mesh { name = "RoadMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Road");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(new Color(0.16f, 0.16f, 0.18f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ----------------------------------------------------------------
        // ウォール: 見た目はメッシュ、当たりは BoxCollider の列で堅牢にする
        // ----------------------------------------------------------------
        static void BuildWalls(Transform parent, TrackPath path, float offset)
        {
            const float wallHeight = 1.4f;
            const int step = 4; // サンプル4つごとに1セグメント

            var wallRoot = new GameObject("Walls");
            wallRoot.transform.SetParent(parent, false);
            var mat = MakeMaterial(new Color(0.85f, 0.2f, 0.2f));
            var matW = MakeMaterial(new Color(0.92f, 0.92f, 0.92f));

            int n = path.SampleCount;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < n; i += step)
                {
                    int next = (i + step) % n;
                    Vector3 fwdA = path.Forwards[i];
                    Vector3 rightA = Vector3.Cross(Vector3.up, fwdA).normalized;
                    Vector3 fwdB = path.Forwards[next];
                    Vector3 rightB = Vector3.Cross(Vector3.up, fwdB).normalized;

                    Vector3 a = path.Points[i] + rightA * (offset * side);
                    Vector3 b = path.Points[next] + rightB * (offset * side);
                    Vector3 center = (a + b) * 0.5f + Vector3.up * (wallHeight * 0.5f);
                    Vector3 dir = b - a;
                    if (dir.sqrMagnitude < 1e-4f) continue;

                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = "WallSeg";
                    seg.transform.SetParent(wallRoot.transform, false);
                    seg.transform.SetPositionAndRotation(center, Quaternion.LookRotation(dir));
                    seg.transform.localScale = new Vector3(0.5f, wallHeight, dir.magnitude + 0.6f);
                    // 赤白の縞でサーキット感を出す
                    seg.GetComponent<MeshRenderer>().sharedMaterial = (i / step) % 2 == 0 ? mat : matW;
                }
            }
        }

        // ----------------------------------------------------------------
        // スタート/フィニッシュライン
        // ----------------------------------------------------------------
        static void BuildStartLine(Transform parent, TrackPath path, float width)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "StartLine";
            Object.Destroy(line.GetComponent<Collider>());
            line.transform.SetParent(parent, false);
            line.transform.SetPositionAndRotation(
                path.Points[0] + Vector3.up * 0.03f,
                Quaternion.LookRotation(path.Forwards[0]));
            line.transform.localScale = new Vector3(width, 0.05f, 2f);
            line.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(Color.white);
        }

        // ----------------------------------------------------------------
        // 地面
        // ----------------------------------------------------------------
        static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.15f, 50f);
            ground.transform.localScale = new Vector3(150f, 1f, 150f); // 1500m 四方
            ground.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(new Color(0.22f, 0.42f, 0.2f));
        }

        /// <summary>レンダーパイプラインを問わず動くマテリアル生成。</summary>
        public static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader) { color = color };
        }
    }
}
