using UnboundGP.Core;
using UnboundGP.Track;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// プリミティブからフォーミュラカーの見た目と物理を実行時に組み立てる。
    /// アートアセット導入後はこのクラスをプレハブ生成に差し替えるだけでよい。
    /// </summary>
    public static class CarFactory
    {
        /// <summary>マシンを生成する。driverInput は PlayerInputDriver / AIDriver どちらでもよい。</summary>
        public static CarController Create(
            string entrantName, Color teamColor, MachineStats stats,
            Vector3 position, Quaternion rotation,
            DriverProfile profile, bool isPlayerControlled, TrackPath path)
        {
            var root = new GameObject("Car_" + entrantName);
            root.transform.SetPositionAndRotation(position, rotation);

            // ---- 物理 ----
            root.AddComponent<Rigidbody>();
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.9f, 0.8f, 4.6f);
            box.center = new Vector3(0f, 0.45f, 0f);
            box.material = new PhysicMaterial("Tire")
            {
                dynamicFriction = 0.1f,
                staticFriction = 0.1f,
                bounciness = 0f,
                frictionCombine = PhysicMaterialCombine.Minimum,
            };

            BuildVisual(root.transform, teamColor);

            // ---- ドライバー ----
            var condition = root.AddComponent<DriverCondition>();
            condition.Setup(profile);

            IDriverInput input;
            if (isPlayerControlled)
            {
                input = root.AddComponent<PlayerInputDriver>();
            }
            else
            {
                var ai = root.AddComponent<AIDriver>();
                input = ai;
            }

            var car = root.AddComponent<CarController>();
            car.Setup(stats, input, condition);
            car.AllowReverse = isPlayerControlled;   // 後退はプレイヤー機のみ

            // AIDriver は CarController 生成後に参照を渡す
            if (input is AIDriver aiDriver) aiDriver.Setup(car, path, condition);

            return car;
        }

        // ----------------------------------------------------------------
        // ビジュアル (コリジョンなしの飾り)
        // ----------------------------------------------------------------
        static void BuildVisual(Transform parent, Color color)
        {
            var bodyMat = TrackBuilder.MakeMaterial(color);
            var darkMat = TrackBuilder.MakeMaterial(new Color(0.08f, 0.08f, 0.08f));

            AddPart(parent, bodyMat, new Vector3(0f, 0.5f, 0f), new Vector3(1.0f, 0.45f, 4.2f));    // モノコック
            AddPart(parent, bodyMat, new Vector3(0f, 0.55f, 2.3f), new Vector3(0.45f, 0.25f, 1.0f)); // ノーズ
            AddPart(parent, darkMat, new Vector3(0f, 0.85f, -0.4f), new Vector3(0.6f, 0.35f, 0.8f)); // コックピット
            AddPart(parent, darkMat, new Vector3(0f, 1.0f, -2.0f), new Vector3(1.7f, 0.08f, 0.5f));  // リアウイング
            AddPart(parent, darkMat, new Vector3(0f, 0.35f, 2.2f), new Vector3(1.8f, 0.08f, 0.4f));  // フロントウイング

            // タイヤ x4
            for (int side = -1; side <= 1; side += 2)
            {
                AddPart(parent, darkMat, new Vector3(0.85f * side, 0.35f, 1.5f), new Vector3(0.35f, 0.7f, 0.7f));
                AddPart(parent, darkMat, new Vector3(0.85f * side, 0.35f, -1.6f), new Vector3(0.4f, 0.7f, 0.75f));
            }
        }

        static void AddPart(Transform parent, Material mat, Vector3 localPos, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(part.GetComponent<Collider>()); // 当たりはルートの BoxCollider のみ
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
