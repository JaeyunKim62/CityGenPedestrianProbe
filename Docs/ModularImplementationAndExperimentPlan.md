# 모듈형 구현·브랜치·실험 계획

상태: 구현 시작 전 기준 계획
목표: 비시각 공간인지 피드백을 하나씩 독립 구현하고, 각 기능을 단독·조합·제거 조건으로 반복 시험할 수 있게 한다.

## 1. 결론

지금 시작할 수 있다. 현재 `main`에는 이미 `FeedbackModule`과 `FeedbackCoordinator`가 있어 모듈화의 출발점이 마련되어 있다.

다만 첫 작업은 인지 피드백 아이디어가 아니라 **공통 실험 기반**이어야 한다. 그 다음 첫 실제 피드백으로 **자기중심 좌우 스위프 프로브**를 구현한다.

권장 순서는 다음과 같다.

1. 현재 문서 변경을 안전하게 기준선으로 보존한다.
2. `infra/feedback-experiments` 브랜치에서 모듈 ID, on/off, registry, toggle log만 구현한다.
3. 기반을 `main`에 병합한다.
4. `feat/fb-01-egocentric-sweep` 브랜치에서 좌우 스위프 하나만 구현한다.
5. 기능을 끈 조건과 켠 조건을 각각 검증한다.
6. 모듈을 기본 OFF 상태로 `main`에 병합한다.
7. 같은 방식으로 다음 기능을 하나씩 추가한다.

브랜치는 기능 구현을 격리하는 용도이고, 기능 조합 조건은 runtime profile로 관리한다. `sweep+landmark`, `sweep+shoreline` 같은 조합마다 Git 브랜치를 새로 만들지 않는다.

## 2. 현재 상태와 시작 전 주의점

### 이미 있는 기반

- `FeedbackModule`: 피드백 모듈의 공통 부모
- `FeedbackCoordinator`: 모듈 검색·설정과 boundary event 전달
- `SemanticAudioGuide`: 현재 semantic 접근·진입·study-boundary 음향
- `SemanticSessionLogger`: 위치·heading·semantic과 사건 CSV
- `SemanticSpaceMap`: 현재 위치 분류와 반경 내 최근접 경계 질의
- Unity Input System과 EditMode test framework

### 아직 필요한 기반

- 안정된 module ID
- runtime enable/disable API
- 모듈 toggle 사건의 로그
- 기능 OFF 시 기존 동작과 완전히 같은지 확인하는 회귀 테스트

실험 profile, trial lock, 공통 입력 라우터, 피드백 세분화는 실제 두 번째 모듈 또는 정식 trial이 요구할 때 추가한다. 첫 기반에서 미래 요구를 추측해 구현하지 않는다.

### 현재 Git 상태

현재 브랜치는 `main`이며 `README.md` 수정과 `Docs/`의 새 문서들이 아직 working tree에 있다. 이 상태에서 바로 브랜치를 만들면 문서 변경도 새 브랜치에 함께 따라간다.

구현 전에 다음 중 하나로 기준선을 명확히 해야 한다.

- 권장: 현재 문서와 의도된 README 변경을 검토한 뒤 `main`에 문서 커밋
- 대안: 문서 전용 브랜치에 먼저 커밋하고 병합
- 임시 대안: 구현 브랜치 전에 stash하되, 어떤 변경을 숨겼는지 기록

이 계획 단계에서는 branch 생성이나 commit을 수행하지 않는다.

## 3. 브랜치 전략

### 원칙

- `main`은 컴파일되고 기존 probe가 동작하는 통합 기준선이다.
- 공통 실험 기반은 한 번만 별도 infra 브랜치에서 만든다.
- 실험 아이디어 하나당 짧은 feature 브랜치 하나를 사용한다.
- 새 feature 브랜치는 항상 최신 `main`에서 만든다.
- 미완성 기능도 기본 OFF이고 기존 동작을 바꾸지 않으면 병합할 수 있다.
- 실험 결과는 branch 이름이 아니라 commit hash + profile ID + map version으로 재현한다.
- 조합 조건마다 branch를 만들지 않는다.

