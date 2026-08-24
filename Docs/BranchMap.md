# 브랜치 현황과 Android 작업 계획

기준: 2026-08-21. 브랜치 규칙의 근거는 [ModularImplementationAndExperimentPlan.md](ModularImplementationAndExperimentPlan.md) 3절.

## 1. 현재 브랜치

미병합 브랜치 셋은 **병렬이 아니라 일렬 스택**이다. 뒤엣것이 앞엣것을 전부 포함한다.

```
origin/main
   └─ main                          (+9, 미푸시)
        └─ infra/feedback-experiments   (+1)   73cae45
             └─ infra/feedback-signal-bus (+10) a59a9ff
                  └─ feat/fb-01-egocentric-sweep (+22) ← 현재
```

| 브랜치 | main 기준 | 상태 | 내용 |
| --- | ---: | --- | --- |
| `main` | 0 | origin보다 9 앞섬, 미푸시 | 통합 기준선 |
| `infra/feedback-experiments` | +1 | 미병합 | module ID·토글·registry |
| `infra/feedback-signal-bus` | +10 | 미병합 | signal intent·bus·catalog·audio renderer |
| `feat/fb-01-egocentric-sweep` | +22 | 미병합 | 자기중심 스위프 v0, 합성 테스트베드 |

main 병합은 **나중에** 한다. 지금은 fb-01 위에서 계속 간다.

## 2. 이번에 만들 것

```
git checkout -b infra/android-input feat/fb-01-egocentric-sweep
```

`main`이 아니라 **fb-01에서** 딴다. 근거 두 가지.

- 검증 자극인 합성 테스트베드(`Assets/Testbed`)가 fb-01에만 있다.
- `SemanticPedestrianController`와 `BlindModeOverlay`를 fb-01이 이미 수정했다. main에서 따면 나중에 충돌한다.

fb-01이 main에 들어갈 때 rebase 한 번. 20줄이라 비용은 없다.

## 3. 커밋 계획

| # | 내용 | 성격 |
| ---: | --- | --- |
| 1 | seam 3개 (아래 4절) | **동작 변화 0**, 씬 diff 0, EditMode 94/94 |
| 2 | 플랫폼 위생 + 기기정보 로그 | 새 파일만 |
| 3+ | 입력 라우터 / 터치 / 확인음 / 씬 전환 | 새 파일만 |

커밋 1을 반드시 분리한다. Android와 무관한 순수 seam이라 단독으로 `main`에 갈 자격이 있고, fb-02가 Android보다 먼저 준비되면 이 커밋만 cherry-pick해서 그 위에서 시작할 수 있다. 뭉쳐두면 불가능하다.

## 4. 기존 파일 수정 (총 20줄 미만, 전부 덧붙이기)

| 파일 | 추가 | 기존 영향 |
| --- | --- | --- |
| `SemanticPedestrianController` | `SetMoveSource(IPedestrianMoveSource)` | 없음. null이 기본이라 기존 분기 그대로. 직렬화 필드가 아니라 **씬 파일이 안 바뀐다** |
| `BlindModeOverlay` | `SetBlind(bool)` | 없음. 호출 전까지 무영향 |
| `BlindModeOverlay` | `sourceClassView = map.SourceClasses.Count > 0` | 도시 씬 없음 (Seoul 26 / NYC 28 → 그대로 `true`). 합성 맵만 `false`로 바뀌며 이는 의도한 개선 |

`SetBlind`의 가드 위치가 유일한 설계 결정이다.

- `SetBlind` 자체 — **가드 없음**. 배포 빌드에서 trial 스크립트가 blind를 *거는* 건 정상이고 필요하다.
- blind를 *푸는* 입력 경로 — `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. 참가자가 조건을 벗어나는 것만 컴파일 타임에 막는다.

이렇게 두면 기존 가드의 실험 무결성이 그대로 유지되면서 정책과 기전이 갈라진다.

## 5. 결정 사항

**리팩터는 하지 않는다.** 넷 다 덧붙이기다. blind 상태를 `BlindModeOverlay` 밖으로 옮기는 건 진짜 리팩터이고, Android 검증과 섞이면 실패 원인을 이식과 리팩터로 나눌 수 없게 된다. 부채로만 기록한다.

**부채 기록 — blind 상태 소유.** `BlindModeOverlay`가 blind 상태·입력·HUD·미니맵 넷을 겸한다. blind는 오버레이의 관심사가 아니라 실험 조건이다(파급이 이미 `PedestrianProbeRuntime`의 로거와 `SetLookEnabled`까지 나간다). Android가 드러냈을 뿐 PC에서도 언젠가 물릴 문제다.

**아직 안 하는 것.** `#if DEVELOPMENT_BUILD` 가드 제거 / `CityGen3D/Resources` 제외 빌드 훅 / 자이로 heading / `HapticSignalRenderer` / seoul 경량 씬 / `SignalClipFactory` 샘플레이트(실기에서 클릭이 들릴 때만).

**미결 — 라우터 착수 전에 정할 것.**

1. 스위프 각도를 절대 매핑(터치·스틱)으로 갈지 rate(키보드 Q/E)로 갈지. 실험 변수이므로 어느 쪽이 쓰였는지 CSV에 남겨야 한다.
2. blind 중 좌우 입력을 몸 회전으로 볼지. 회전으로 두면 `3e9eb55`의 look lock을 "pitch만 잠금"으로 재정의해야 하고 `PedestrianLookLockTests`도 같이 고쳐야 한다.

## 6. 검증 순서

| 단계 | 확인 | 필요한 코드 |
| --- | --- | --- |
| A | `adb logcat`에 `[PedestrianProbe]` 상태가 초당 1회 | 없음 |
| B | miss 지속음이 들리고 0.5초 주기 클릭이 없음 | 커밋 2 |
| C | BT 키보드로 SYN01 좌/우 `2.00m`, PC 로그와 일치 | 없음 |
| D | SYN03 좌=miss·우=2.00, SYN04는 반대 (좌우 반전 검출) | 없음 |
| E | 눈 감고 SYN01→SYN05 단독 진행 | 커밋 3+ |
| F | seoul 맵 10Hz 질의 유지, 30분 세션 CSV가 `session_end`로 닫힘 | 별도 |

A와 C는 코드 없이 통과해야 한다. "이식은 정확하다"가 확정된 뒤에 라우터를 시작해야 이후 문제가 전부 매핑 문제로 좁혀진다.
