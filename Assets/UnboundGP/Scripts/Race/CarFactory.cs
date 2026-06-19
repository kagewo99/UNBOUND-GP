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
            DriverProfile profile, bool isPlayerControlled, TrackPath path, bool hasCVT = false)
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
            car.Setup(stats, input, condition, hasCVT);

            // ---- 重量感ビジュアル(バネ上ボディ+ホイール)と音 ----
            BuildVisual(root.transform, teamColor, out var bodyPivot, out var wheels, out var isFront);
            root.AddComponent<CarVisuals>().Setup(car, bodyPivot, wheels, isFront);
            root.AddComponent<CarAudio>().Setup(car);

            // AIDriver は CarController 生成後に参照を渡す
            if (input is AIDriver aiDriver) aiDriver.Setup(car, path, condition);

            return car;
        }

        // ----------------------------------------------------------------
        // ビジュアル:バネ上ボディ(Body)+回転/操舵するホイール
        // ----------------------------------------------------------------
        static void BuildVisual(Transform parent, Color color,
            out Transform bodyPivot, out Transform[] wheels, out bool[] isFront)
        {
            var bodyMat = TrackBuilder.MakeMaterial(color);
            var darkMat = TrackBuilder.MakeMaterial(new Color(0.08f, 0.08f, 0.08f));
            var tireMat = TrackBuilder.MakeMaterial(new Color(0.06f, 0.06f, 0.06f));

            // バネ上ピボット(車軸高さ付近に置き、ここを傾けると車体だけが沈む/傾く)
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(parent, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            bodyPivot = bodyGo.transform;

            // 車体パーツは Body の子(ピボット基準にローカル位置を補正)
            AddPart(bodyPivot, bodyMat, new Vector3(0f, 0.15f, 0f), new Vector3(1.0f, 0.45f, 4.2f));     // モノコック
            AddPart(bodyPivot, bodyMat, new Vector3(0f, 0.20f, 2.3f), new Vector3(0.45f, 0.25f, 1.0f));  // ノーズ
            AddPart(bodyPivot, darkMat, new Vector3(0f, 0.50f, -0.4f), new Vector3(0.6f, 0.35f, 0.8f));  // コックピット
            AddPart(bodyPivot, darkMat, new Vector3(0f, 0.65f, -2.0f), new Vector3(1.7f, 0.08f, 0.5f));  // リアウイング
            AddPart(bodyPivot, darkMat, new Vector3(0f, 0.0f, 2.2f), new Vector3(1.8f, 0.08f, 0.4f));    // フロントウイング

            // ホイール(円筒)。回転/操舵は CarVisuals が制御するので位置のみ設定。
            wheels = new Transform[4];
            isFront = new bool[4];
            var pos = new[]
            {
                new Vector3(-0.95f, 0.35f, 1.5f),  // FL
                new Vector3( 0.95f, 0.35f, 1.5f),  // FR
                new Vector3(-0.95f, 0.35f, -1.6f), // RL
                new Vector3( 0.95f, 0.35f, -1.6f), // RR
            };
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(w.GetComponent<Collider>());
                w.name = "Wheel" + i;
                w.transform.SetParent(parent, false);
                w.transform.localPosition = pos[i];
                // 円筒は既定で Y が長手。CarVisuals が Euler(0,0,90) を掛けて軸をXへ向ける。
                w.transform.localScale = new Vector3(0.7f, i < 2 ? 0.16f : 0.20f, 0.7f);
                w.GetComponent<MeshRenderer>().sharedMaterial = tireMat;
                wheels[i] = w.transform;
                isFront[i] = i < 2;
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
