# 브랜치 현황과 Android 작업

기준: 2026-08-24. 브랜치 규칙의 근거는 [ModularImplementationAndExperimentPlan.md](ModularImplementationAndExperimentPlan.md) 3절.

## 1. 브랜치

미병합 브랜치는 **병렬이 아니라 일렬 스택**이다. 뒤엣것이 앞엣것을 전부 포함하므로, 병합할 때 맨 뒤 하나만 올리면 앞의 것이 함께 정리된다.

```
origin/main
  └─ main                              (+9, 미푸시)
       └─ infra/feedback-experiments   (+1)
            └─ infra/feedback-signal-bus (+10)
                 └─ feat/fb-01-egocentric-sweep (+22)
                      └─ infra/input-router      (+4)  ← 플랫폼 무관, main 후보
                           └─ infra/android-input (+3)  ← Android 전용
```

`main` 병합은 나중에 한다.

## 2. 커밋된 것

**`infra/input-router`** — PC에서도 유효한 작업만. 단독으로 `main`에 갈 자격이 있다.

| | |
| --- | --- |
| `f1a8f8b` | 이동·회전 입력을 외부에서 주입 (`SetMoveSource`) |
| `ed366b8` | `SetBlind`, 맵에서 유도하는 미니맵 기본값, 화면 터치 설정 메뉴 |
| `134f253` | `ProbeInputFrame` / `IProbeInputSource` / `ProbeInputRouter` |
| `feef9a4` | 터치 제스처 + EditMode 16개 |

**`infra/android-input`** — Android 전용

| | |
| --- | --- |
| `1b88c5b` | 핸드헬드 세션 설정 + 기기·오디오 정보 CSV 행 |
| `0aecc9c` | `AutoSweepDemo` (PROBE_DEMO 빌드 한정) |
| `d4d6f49` | APK 빌드 스크립트, App ID, minSdk 26 |

**커밋하지 않은 것**: `.vscode/settings.json`, `Assets/Settings/*.asset` 3개. 세션 이전부터 dirty였고 이 작업과 무관하다.

## 3. PC용과 모바일용을 어떻게 나누는가

**브랜치로도 `#if`로도 나누지 않는다. 씬과 빌드 레시피로 나눈다.**

- 브랜치로 나누면 실험 코드가 갈라지고, 두 플랫폼에서 나온 결과를 비교할 수 없게 된다. 연구에서는 치명적이다.
- `#if UNITY_ANDROID`로 나누면 PC 에디터에서 돌 수 없는 코드가 생긴다. EditMode 테스트 전부가 PC에서 도는데, 검증 불가능한 분기를 만드는 셈이다.

갈리는 지점은 셋뿐이고 전부 한곳에 모여 있어야 한다.

| 갈리는 곳 | 내용 | 사는 곳 |
| --- | --- | --- |
| 빌드 레시피 | 대상 플랫폼, 씬 목록, 스크립팅 정의, App ID | `Assets/Editor/Build/` (에디터 코드) |
| 씬 | PC는 도시 전체(시각 디버깅), 모바일은 합성 테스트베드 → 나중에 시맨틱 전용 도시 | 자산 |
| 런타임 능력 판정 | 절전·프레임 상한·화면 방향·입력 소스 | `ProbeSessionPlatform` 한곳 |

**판정은 컴파일러가 아니라 기기에게 묻는다.** `SystemInfo.deviceType == DeviceType.Handheld`이지 `#if UNITY_ANDROID`가 아니다. 그래야 한 빌드가 어디서 돌든 맞게 행동하고, 데스크톱이 폰과 같은 코드를 계속 돌린다.

`ProbeInputRouter`가 이미 이 모양이다. 플랫폼이 아니라 **소스가 존재하는지**로 병합하므로, 폰에 블루투스 키보드를 꽂으면 키보드가 그대로 살아나고 터치와 함께 쓰인다. 브링업 중에 실험자가 키보드로 조건을 토글하면서 참가자가 엄지로 조준하는 상황이 코드 변경 없이 성립한다.

이 원칙을 세우면서 회귀를 하나 잡았다. `Screen.sleepTimeout`과 `targetFrameRate = 30`이 플랫폼 구분 없이 걸려서 **에디터 Play까지 30fps로 묶고 있었다.** 핸드헬드에서만 걸도록 고쳤다.

## 4. 실측값

Galaxy SM-S948N, Android 16 (API 36), Vulkan, arm64-v8a.

