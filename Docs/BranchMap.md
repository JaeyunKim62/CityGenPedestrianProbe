# 브랜치 현황과 Android 작업

기준: 2026-08-26. 브랜치 규칙의 근거는 [ModularImplementationAndExperimentPlan.md](ModularImplementationAndExperimentPlan.md) 3절. 층 구조는 [ImplementationAxes.md](ImplementationAxes.md).

## 1. 브랜치

8월 24일 문서에 있던 일렬 스택(`infra/feedback-experiments` → `infra/feedback-signal-bus` → `feat/fb-01-egocentric-sweep` → `infra/input-router` → `infra/android-input`)은 전부 `main`에 들어갔고 브랜치는 삭제됐다.

```
origin/main
  └─ main
       └─ infra/heading-model   (+11, 미푸시)
```

## 2. `infra/heading-model`에 담긴 것

| | |
| --- | --- |
| `3ffe6eb` | `ProbeHeading` — 몸 방위와 주의 방향을 각각 이름 붙임. **CSV 스키마 경계** |
| `e7ec601` | `ImplementationAxes.md` |
| `6062a12` | 설정 메뉴를 `BlindModeOverlay`에서 분리 |
| `15caad7` | 발소리 (`locomotion.step`, `SignalChannel.Locomotion`) |
| `588762f` | 방위 중심 이동 프레임 (`MovementFrame`) |
| `9643180` | 기울기 조향 — 롤 → 절대 방위 |
| `81cdecd` | 릴리스에서 설정 메뉴가 안 눌리던 것 수정 |
| `b68edb1` | 홀드 전진 + 개발 빌드 서명 수정 |
| `e3fe843` | 발소리 진동 (`HapticSignalRenderer`) |
| — | 실험자용 빌드 (`PROBE_EXPERIMENTER`) + 릴리스 스크립트 수정 |
| — | 이 문서를 포함한 문서 갱신 |

**미커밋** — 이 작업과 무관하게 세션 전부터 dirty였던 `ProjectSettings.asset`(빌드마다 버전 코드가 오른다)과 `Assets/Resources.meta`.

`3ffe6eb`이 세션 CSV 컬럼을 둘 늘렸다(`gaze_bearing_deg`, `gaze_offset_deg`). 이 커밋 이전과 이후 파일을 섞어 읽을 때의 경계다. 컬럼은 맨 뒤에 붙였으므로 위치로 읽는 코드는 양쪽에서 동작한다.

## 3. PC용과 모바일용을 나누지 않는다

두 갈래를 만들면 관리가 갈라지고 테스트가 반쪽이 된다. 하나로 가고, 어쩔 수 없는 것만 런타임에 조정한다.

| 축 | 어쩔 수 없나 | 처리 |
| --- | --- | --- |
| 컨트롤 | 예 | 라우터가 플랫폼이 아니라 **소스 존재 여부**로 병합. 코드 분기 없음 |
| 화면 절전·방향 | 예 | `SystemInfo.deviceType` 판정 두 줄 |
| 메모리·맵 | 예 | 실행 씬을 런타임이 쓰는 것만으로 줄인다 — **양쪽 동일** |
| 어떤 씬을 도는가 | 아니오 | 실험 자극 축이다 |
| 렌더링 on/off | 아니오 | 블라인드 조건 축이다 |
| APK 크기·텍스처 | 아니오 | 빌드 패키징. 플랫폼과 무관한 군살 |

**판정은 컴파일러가 아니라 기기에게 묻는다.** `SystemInfo.deviceType == DeviceType.Handheld`이지 `#if UNITY_ANDROID`가 아니다. EditMode 테스트가 전부 PC에서 도는데 컴파일 분기는 PC에서 검증할 수 없는 코드를 만든다.

**예외가 하나 생겼다.** 진동은 `#if UNITY_ANDROID && !UNITY_EDITOR`로 갈랐다. JNI 호출이라 다른 플랫폼에서는 컴파일 자체가 안 되고, 런타임 판정으로는 피할 수 없다.

## 4. 빌드 세 종류

| | 정의 | 산출물 | 용도 |
| --- | --- | --- | --- |
| 개발 | `DEVELOPMENT_BUILD` + `PROBE_DEMO` | APK ~500MB | 케이블로 25초. 실기 확인은 전부 여기서 |
| 참가자 | 없음 | AAB ~38MB | 검은 화면만. 내부 테스트 배포 |
| 실험자 | `PROBE_EXPERIMENTER` | AAB ~38MB | 참가자용 + 맵·HUD·블라인드 해제 |

가드가 8월 25일까지 "개발 빌드인가"와 "참가자에게 건네는 빌드인가"를 한 단어로 묶고 있었다. 내부 테스트로 배포하면서 **서명된 빌드에 맵을 넣을 방법이 없다는 것**이 드러나 `PROBE_EXPERIMENTER`로 갈랐다. 기본값은 참가자용이다.

