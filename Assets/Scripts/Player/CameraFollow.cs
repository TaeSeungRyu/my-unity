using UnityEngine;

// 플레이어를 3인칭 시점으로 부드럽게 따라다니는 간단한 카메라.
// 스톰프 등 타격 시점에 Shake()를 호출해 짧은 진동을 더해 타격감을 강화한다.
public class CameraFollow : MonoBehaviour
{
    public Transform target;                 // 따라갈 대상(플레이어)
    public Vector3   offset = new Vector3(0f, 6f, -8f); // 카메라 오프셋
    public float     followLerp = 6f;        // 위치 추종 속도
    public float     lookLerp   = 10f;       // 시선 회전 속도

    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeMagnitude;

    // 외부에서 호출. 더 강한 진동이 들어오면 덮어쓰고, 약하면 무시(콤보 시 자연스러운 누적).
    public void Shake(float duration, float magnitude)
    {
        if (shakeTimeRemaining > 0f && magnitude < shakeMagnitude) return;
        shakeDuration       = Mathf.Max(0.01f, duration);
        shakeTimeRemaining  = shakeDuration;
        shakeMagnitude      = magnitude;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 목표 위치: 타깃에서 오프셋만큼 떨어진 곳
        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, followLerp * Time.deltaTime);

        // 진동: 시간 비율에 따라 점차 감쇠. unscaledDeltaTime을 써야 히트 스톱 중에도 정상 진행.
        if (shakeTimeRemaining > 0f)
        {
            shakeTimeRemaining -= Time.unscaledDeltaTime;
            float k   = Mathf.Clamp01(shakeTimeRemaining / shakeDuration);
            float amp = shakeMagnitude * k;
            transform.position += new Vector3(
                (Random.value - 0.5f) * 2f * amp,
                (Random.value - 0.5f) * 2f * amp,
                (Random.value - 0.5f) * 2f * amp);
        }

        // 타깃의 살짝 위를 바라보도록 회전
        Quaternion lookRot = Quaternion.LookRotation((target.position + Vector3.up) - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, lookLerp * Time.deltaTime);
    }
}