### 권장 이름

| 순서 | 브랜치 | 내용 |
| --- | --- | --- |
| 0 | `infra/feedback-experiments` | module ID·토글·registry·로그 기반 |
| 1 | `feat/fb-01-egocentric-sweep` | 자기중심 좌우 스위프 v0 |
| 2 | `feat/fb-02-boundary-flow` | 경계 흐름 또는 피드포워드 shoreline |
| 3 | `feat/fb-03-junction-topology` | 교차로 포털·허브 |
| 4 | `feat/fb-04-reference-axis` | 출발축 또는 환경중심 방향축 |
| 5 | `feat/fb-05-user-landmark` | 선택적 자기지정 랜드마크 |
| 6 | `feat/fb-06-relation-replay` | 경로·관계 압축 재생 |

번호는 구현 의존성과 실험 순서를 나타낼 뿐, 인지 과정의 순서를 뜻하지 않는다. 결과에 따라 2번 이후 순서는 바꿀 수 있다.

### 한 기능의 반복 흐름

1. 최신 `main`에서 feature branch 생성
2. 질문·가설·최소 범위를 문서에 먼저 기록
3. detection 또는 geometry 계산 구현
4. feedback renderer 구현
5. module toggle과 profile 연결
6. 계산 단위 테스트
7. module OFF 회귀 확인
8. module ON 수동 확인
9. 자체 실험 1회 이상 기록
10. 기본 OFF로 병합

다음 기능은 이전 기능 브랜치에서 연속 개발하지 않고, 병합된 최신 `main`에서 시작한다. 그래야 기능별 diff와 실패 원인이 분리된다.

## 4. 공통 런타임 구조

### 세 층

| 층 | 책임 | 실험 토글 대상 |
| --- | --- | --- |
| 기반 | 이동, semantic map, logger, blind overlay, study bounds | 원칙적으로 아님 |
| 감지 | ray query, 경계 선분, 교차로 포털, 위치 관계 계산 | 필요한 모듈이 내부 사용 |
| 피드백 | 스위프, shoreline, junction, 축, landmark, replay | 예 |

현재 `SemanticAudioGuide`는 semantic 접근·진입음과 study-boundary 경고를 함께 갖는다. 첫 infra 브랜치에서는 이를 분리하지 않고 하나의 기존 module로 유지한다.

- 이유: 아직 비교할 두 번째 실험 module이 없고, 분리는 기존 동작을 크게 건드린다.
- 시점: boundary 경고를 유지한 채 semantic 접근음만 끄는 실제 조건이 생길 때 별도 refactor branch에서 수행한다.

그 전까지 `semantic.audio`의 OFF는 접근·진입·study-boundary 음향 전체가 꺼진다는 뜻으로 로그와 문서에 명시한다.

### FeedbackModule 계약

현재 추상 클래스에 개념적으로 다음 계약이 필요하다.

```text
ModuleId                 안정된 문자열 ID
IsFeedbackEnabled        현재 피드백 활성 상태
Configure(context)       공통 의존성 전달
SetFeedbackEnabled()     시작·중단·음원 정리 lifecycle
```

초기 module ID 예시:

- `semantic.audio`
- `probe.egocentric_sweep.audio`
- `boundary.flow.audio`
- `junction.topology.audio`
- `reference.axis.audio`
- `landmark.user.audio`
- `replay.relation.audio`

ID는 클래스명을 바꾸더라도 유지한다. 분석 데이터와 profile이 이 ID를 참조하기 때문이다.

`MonoBehaviour.enabled`만 직접 바꾸는 방식은 피한다. coordinator의 `SetModuleEnabled`를 거쳐야 모듈 lifecycle이 실행되고 toggle 이유가 로그에 남는다.

### FeedbackCoordinator 책임

- 모든 `FeedbackModule`을 module ID로 등록
- 중복 module ID 검사
- 개별 module on/off API 제공
- 상태 변경을 logger에 기록
- module ID와 현재 상태 조회 API 제공

