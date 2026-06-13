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
            BuildWalls(root.transform, path, data.roadWidth * 0.5f + 0.8f);
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
            // 背を高く(3m)、路面より 0.8m 下から立ち上げる。
            // こうすると立体交差の標高変化部でも壁と路面の間に隙間ができず、車が下へ抜けない。
            const float wallHeight = 3.0f;
            const float rootBelow = 0.8f;     // 路面下へ食い込ませる量
            const int step = 2;               // サンプル2つごと=密に置いて隙間を防ぐ

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
                    Vector3 rightA = Vector3.Cross(Vector3.up, path.Forwards[i]).normalized;
                    Vector3 rightB = Vector3.Cross(Vector3.up, path.Forwards[next]).normalized;

                    Vector3 a = path.Points[i] + rightA * (offset * side);
                    Vector3 b = path.Points[next] + rightB * (offset * side);
                    // 中心を持ち上げる量 = 高さ/2 − 食い込み。底が路面より rootBelow だけ下になる。
                    Vector3 center = (a + b) * 0.5f + Vector3.up * (wallHeight * 0.5f - rootBelow);
                    Vector3 dir = b - a;
                    if (dir.sqrMagnitude < 1e-4f) continue;

                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = "WallSeg";
                    seg.transform.SetParent(wallRoot.transform, false);
                    seg.transform.SetPositionAndRotation(center, Quaternion.LookRotation(dir));
                    // 厚さ0.8m、長さは隣接セグメントと重なるよう余分に伸ばす(隙間防止)
                    seg.transform.localScale = new Vector3(0.8f, wallHeight, dir.magnitude + 1.2f);
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
