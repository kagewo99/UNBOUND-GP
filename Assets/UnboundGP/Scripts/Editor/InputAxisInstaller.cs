using UnityEditor;
using UnityEngine;

namespace UnboundGP.EditorTools
{
    /// <summary>
    /// 旧 Input Manager (ProjectSettings/InputManager.asset) に、
    /// ジョイスティック/ホイールの生軸を読むための汎用軸 "Joy_Axis_1".."Joy_Axis_10" を注入する。
    ///
    /// 旧 Input は事前に定義した軸しか Input.GetAxis で読めないため、これを入れておくと
    /// WheelMapping がデバイスの任意の軸(ステア・アクセル・ブレーキ)を参照できるようになる。
    /// 冪等(既にあれば何もしない)。Setup からも呼ばれる。
    /// </summary>
    public static class InputAxisInstaller
    {
        public const int AxisCount = 10;
        public const string Prefix = "Joy_Axis_";

        /// <summary>軸を冪等に追加し、追加件数を返す(ダイアログなし。Setup から呼ぶ用)。</summary>
        public static int EnsureAxes()
        {
            int added = 0;
            for (int i = 0; i < AxisCount; i++)
            {
                string name = Prefix + (i + 1);
                if (!AxisExists(name))
                {
                    AddJoystickAxis(name, i); // axis index 0=X,1=Y,2=3rd...
                    added++;
                }
            }
            AssetDatabase.SaveAssets();
            return added;
        }

        [MenuItem("UNBOUND GP/Install Wheel Input Axes")]
        public static void Install()
        {
            int added = EnsureAxes();
            Debug.Log($"[UNBOUND GP] ホイール入力軸を確認: {added} 件追加 (計 {AxisCount} 軸)。");
            EditorUtility.DisplayDialog("UNBOUND GP",
                $"ホイール入力軸を設定しました({added}件追加)。\n" +
                "ゲーム内のレース画面で F1 キーを押すとハンドル設定(キャリブレーション)が開きます。",
                "OK");
        }

        static SerializedObject GetInputManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            return assets.Length > 0 ? new SerializedObject(assets[0]) : null;
        }

        static bool AxisExists(string axisName)
        {
            var so = GetInputManager();
            if (so == null) return false;
            var axes = so.FindProperty("m_Axes");
            for (int i = 0; i < axes.arraySize; i++)
            {
                if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == axisName)
                    return true;
            }
            return false;
        }

        static void AddJoystickAxis(string axisName, int axisIndex)
        {
            var so = GetInputManager();
            if (so == null)
            {
                Debug.LogWarning("[UNBOUND GP] InputManager.asset を取得できませんでした。");
                return;
            }
            var axes = so.FindProperty("m_Axes");
            axes.arraySize++;
            var a = axes.GetArrayElementAtIndex(axes.arraySize - 1);

            a.FindPropertyRelative("m_Name").stringValue = axisName;
            a.FindPropertyRelative("descriptiveName").stringValue = "";
            a.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            a.FindPropertyRelative("negativeButton").stringValue = "";
            a.FindPropertyRelative("positiveButton").stringValue = "";
            a.FindPropertyRelative("altNegativeButton").stringValue = "";
            a.FindPropertyRelative("altPositiveButton").stringValue = "";
            a.FindPropertyRelative("gravity").floatValue = 0f;
            a.FindPropertyRelative("dead").floatValue = 0.001f;     // デッドゾーンはコード側で扱う
            a.FindPropertyRelative("sensitivity").floatValue = 1f;
            a.FindPropertyRelative("snap").boolValue = false;
            a.FindPropertyRelative("invert").boolValue = false;
            a.FindPropertyRelative("type").intValue = 2;            // 2 = Joystick Axis
            a.FindPropertyRelative("axis").intValue = axisIndex;    // 0-based
            a.FindPropertyRelative("joyNum").intValue = 0;          // すべてのジョイスティック

            so.ApplyModifiedProperties();
        }
    }
}