현재 coordinator가 `SemanticAudioGuide`를 무조건 추가하는 코드는 기존 scene 호환성을 위해 유지한다. 등록된 component와 활성 상태만 분리한다. 두 번째 module부터는 각 feature branch가 자신의 component를 명시적으로 추가한다.

## 5. 실험 profile은 후속 계층으로 둔다

### 역할

Git branch는 코드 차이를 보존한다. 실험 profile은 같은 코드에서 어떤 모듈을 켰는지 보존한다.

공통 토글 API가 검증되고 실제 module이 두 개 이상 생기면 `FeedbackExperimentProfile`을 별도 작은 branch에서 추가할 수 있다. 예상 최소 필드는 다음과 같다.

| 필드 | 예시 |
| --- | --- |
| profile ID | `C001_SWEEP_ONLY` |
| revision | `1` |
| description | `baseline semantic off, sweep audio on` |
| enabled module IDs | `probe.egocentric_sweep.audio` |
| locked during trial | `true` |
| profile notes | 실험 의도와 알려진 제한 |

각 module은 자신의 설정 ID와 실제 파라미터 snapshot을 제공한다. profile 이름만 기록하고 inspector 값이 바뀌면 재현이 불가능하므로, session 시작 시 다음을 함께 로그로 남긴다.

- experiment ID
- run ID
- profile ID와 revision
- Git commit hash 또는 수동 입력된 build ID
- 활성 module ID 목록
- 각 module의 설정 snapshot
- map version과 database ID
- Unity version
- scene 또는 합성 환경 variant ID와 random seed
- 입력 binding revision
- 헤드폰·audio output 설정
- 햅틱 사용 시 장치 모델·firmware·강도 설정

### 수동 toggle과 실험 잠금

개발 중에는 UI 또는 debug key로 각 모듈을 켜고 끌 수 있게 한다. 모든 변경은 `module_toggle` 사건으로 기록한다.

정식 trial에서는 profile을 잠근다. 실험 도중 toggle이 연구 조건 자체가 아닌 이상, 우발적인 키 입력으로 조건이 바뀌면 안 된다. 의도적으로 trial 중 전환할 때는 `toggle_reason=protocol`과 단계 ID를 기록한다.

### 조합 profile

기능이 누적되면 다음처럼 profile을 만든다.

| profile | 활성 기능 |
| --- | --- |
| `C000_LOCAL_BASELINE` | 기존 국소 semantic만 |
| `C001_SWEEP_ONLY` | 스위프만 |
| `C002_FLOW_ONLY` | 경계 흐름만 |
| `C003_SWEEP_FLOW` | 스위프 + 경계 흐름 |
| `C004_ALL_CURRENT` | 현재 구현된 모든 실험 모듈 |
| `C005_ALL_MINUS_SWEEP` | 전체에서 스위프 제거 |

profile layer는 module 구현에 의존하지 않고 `FeedbackCoordinator.SetModuleEnabled`를 호출하기만 해야 한다. 초기에는 profile asset을 수동으로 작성하고, 모듈 수가 늘면 bitmask 또는 editor generator로 모든 조합을 생성한다.

## 6. 왜 좌우 스위프부터 시작하는가

### 후보 평가

| 후보 | 현재 구현 가능성 | 핵심 질문과의 직접성 | 새 의존성 | 첫 기능 판단 |
| --- | --- | --- | --- | --- |
| 자기중심 좌우 스위프 | 중상 | 매우 높음: 짧은 지각 지평을 직접 조작 | semantic ray query | **선정** |
| 환경중심 방향축 | 매우 높음 | 중간: 좌표틀 병목만 다룸 | 거의 없음 | 빠른 후속 후보 |
| 자기지정 랜드마크 | 높음 | 중간: 여러 전략 중 하나 | 저장·선택 입력 | 랜드마크 가정을 먼저 두게 됨 |
| 피드포워드 shoreline | 중간 | 높음 | 경계 선분·접선·연결 | 스위프 geometry를 재사용 가능 |
| 교차로 허브 | 중하 | 높음 | walkable portal 추출 | 구조 추론이 먼저 필요 |
| 관계 리플레이 | 낮음 | 높음 | 관찰 모델·인지 그래프 | 앞선 모듈 데이터가 필요 |

