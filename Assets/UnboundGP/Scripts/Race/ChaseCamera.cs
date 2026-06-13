using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 三人称の追従カメラ(主に Machine GP の観戦用)。
    /// 速度で FOV が広がり、異常な速度域を外から体感させる。
    /// SetTarget で対象を切り替えられる (Tab 切り替え)。
    /// </summary>
    public class ChaseCamera : MonoBehaviour, IRaceCamera
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
