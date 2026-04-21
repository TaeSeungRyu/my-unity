# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 3D 스톰프 액션 게임. 플레이어가 몰려오는 5종의 적을 피하거나 밟아서 처치하며, HP 3개를 모두 잃으면 게임 오버. 자세한 게임플레이 사양은 `README.md` 참고. 코드 주석과 UI 문자열은 한국어로 유지한다.

## Environment

- **Unity 6000.3.0f1** (`ProjectSettings/ProjectVersion.txt` 기준; Unity Hub에서 동일 버전 필요)
- **URP 17.3.0** — 머티리얼 생성 시 `Universal Render Pipeline/Lit` → `Standard` → `Unlit/Color` 순으로 폴백. 색상은 `_BaseColor`(URP)와 `_Color`(Built-in)를 **모두** 세팅해 호환성 확보.
- **New Input System 1.16.0** — 구 Input Manager(`Input.GetAxis`, `Input.GetKey`)는 사용하지 않는다. `Keyboard.current` 등을 직접 폴링.
- **API 주의**: `Rigidbody.linearVelocity`(구 `velocity` 대체), `FindFirstObjectByType<T>()`(구 `FindObjectOfType<T>()` 대체), `InputSystemUIInputModule` 사용.

## Workflow (Unity Editor)

이 프로젝트에는 빌드/테스트용 CLI가 없다. 모든 실행은 Unity 에디터에서 수행한다.

1. **Tools → Setup Game Scene** — 현재 씬을 자동 구성(지면, 벽 40×40, 플레이어, EnemyTemplates, Spawner, UI, 카메라). 재실행 시 동일 이름의 기존 오브젝트를 `DestroyImmediate`로 정리 후 재생성하므로 **수동 편집은 유실된다**.
2. **Window → TextMeshPro → Import TMP Essential Resources** — TMP 폰트 자산이 없으면 UI가 깨지므로 처음 한 번 필수.
3. **Tools → Auto-Assign Models to VisualOverrides** — `Assets/Models/` 안의 FBX/GLB/OBJ/Prefab을 파일명 키워드로 `VisualOverrides.asset`의 슬롯에 자동 연결(예: "ufo" → flyer, "tank"/"robot" → tank). Kenney Blocky Characters 같이 `character-a`, `character-b` … 만 있는 팩은 알파벳 순 폴백으로 빈 슬롯에 채워진다.
4. 씬 로드 후 ▶ Play.

테스트 프레임워크(`com.unity.test-framework`)는 매니페스트에 있지만 실제 테스트는 작성되어 있지 않다.

## Architecture

### Scene bootstrap is code-driven

플레이 가능한 씬 전체를 **`Assets/Scripts/Editor/SceneSetup.cs`** 한 파일이 런타임 없이 에디터 메뉴에서 생성한다. 이 파일이 씬 구조의 **정본**이므로, 씬 구성 변경은 보통 `SceneSetup`을 수정해 재실행해야 하며 수동 씬 편집은 재생성으로 덮여쓴다. Ground/Walls/Player/GameManager/EnemyTemplates/EnemySpawner/UIManager/Canvas/EventSystem/Main Camera가 모두 여기서 새로 만들어진다.

### Enemy templates, not prefab assets

`SceneSetup`은 5종의 적을 **비활성(`SetActive(false)`) 루트 `EnemyTemplates` 아래**에 자식 GameObject로 배치한다. `.prefab` 파일을 쓰지 않고, `EnemySpawner`가 이 자식들을 `Instantiate`해서 사용한다. 따라서:
- 적 외형/스탯 변경은 `SceneSetup`의 `BuildWalker/BuildChaser/…` 메서드를 수정하거나, 개별 템플릿 인스펙터를 건드린 뒤 저장해야 지속된다.
- 적은 새 씬 생성 시 재구성된다.

### Composite model pattern

플레이어와 적은 모두 동일한 패턴으로 조립된다:
- **루트**: 빈 GameObject + 수동 `Collider` + `Rigidbody`(X,Z 회전 고정) + 동작 스크립트.
- **자식**: `AddVisual` 헬퍼가 붙인 **콜라이더 없는 시각 전용** 프리미티브(캡슐/큐브/구/실린더).
- 피격 플래시·스톰프 하이라이트는 `EnemyBase.Awake`에서 `GetComponentsInChildren<Renderer>()`로 캐시한 배열 전체에 일괄 적용 — 한 루트 아래의 모든 시각 파츠가 동시에 색이 바뀐다.

### VisualOverrides swap system

`Assets/Prefabs/VisualOverrides.asset`(ScriptableObject)은 각 슬롯(player/walker/chaser/jumper/flyer/tank)에 대체 모델 Prefab을 연결한다. `SceneSetup.TryAttachVisualOverride`는 슬롯이 채워져 있으면:
1. 기본 프리미티브 조립을 스킵.
2. 해당 Prefab을 루트의 자식으로 인스턴스화.
3. **자식의 모든 `Collider`와 `Rigidbody`를 제거**(루트가 물리를 전담).
4. 루트에 `ProceduralWalkAnimation`을 부착.