### 선정 이유

- 목적지와 랜드마크를 요구하지 않는다.
- 사용자가 현재 위치에서 능동적으로 공간을 샘플링한다.
- “정보 접근 범위가 병목인가”를 가장 직접적으로 조작한다.
- 기존 player 위치·heading·semantic map·3D audio·logger를 재사용한다.
- 이후 shoreline의 경계 교차 계산과 입력 구조를 재사용할 수 있다.
- OFF일 때 기존 baseline과 명확히 비교할 수 있다.

### 아직 모르는 것

- semantic polygon만으로 장애물 hit까지 표현할 수 있는가
- 연속 sweep이 공간 단면으로 들리는가, 개별 click으로만 들리는가
- 사용자가 좌우 센터링만 배우고 공간 형태는 학습하지 않는가
- 음향 거리와 실제 거리의 대응이 충분한가
- 햅틱 장치 없이 audio-only 결과가 어떠한가

이 불확실성들이 바로 첫 프로토타입이 답할 질문이다.

## 7. 첫 기능의 최소 범위

### v0 질문

> 현재 위치 중심의 수동 좌우 스위프로 얻은 첫 semantic 경계 hit가, 기존 최근접 경계 알림보다 통로 폭·개구부·경계 형태 재구성을 돕는가?

### 포함

- 단일 수동 레이
- 사용자 위치 원점
- player heading 기준 상대각
- 최대 거리 한 개
- 첫 semantic transition 또는 첫 경계 hit
- hit 지점의 짧은 월드 고정 공간음
- module on/off
- profile 적용
- 스위프 입력과 hit 로그
- development build의 선택적 ray debug 표시

### 제외

- 햅틱 실제 출력
- semantic 종류별 음색
- 다중 hit와 거리층
- 자동 왕복
- 장애물 mesh와 semantic boundary의 통합
- 교차로 인식
- landmark 저장
- 인지 그래프

햅틱은 v0 detection이 안정된 뒤 별도 renderer 또는 후속 module로 연결한다. 하드웨어가 정해지지 않은 상태에서 특정 진동 API를 첫 구현에 결합하지 않는다.

### 입력 추상화

기능 코드가 특정 키를 직접 읽지 않게 다음 action을 정의한다.

- `ProbeActive`: 누르고 있는 동안 probe 활성
- `ProbeSweep`: `-1..+1` 연속 좌우 입력
- `ProbeReset`: 중앙으로 복귀

키보드·마우스와 gamepad는 action binding에서 연결한다. 이후 햅틱 controller를 사용해도 module 코드를 바꾸지 않는다.

### 감지 구현

첫 단계는 semantic ray marching이다.

1. 현재 위치의 semantic을 구한다.
2. 상대 sweep 각도를 player heading에 더한다.
3. 레이 위를 작은 step으로 샘플링한다.
4. semantic 또는 feature ID가 처음 변하는 지점을 찾는다.
5. 이분 탐색으로 경계 거리를 보정한다.
6. `SweepProbeHit`을 반환한다.

초기 step 후보는 0.25–0.5 m지만, 이는 검증된 인간 지각값이 아니라 계산 성능과 오차를 확인하기 위한 공학적 시작값이다.

`Physics.Raycast`는 collider 존재와 obstacle semantic이 확인된 후 별도 hit provider로 추가한다. v0의 결과를 “모든 장애물 탐지”로 표현하지 않는다.

### 권장 코드 단위

| 코드 | 책임 |
| --- | --- |
| `SemanticRayProbe` | 순수 계산으로 첫 semantic hit 반환 |
| `SweepProbeHit` | 각도·거리·월드점·feature 정보 |
| `EgocentricSweepFeedback` | 입력, 계산 호출, 공간음, 로그 |

`SemanticRayProbe`는 `MonoBehaviour`와 오디오에 의존하지 않게 만들어 EditMode test가 가능하도록 한다.

## 8. 첫 브랜치별 작업

### Branch 0 — `infra/feedback-experiments`

#### 구현

