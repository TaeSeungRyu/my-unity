# Unity 3D 스톰프 액션 게임

캐릭터가 3D 공간을 자유롭게 이동/점프하며 몰려오는 적을 **피하거나 밟아서** 처치하는 간단한 아케이드 게임. 체력 3개가 모두 떨어지면 게임 오버와 함께 최종 점수가 표시된다.

---

## 개발 환경

| 항목 | 버전 |
|------|------|
| Unity | 6000.3.0f1 |
| Render Pipeline | Universal RP 17.3.0 |
| Input | New Input System 1.16.0 |
| UI | TextMeshPro (com.unity.ugui 2.0.0 내장) |

---

## 조작법

| 입력 | 동작 |
|------|------|
| `W` `A` `S` `D` / 방향키 | 카메라 기준 이동 |
| `Space` | 점프 |
| (자동) 적 위에서 낙하 | 밟아서 처치 & 튕겨오름 |

---

## 게임 규칙

- 시작 **HP: 3** / 시작 **Score: 0**
- 적 **측면·아래** 충돌 → HP −1 (플레이어가 반대로 튕겨나감)
- 적 **위에서 낙하**하며 접촉 → 적 처치, 점수 획득, 플레이어 튕겨오름
- 시간이 지날수록 스폰 간격이 짧아져 **난이도 상승**
- 플레이 영역은 **40×40**, 네 면이 벽으로 막혀있음
- HP가 0이 되면 게임 오버 → 최종 점수 + **다시 시작** 버튼

---

## 적 유형 (5종)

| # | 이름 | 외형 | 행동 | 체력 | 점수 |
|---|------|------|------|-----|-----|
| ① | Walker | 갈색 블록형(뿔·발·눈) | 순찰 | 1 | 100 |
| ② | Chaser | 붉은 가시형(노란 눈) | 플레이어 추적 | 1 | 200 |
| ③ | Jumper | 노란 안테나 공 | 주기적 점프로 접근 | 1 | 250 |
| ④ | Flyer | 보라 UFO(돔·발광등) | 공중 부유·수평 추적 | 1 | 300 |
| ⑤ | Tank | 회색 장갑차(터렛·포신) | 느린 추적 | 2 | 500 |

Tank는 **2번 밟아야** 처치 가능. 첫 스톰프 시 색이 어두워져 HP가 남았음을 표시.

Flyer는 공중에 떠 있어 서서는 닿을 수 없음 — 점프해서 위에서 낙하해야 처치 가능.

---

## 씬 설치

처음 한 번만 해주세요:

1. **TMP 에센셜 리소스 임포트**
   `Window → TextMeshPro → Import TMP Essential Resources`
2. **씬 자동 구성**
   `Tools → Setup Game Scene` → 진행
3. ▶ 재생

`Setup Game Scene`을 재실행하면 기존 오브젝트(Ground, Player, Walls, UI 등)를 제거하고 새로 생성합니다.

---

## 프로젝트 구조

```
Assets/Scripts/
├── Player/
│   ├── PlayerController.cs   # 이동·점프·스톰프 판정 (OnCollisionStay 기반 접지)
│   └── CameraFollow.cs       # 3인칭 추적 카메라
├── Enemies/
│   ├── EnemyBase.cs          # 공통 체력/피해/밟기 처리 (추상)
│   ├── WalkerEnemy.cs        # ① 순찰형
│   ├── ChaserEnemy.cs        # ② 추적형
│   ├── JumperEnemy.cs        # ③ 점프형
│   ├── FlyerEnemy.cs         # ④ 비행형 (useGravity=false, velocity 이동)
│   └── TankEnemy.cs          # ⑤ 탱크형 (체력 2)
├── Managers/
│   ├── GameManager.cs        # 싱글톤: HP·점수·게임오버·재시작
│   └── EnemySpawner.cs       # 주기 스폰 + 가중치 + 플레이 영역 클램프
├── UI/
│   └── UIManager.cs          # HUD + 게임오버 패널 (TMP + uGUI)
└── Editor/
    └── SceneSetup.cs         # [Tools/Setup Game Scene] 자동 씬 빌더
```

### 설계 메모

- **적 템플릿은 비활성 루트(`EnemyTemplates`) 아래의 GameObject**로 배치. `EnemySpawner`가 `Instantiate`로 복제해 활성화. 프리팹 애셋 파일이 필요 없어 배포가 가볍다.
- **복합 모양**: 루트는 빈 GameObject + 수동 콜라이더 + Rigidbody + 스크립트. 자식은 모두 **콜라이더 없는 시각 전용** 프리미티브(`AddVisual` 헬퍼).
- **피격 플래시**는 모든 자식 렌더러에 적용. `EnemyBase.Awake`에서 `GetComponentsInChildren<Renderer>()`로 배열 캐시.
- **접지 판정**은 `Physics.Raycast` 대신 `OnCollisionStay`에서 접촉 법선 `normal.y > 0.5` 체크 — 자기 콜라이더 오검출 이슈 회피.
- **Flyer**는 벽 충돌을 위해 `useGravity = false`만 끄고 `linearVelocity`로 이동(kinematic 아님). y는 기준 높이 쪽으로 스프링처럼 수렴.

---

## 튜닝 포인트 (인스펙터에서 바로 조정)

| 컴포넌트 | 필드 | 의미 |
|---------|------|------|
| `PlayerController` | `moveSpeed` / `jumpForce` / `stompBounceForce` | 조작감 |
| `EnemySpawner` | `startInterval` / `minInterval` / `rampPerSecond` | 난이도 곡선 |
| `EnemySpawner` | `playAreaMin/Max` | 플레이 영역 |
| `EnemyBase`(파생) | `moveSpeed` / `maxHealth` / `scoreValue` | 개별 적 밸런스 |
| `GameManager` | `maxHealth` | 시작 체력 |

---

## 알려진 한계

- 모델/애니메이션이 없는 **프리미티브 조합** 기반. 실제 게임에서는 `.fbx` 캐릭터로 교체 권장.
- **사운드 없음**.
- 1개 씬/1개 스테이지. 재시작은 현재 씬 리로드.