| | |
| --- | --- |
| APK | 476MB (터치 추가 후 497MB) |
| 텍스처 | 678.8MB, 사용자 자산의 **98.4%** |
| 씬 5개 | 9.3KB |
| 원인 | `Assets/CityGen3D/Resources/`의 Building LOD 프리팹 → 파사드 머티리얼 → 파사드 텍스처 152장 |
| 오디오 | `audio_hz=24000` vs `clip_hz=44100`, dsp 512, Stereo |
| 세션 CSV | 51.6초에 353KB (sample 449 / event 1042) |

`SignalClipFactory`가 44100을 가정하는데 기기는 24kHz로 믹싱한다. 루프 이음매가 사이클에 떨어지지 않으므로 주기적 클릭이 생길 조건이지만, 실기 청취에서는 문제가 보고되지 않았다. 우선순위는 낮다.

## 5. 검증 현황

| 단계 | 확인 | 결과 |
| --- | --- | --- |
| A | `[PedestrianProbe]` 상태가 초당 1회 | ✅ |
| B | 지속음 재생, 클릭 없음 | ✅ 청취 확인 |
| C | SYN01 좌/우 2.00m가 PC와 일치 | ✅ `edge=2.00m`, `+86.2° → 2.00m`, `+55.9° → 2.41m` (2.0/sin55.9°) |
| D | SYN03/04 좌우 반전 검출 | 미실시 (씬 전환 미구현) |
| E | 눈 감고 SYN01→SYN05 단독 진행 | 미실시 |
| F | seoul 맵 10Hz 유지, 30분 세션 | 미실시 |

## 6. 결정과 부채

**해결된 미결 두 가지.**

1. 스위프 각도는 절대와 rate를 **둘 다** 프레임에 싣고 라우터가 절대를 우선한다. 어느 쪽이 세션을 몰았는지는 로그로 복구할 수 있어야 한다.
2. look lock을 "pitch만 잠금"으로 재정의했다. `lookEnabled` → `pitchEnabled`가 되고 yaw는 무조건 적용되므로, 블라인드 중에도 이동 입력이 몸을 돌린다. 스위프가 유일한 조준 수단이라는 원 의도는 유지된다.

**부채 — `BlindModeOverlay`의 책임 과다.** blind 상태·키보드 입력·HUD·미니맵·터치 메뉴 다섯을 겸한다. blind는 오버레이의 관심사가 아니라 실험 조건이며, 파급이 이미 `PedestrianProbeRuntime`의 로거와 컨트롤러까지 나간다. 터치 메뉴가 들어가며 더 무거워졌다. 지금 옮기지 않는 이유는 Android 검증과 섞이면 실패 원인을 이식과 리팩터로 나눌 수 없기 때문이다.

**계획에서 어긋난 것.** "seam은 20줄 미만, 동작 변화 0"을 지킨 것은 `f1a8f8b` 하나뿐이다(`SemanticPedestrianController` +35 −4). `BlindModeOverlay`는 터치 메뉴 때문에 +200 −10이 되었고 동작 변화를 포함한다. 무변화 cherry-pick 대상은 `f1a8f8b`뿐이다.

**아직 안 하는 것.** `#if DEVELOPMENT_BUILD` 가드 제거 / 자이로 heading / `HapticSignalRenderer` / seoul 경량 씬 / `SignalClipFactory` 샘플레이트.

**알려진 불일치.** `SyntheticTestSceneBuilder`는 카탈로그 경로로 C002를 하드코딩하는데, 커밋된 테스트베드 씬은 C101_PULSE_CATEGORY r2를 참조한다. 지금 "Build Synthetic Scenes"를 다시 돌리면 자극이 조용히 바뀐다.

**저장소 위생.** `Assets/Testbed.meta`가 미추적으로 남아 있다. `Assets/Testbed/` 내용은 `c96fe65`에서 커밋됐는데 폴더 meta만 빠졌다. fb-01 쪽에서 정리할 일이다.

## 7. 다음

| | 내용 |
| --- | --- |
| 1 | 터치 조작 실기 확인 — 엄지로 조준·보행이 되는지 |
| 2 | 씬 전환 구현 → 검증 D·E |
| 3 | 슬림 빌드 — 빌드 중 `CityGen3D/Resources`를 치웠다 되돌려 497MB를 50MB 안팎으로 |
| 4 | 시맨틱 전용 seoul 씬 → 검증 F |
