using UnityEngine;

// 플레이어/적의 외형을 임포트한 모델 Prefab으로 교체하기 위한 설정 에셋.
// 슬롯이 비어 있으면 SceneSetup이 기본 도형(캡슐/큐브/스피어)으로 생성합니다.
// 에셋 위치: Assets/Prefabs/VisualOverrides.asset (SceneSetup 실행 시 자동 생성)
[CreateAssetMenu(fileName = "VisualOverrides", menuName = "Game/Visual Overrides")]
public class VisualOverrides : ScriptableObject
{
    [Tooltip("비어 있으면 기본 캡슐+구 도형이 사용됩니다. 발바닥(y=0) 피봇 권장.")]
    public GameObject playerVisual;

    [Tooltip("워커 적(갈색 블록) 대체용 모델 Prefab.")]
    public GameObject walkerVisual;

    [Tooltip("체이서 적(붉은 돌진형) 대체용 모델 Prefab.")]
    public GameObject chaserVisual;

    [Tooltip("점퍼 적(노란 통통) 대체용 모델 Prefab.")]
    public GameObject jumperVisual;

    [Tooltip("플라이어 적(UFO) 대체용 모델 Prefab. 공중에 떠 있도록 설계됨.")]
    public GameObject flyerVisual;

    [Tooltip("탱크 적(회색 중장갑) 대체용 모델 Prefab.")]
    public GameObject tankVisual;
}