**릴리스는 약 2시간 걸린다.** CityGen Resources 678MB를 뺐다 되돌리며 에셋 재임포트가 두 번 돌기 때문이고, 개발 APK가 증분 2~15분인 것과 대비된다.

## 5. 실측값

Galaxy SM-S948N, Android 16 (API 36), Vulkan, arm64-v8a.

| | |
| --- | --- |
| APK (개발) | 498~520MB |
| AAB (릴리스) | 38MB |
| 텍스처 | 678.8MB, 사용자 자산의 **98.4%** — 개발 APK에만 들어감 |
| 오디오 | `audio_hz=24000` vs `clip_hz=44100`, dsp 512, Stereo |
| 세션 CSV | 51.6초에 353KB |

`SignalClipFactory`가 44100을 가정하는데 기기는 24kHz로 믹싱한다. 루프 이음매가 사이클에 떨어지지 않아 주기적 클릭이 생길 조건이지만 실기 청취에서 보고되지 않았다. 우선순위 낮음.

## 6. 검증 현황

| 단계 | 확인 | 결과 |
| --- | --- | --- |
| A | `[PedestrianProbe]` 상태가 초당 1회 | ✅ |
| B | 지속음 재생, 클릭 없음 | ✅ 청취 확인 |
| C | SYN01 좌/우 2.00m가 PC와 일치 | ✅ |
| D | SYN03/04 좌우 반전 검출 | 미실시 (씬 전환은 이제 됨) |
| E | 눈 감고 SYN01→SYN05 단독 진행 | 미실시 |
| F | seoul 맵 10Hz 유지, 30분 세션 | 미실시 |
| G | 설정 메뉴 터치 | ✅ (릴리스에서 안 되던 것 수정 후) |
| H | 기울기 조향 부호 — 반시계 90° → 서 | ✅ 실기 확인 |
| I | 눕히면 방위 동결 + 경고 | ✅ |
| J | 홀드 전진 | ✅ |
| K | 발소리 진동 | ✅ |
| L | 실험자용 AAB | ✅ 38.2MB, 버전 코드 6, 실기 확인 |

EditMode 191/191.

## 7. 결정과 부채

**기울기 조향에 상태가 없다.** 중력의 화면평면 성분이 매 프레임 절대 측정이므로 원시 롤 각도가 곧 방위다. 캘리브레이션도, 언랩도, 앵커도 없다. 눕혔다 세워도 재보정 없이 맞는다. 유일한 상태는 중력 벡터에 거는 필터다.

**절대 지시가 델타를 이긴다.** `ProbeInputFrame.HeadingAbsolute`는 스위프 각도와 같은 규칙을 따른다. 그래서 폰과 엄지가 같은 몸을 두고 싸우지 않고, **모드 3분기 없이도** 기울기 조향이 성립한다. 모드 배타 선택은 조건 기록이 필요해질 때 하면 된다.

**해소된 부채 — `BlindModeOverlay`.** 502줄에서 361줄로 줄었고 설정 메뉴가 `ProbeSettingsMenu`로 나갔다. 아직 blind 상태·키보드 입력·HUD·미니맵 넷을 겸한다.

**참가자가 조건을 바꿀 수 있다.** 릴리스 메뉴의 `WALK`와 `TILT STEERING`은 참가자도 누를 수 있다. 블라인드 해제만 컴파일 제외되어 있다. 현재 운용(실험자가 세팅 후 건넴)에서는 문제없다고 판단했다. 참가자 단독 시행을 시작하면 다시 봐야 한다.

**알려진 불일치 (미해결).** `SyntheticTestSceneBuilder`는 카탈로그 경로로 C002를 하드코딩하는데 커밋된 씬은 C101을 참조한다. 지금 "Build Synthetic Scenes"를 다시 돌리면 자극이 조용히 바뀐다.

**저장소 위생.** `Assets/Resources.meta`가 미추적으로 남아 있다.

## 8. 다음

| | 내용 |
| --- | --- |
| 1 | `-KeepVersionCode`가 실효하는지 확인. 지금까지 두 번 다 올랐다(4→5→6). 로그에 `Version code N`과 `Keeping version code N`이 갈라져 찍히므로 이제 한 줄로 판정된다 |
| 2 | 소리 세트 전환을 폰 메뉴에 노출 — 지금은 PC `V`키뿐이라 팀원이 카탈로그를 비교할 수 없다 |
| 3 | 검증 D·E — 씬 전환이 되므로 이제 실시 가능 |
| 4 | 좌우 스와이프 (모드 3의 미구현 제스처) |
| 5 | 빌드에서 `CityGen3D/Resources` 군살 제거 — 개발 APK 500MB를 줄이면 실기 반복이 빨라진다 |
| 6 | seoul 실행 씬 → 검증 F |