- `FeedbackModule`에 안정된 module ID와 명시적 toggle lifecycle 추가
- `FeedbackCoordinator`에 ID registry, 상태 조회, 개별 toggle API 추가
- module 상태가 바뀔 때 `feedback_toggle` event log 추가
- 기존 `SemanticAudioGuide`에 ID를 부여하고 기본 ON 유지

#### 테스트

- 중복 module ID를 거부함
- 알려진 module을 ID로 조회하고 켜고 끌 수 있음
- 같은 상태를 두 번 적용해도 중복 상태 변경이 없음
- module OFF 시 update와 coordinator event 전달이 발생하지 않음
- toggle event에 module ID, old/new state, reason이 기록됨
- 알려지지 않은 ID 요청이 false를 반환하고 다른 module에 영향을 주지 않음

#### 병합 게이트

- 기존 EditMode tests 통과
- 기본 상태에서 현재 음향 동작 유지
- 기존 semantic module을 OFF 후 다시 ON할 수 있음
- 상태 변경이 session log에 남음
- 새 인지 피드백 기능은 포함하지 않음

### Branch 1 — `feat/fb-01-egocentric-sweep`

#### 구현

- `SemanticRayProbe`
- `SweepProbeHit`
- `EgocentricSweepFeedback`
- 스위프 input actions
- ray/hit debug 표시
- event 기반 sweep log

#### 자동 테스트

- 직선 레이가 첫 semantic transition을 반환
- miss가 명시적으로 반환됨
- 최대 거리 밖의 경계는 제외됨
- player heading + 상대각 변환이 정확함
- coarse step 뒤 보정된 hit 오차가 허용 범위 안임
- module OFF이면 ray query와 음향 재생이 없음
- sweep toggle이 다른 module 상태를 바꾸지 않음

#### 수동 기술 테스트

- 좌우 움직임과 음원 방향이 일치
- 머리를 돌려도 hit 음원이 월드 위치에 남음
- sweep 정지 시 음원이 즉시 중단 또는 정리됨
- hit 없음과 로그 누락이 구분됨
- 장시간 사용 시 log 폭증이나 GC spike가 없음

#### 병합 게이트

- module 기본 OFF
- 기존 semantic module을 끈 상태가 기존 module OFF 기준선과 동일
- sweep-only profile이 독립 실행됨
- 계산·입력·음향·로그 각각을 끊어 재현 가능
- 알려진 제한을 문서화

## 9. 첫 자체 실험

### EXP-SWEEP-00 — 계산과 표현 검증

- 질문: ray hit 참값과 들리는 위치가 일치하는가
- 환경: 합성 직선 경계, 좌·우 경계, miss
- 결과: hit 거리오차, 방위오차, event 누락
- 통과 전에는 인지 실험을 하지 않음

### EXP-SWEEP-01 — 국소 형태 인지

- 질문: 스위프가 통로 횡단면과 개구부를 구별하게 하는가
- 조건 A: 기존 최근접 semantic feedback
- 조건 B: sweep-only
- 공간: 같은 크기의 직선, 한쪽 확장, 좌측 개구부, 우측 개구부, T 구조
- 탐색: 목적지 없이 배치를 이해할 때까지 자유 탐색
- 결과: 폭 순위, 개구부 방향, 분기 수, 형태 재구성
- 부차 결과: 탐색시간, sweep 수, 이동거리, 확신도
- 전이: feedback을 끄고 시작 heading을 바꾼 뒤 다시 관계를 답함

연구자 1인 결과는 구현 진행 게이트로만 사용한다. 시각장애인 사용성이나 인지맵 효과를 일반화하지 않는다.

## 10. 기능이 늘어난 뒤의 테스트 전략

모듈 수가 `N`이면 가능한 on/off 조합은 `2^N`이다. 코드 호환성 시험과 인간 인지 실험을 구분한다.

### 자동 compatibility matrix

구현된 모든 조합을 가능한 한 자동으로 실행한다.

- profile 적용 성공
- 중복 AudioListener·입력 충돌 없음
- module ID와 상태 일치
- 활성화·비활성화 중 예외 없음
- session log에 정확한 조합 기록
- 종료 시 음원·event subscription 정리

