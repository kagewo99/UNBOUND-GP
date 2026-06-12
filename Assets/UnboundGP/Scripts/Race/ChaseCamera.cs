using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 追従カメラ。速度で FOV が広がり、Machine GP の異常な速度域を体感させる。
    /// SetTarget で観戦対象を切り替えられる (Machine GP の Tab 切り替え用)。
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        CarController target;
        Camera cam;

        const float Distance = 10f;
        const float Height = 4f;

        public void SetTarget(CarController car)
        {
            target = car;
            cam = GetComponent<Camera>();
            if (target != null)
            {
                // 切り替え時に即座に背後へワープ
                transform.position = TargetPosition();
            }
        }

        void LateUpdate()
        {
            if (target == null) return;

            transform.position = Vector3.Lerp(transform.position, TargetPosition(), 6f * Time.deltaTime);
            transform.LookAt(target.transform.position + Vector3.up * 1.2f + target.transform.forward * 4f);

            if (cam != null)
            {
                float t = Mathf.Clamp01(target.CurrentSpeedKmh / 400f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(60f, 85f, t), 3f * Time.deltaTime);
            }
        }

        Vector3 TargetPosition()
        {
            return target.transform.position
                   - target.transform.forward * Distance
                   + Vector3.up * Height;
        }
    }
}
