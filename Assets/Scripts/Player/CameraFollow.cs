using UnityEngine;

// 플레이어를 3인칭 시점으로 부드럽게 따라다니는 간단한 카메라.
public class CameraFollow : MonoBehaviour
{
    public Transform target;                 // 따라갈 대상(플레이어)
    public Vector3   offset = new Vector3(0f, 6f, -8f); // 카메라 오프셋
    public float     followLerp = 6f;        // 위치 추종 속도
    public float     lookLerp   = 10f;       // 시선 회전 속도

    void LateUpdate()
    {
        if (target == null) return;

        // 목표 위치: 타깃에서 오프셋만큼 떨어진 곳
        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, followLerp * Time.deltaTime);

        // 타깃의 살짝 위를 바라보도록 회전
        Quaternion lookRot = Quaternion.LookRotation((target.position + Vector3.up) - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, lookLerp * Time.deltaTime);
    }
}