이 검사는 `2^N` 전체 조합을 대상으로 할 수 있다.

### 인간 자체 실험

모든 조합을 처음부터 동일 횟수로 수행하면 학습효과와 피로가 커진다. 다음 순서로 확장한다.

1. 각 module 단독 대 baseline
2. 통과한 module의 쌍별 조합
3. 현재 best 조합
4. best 조합에서 하나씩 제거하는 leave-one-out ablation
5. 상호작용이 의심되는 조합의 추가 반복
6. 필요할 때 고정된 소수 module에 대해 전체 factorial

“결국 모두 시험한다”는 목표는 유지하되, 효과가 없거나 인지가 불가능한 module까지 모든 인간 조합에 계속 넣지는 않는다. 탈락 근거와 profile은 보존한다.

### 조합 ID

안정된 module 순서를 정한 후 bitmask를 보조 ID로 사용할 수 있다. 예를 들어 `sweep=1`, `flow=2`, `junction=4`이면 `C005`는 sweep+junction이다. 분석에서는 숫자만 쓰지 않고 활성 module ID 목록도 함께 보존한다.

## 11. 기능 추가 권장 순서

| 단계 | 기능 | 앞 단계에서 재사용 | 판정 질문 |
| --- | --- | --- | --- |
| F0 | 실험 harness | 현재 coordinator/logger | 독립 on/off와 재현이 가능한가 |
| F1 | 좌우 스위프 | map, heading, audio | 국소 단면을 형태로 통합하는가 |
| F2 | 경계 흐름/shoreline | ray·경계 계산 | 점 hit를 연속 경계로 이해하는가 |
| F3 | 교차로 위상 | 경계·개구부 | 분기 관계를 재구성하는가 |
| F4 | 환경중심 기준축 | heading·audio | 회전 후 공간 업데이트가 나아지는가 |
| F5 | 자기지정 랜드마크 | module/profile/input | 선택적 외부 기준점이 추가 이득을 주는가 |
| F6 | 관계 리플레이 | 앞선 observation logs | 시간적으로 흩어진 정보를 통합하는가 |

F2 이후에는 결과에 따라 순서를 바꾼다. 예를 들어 F1에서 방향 혼동이 주된 실패라면 F4 기준축을 F2보다 먼저 시험한다.

## 12. 진행 의사결정

각 기능은 다음 중 하나로 끝난다.

- `KEEP`: 단독 기능이 기술적으로 안정적이고 인지 가능함
- `REVISE`: 기제는 가능하지만 표현 또는 입력을 수정해야 함
- `COMBINE`: 단독 효과는 약하지만 특정 module과 함께 시험할 이유가 있음
- `PARK`: 현재 환경·장치에서는 판단 불가
- `DROP`: 반복해도 목표 정보를 전달하지 못함

결정은 branch 삭제 여부와 별개다. 실패한 구현도 tag 또는 commit hash, profile, experiment log로 추적 가능해야 한다.

## 13. 지금 바로 할 수 있는 작업

사용자 승인 후 다음 실제 작업은 `infra/feedback-experiments` 브랜치 생성이다. 그 전에 현재 문서 변경을 어떻게 보존할지만 확정하면 된다.

첫 구현 세션의 완료 정의는 다음과 같다.

1. coordinator API로 기존 semantic module을 ON/OFF할 수 있다.
2. 상태가 CSV에 기록된다.
3. OFF 시 기존 음향이 완전히 멈춘다.
4. 다시 ON하면 정상 동작한다.
5. 알려지지 않은 module ID가 안전하게 거부된다.
6. 관련 EditMode tests가 통과한다.

이 최소 기반이 병합되면 두 번째 module을 추가하고 coordinator API로 각각 또는 함께 켤 수 있다. 그때 실제 반복 작업이 드러난 만큼만 profile layer를 추가한다. 좌우 스위프 v0는 현재 코드와 데이터만으로 시작할 수 있다. 햅틱 실제 출력과 일반 장애물 탐지는 추가 장치·collider 검증이 필요하지만, semantic 경계에 대한 audio-only 스위프는 즉시 구현 가능하다.