이 때문에 외부 모델 교체 시에도 물리·스크립트·피격 플래시 로직을 그대로 재사용한다.

### Grounded detection uses OnCollisionStay, not Raycast

`PlayerController`는 의도적으로 `Physics.Raycast` 대신 `OnCollisionStay`에서 접촉점 법선 `normal.y > 0.5`를 검사해 접지를 판정한다(자기 콜라이더 오검출 회피). `groundedThisFrame`을 `OnCollisionStay`에서 세팅하고 `FixedUpdate` 시작 시 `isGrounded`로 확정 후 리셋하는 **one-frame 패턴**이므로 이 흐름을 깨지 말 것. (예외적으로 `LandingIndicator`는 Raycast로 접지/궤적을 별도 판단한다 — 착지점 예측 목적이라 자기 콜라이더 영향이 적음.)

### Flyer is dynamics with gravity off

`FlyerEnemy`는 `rb.useGravity = false`만 끄고 **kinematic은 쓰지 않는다**. 매 `FixedUpdate`마다 `rb.linearVelocity`를 직접 세팅해 수평 추적과 수직 사인 부유를 합성 — Physics 엔진이 벽 충돌을 처리하도록 하기 위함. kinematic으로 바꾸면 벽 통과가 발생한다.

### GameManager is an event-driven singleton

`GameManager.Instance`(HP/Score/IsGameOver)는 상태 변경을 `event Action<int>` 3개(`OnHealthChanged`, `OnScoreChanged`, `OnGameOver`)로 방송한다. `UIManager`가 이를 구독해 HUD를 갱신하며, 게임 오버 시 `Time.timeScale = 0`으로 전체 동작을 정지시킨 뒤 재시작은 현재 씬을 다시 로드한다. 새 UI 요소는 이 이벤트 패턴을 따라 구독한다.

### LandingIndicator predicts parabolic landing

플레이어가 공중에 있을 때 `LandingIndicator`가 현재 속도·중력으로 2차 방정식을 풀어 착지 지점을 예측하고 원반 인디케이터를 표시한다. 착지점 반경 내 스톰프 가능한 적이 있으면 `EnemyBase.SetStompHighlight(true)`로 해당 적의 모든 렌더러를 하이라이트 색으로 바꾼다. 새 적 유형을 추가할 때 `CanBeStomped=false`로 두면 하이라이트 대상에서 제외된다.

### ProceduralWalkAnimation for imported rigs

애니메이션 클립이 없는 FBX(Kenney Blocky Characters 스타일)를 위해 자식 Transform 이름을 키워드(`arm`/`leg`/`shoulder`/`thigh` + `.l`/`_l`/`-l`/`left` 등)로 검색해 팔/다리 뼈를 찾고 `LateUpdate`에서 X축 회전을 직접 부여한다. 뼈를 못 찾으면(UFO 등) 무동작이므로 **모든 루트에 안전하게 부착 가능**. 임포트된 FBX에 자동 추가되는 `Animator`는 간섭 방지를 위해 `Awake`에서 비활성화한다.

## Adding a new enemy type

1. `EnemyBase`를 상속하는 스크립트를 `Assets/Scripts/Enemies/`에 추가. `Awake`에서 `base.Awake()` 호출 후 `maxHealth`/`scoreValue`/`moveSpeed` 설정.
2. `SceneSetup`에 `BuildXxx(Transform parent, VisualOverrides overrides)` 메서드 추가. `CreateEnemyRoot` → 콜라이더 부착 → 스크립트 AddComponent → `TryAttachVisualOverride` → `AddVisual` 호출 순서.
3. `SceneSetup.Setup()`에서 템플릿 생성 및 `sp.entries`에 `SpawnEntry` 추가(가중치 부여).
4. 외부 모델 지원이 필요하면 `VisualOverrides`에 슬롯 필드 추가 + `VisualOverridesAutoAssign.SlotKeywords`에 매칭 키워드 추가.
5. **Tools → Setup Game Scene** 재실행으로 씬 재생성.

## Tuning points

런타임 파라미터는 코드 상수가 아니라 인스펙터에서 조정:
- `PlayerController`: `moveSpeed`, `jumpForce`, `stompBounceForce`, `stompAboveMargin`, `damageCooldown`, `maxJumps`(2단 점프)
- `EnemySpawner`: `startInterval`, `minInterval`, `rampPerSecond`(난이도 곡선), `playAreaMin/Max`(벽 내부 클램프), `maxAlive`
- `EnemyBase` 파생: `maxHealth`, `scoreValue`, `moveSpeed`, `stompHighlightColor`
- `LandingIndicator`: `indicatorRadius`, `enemyHighlightRadius`, `maxPredictTime`
- `GameManager`: `maxHealth`(시작 체력)
