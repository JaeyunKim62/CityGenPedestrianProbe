# CityGen Pedestrian Probe

CityGen3D로 생성한 실제 도시 공간을 보행자 관점의 semantic 공간으로 변환하고, 비시각 보행 실험에서 위치 판정·음향 안내·행동 기록을 수행하기 위한 Unity 연구 프로젝트입니다.

## 프로젝트 목표

최종 목표는 도시의 물리적 형상과 OpenStreetMap 정보를 보행자가 인식할 수 있는 공간 의미로 변환하는 것입니다. 단순히 도로와 건물의 시각적 메시를 표시하는 데 그치지 않고, 현재 위치가 보도·연석·횡단보도·차도·광장·녹지·건물 진입부 중 어디인지 질의할 수 있는 공간 데이터로 만드는 것을 목표로 합니다.

이 semantic 공간을 이용해 다음 기능을 구축합니다.

- 보행자의 현재 공간과 주변 경계 판정
- 공간 접근·진입·이탈에 대한 비시각적 음향 안내
- 위치, 방향, 공간 변화와 사용자 행동 기록
- 원본 지도 클래스와 연구용 semantic 사이의 추적 가능한 변환
- 실제 보행 실험을 위한 반복 가능한 도시 데이터셋과 평가 환경

## 현재 구현 상태

의미 공간 위에 피드백 기반이 서고, 그 위에서 모듈이 신호를 냅니다. 새 피드백 기능은 모듈에 하나 추가되고 나머지는 움직이지 않습니다.

### 의미 공간

| 기능 | 상태 | 설명 |
| --- | --- | --- |
| CityGen 원본 클래스 수집 | 완료 | Scene에서 발견한 `CityGen3D.*` 클래스를 피처에 보존 |
| 장소별 Database 및 출력 선택 | 완료 | 열린 Scene의 `CityGen3D.Data.db`를 기준으로 자동 결정 |
| 2D semantic map 베이크 | 완료 | Scene 메시를 XZ 삼각형·경계·공간 인덱스로 변환 |
| Terrain 좌표 정렬 | 완료 | 실제 Terrain 경계와 지도 좌표 오프셋 반영 |
| 런타임 위치 및 주변 질의 | 완료 | 현재 레이어와 가장 가까운 다른 피처 판정 |
| Road/Sidewalk/Curb/Crosswalk 분류 | 초기 구현 | 메시, CityGen shape와 OSM node를 조합한 1차 규칙 |
| 건물·녹지·광장 등 의미 확장 | 미구현 | 원본 클래스와 OSM tag 기반 규칙 필요 |

### 피드백 기반

| 기능 | 상태 | 설명 |
| --- | --- | --- |
| 모듈 registry와 토글 | 완료 | 안정 ID, 런타임 on/off, 토글 사건 로그 (`FeedbackCoordinator`) |
| 신호 의도와 버스 | 완료 | 모듈은 `SignalIntent`를 내고 렌더러가 받는다 (`FeedbackSignalBus`) |
| 신호 카탈로그 | 완료 | 파형·주파수·거칠기·거리별 컷오프를 asset으로. 점 단위 상위 폴백 |
| 절차적 오디오 렌더러 | 완료 | 클립을 런타임 합성, 공간화 보이스 풀, 지속음 저역 필터 |
| 촉각 렌더러 | 완료 | `HapticSignalRenderer`. 자기운동 채널만 진동으로 (`locomotion`) |

### 피드백 모듈

| 모듈 ID | 기본값 | 상태 | 설명 |
| --- | --- | --- | --- |
| `probe.egocentric-sweep` | **ON** | 연구 대상 | 자기중심 좌우 스위프. 몸 기준 각도로 경계를 훑고 거리를 답한다 |
| `semantic.audio` | OFF | 프로토타입 | 접근·진입·경계 안내. 도시에서 중첩 레이어마다 울려 스위프를 묻어서 꺼둔다 |
| `locomotion.step` | **ON** | 부가 | 걸은 거리마다 발소리와 진동. 시뮬레이션이 주지 않는 자기운동 단서 |

### 실행과 조작

| 기능 | 상태 | 설명 |
| --- | --- | --- |
| 입력 라우터 | 완료 | 소스가 프레임을 채우고 라우터가 병합. 플랫폼이 아니라 **존재 여부**로 고른다 |
| 키보드·마우스 소스 | 완료 | 이동·시선 |
| 기울기 조향 소스 | 완료 | 폰 롤을 절대 방위로. 가로가 북, 반시계 90°가 서 (`DeviceTiltInputSource`) |
| 이동 기준틀 | 완료 | 몸 기준 / 방위 기준 (`MovementFrame`) |
| 터치 소스 | 완료 | 좌반부 보행·회전, 우반부 조준·사거리, 다중 탭 명령. mm 단위라 기기 무관 |
| 블라인드 화면과 디버그 HUD | 완료 | 상태·미니맵. 디버그 요소는 개발 빌드와 실험자 빌드 한정 |
| 설정 메뉴 | 완료 | `ProbeSettingsMenu`. 씬 이동, 이동 기준틀, 기울기 조향 |
| 실험 CSV 로그 | 완료 | 위치·분류·레이어·신호·토글·기기 정보 |
| 합성 테스트베드 | 완료 | 정답을 아는 자극 7종 (`Assets/Testbed`) |
| Android 빌드 | 완료 | 테스트베드는 개발 APK·참가자 AAB·실험자 AAB 세 갈래. 도시는 개발 APK뿐 |
| 씬 전환 | 완료 | 설정 메뉴에서 자극을 바꾼다 |
| 벽 막기 | 미구현 | 이동을 막는 것은 실험 구역 사각형뿐. 경계를 통과할 수 있다 |
| 사용자 실험을 통한 신호 검증 | 미실시 | 음향 패턴과 confidence 값 검증 필요 |

`Unknown`은 반드시 데이터가 없는 상태를 뜻하지 않습니다. 원본 CityGen 피처는 존재하지만 아직 현재의 네 보행 의미 중 하나로 정의되지 않은 경우가 대부분입니다.

## 현재 데이터셋

두 곳을 베이크해 두었습니다. **NYC Doyers Chinatown이 현행**이고 실기 검증도 이쪽으로 했습니다. 서울은 bake version 3에 머물러 있어 쓰려면 다시 베이크해야 합니다.

| 항목 | NYC Doyers Chinatown | 서울시청 |
| --- | --- | --- |
| bake version | **4** | 3 (낡음) |
| 중심 Origin | 위도 `40.7150878906`, 경도 `-73.9994354248` | 위도 `37.5665016174`, 경도 `126.9779968262` |
| Terrain | `256 m × 256 m` | `256 m × 256 m` |
| 월드 경계 | X `-128..128`, Z `-128..128` | X `-128..128`, Z `-128..128` |
| Scene 컴포넌트 | 5,678개 | 1,608개 |
| 베이크 피처 | **2,095개** | 387개 |
| 베이크 삼각형 | **86,996개** | 49,909개 |
| 공간 인덱스 cell | 5 m | 5 m |

NYC의 보행 semantic 분포입니다.

| 의미 | 피처 수 | 주요 원본 |
| --- | ---: | --- |
| `Unknown` | 1,878 | Landscape, MapRoad, MapEntity, MapBuilding |
| `Road` | 173 | MapRoad |
| `Sidewalk` | 22 | Highway, Landscape |
| `Crosswalk` | 18 | OSM crossing node |
| `Curb` | 4 | Highway |

서울 기준 분포는 `Unknown` 337 / `Sidewalk` 24 / `Road` 17 / `Crosswalk` 5 / `Curb` 4입니다.

**두 도시 모두 `Unknown`이 대다수입니다.** NYC는 89.6%, 서울은 87.1%입니다. 원본 피처는 있으나 아직 네 보행 의미 중 하나로 정의되지 않았다는 뜻이고, 의미 확장이 다음 과제인 이유입니다.

0.1 m 격자로 피처 내부 중복을 제거해 분석하면, 원본 피처는 전체 실험 영역의 약 88.72%를 덮습니다. 어떤 원본 피처도 덮지 않는 영역은 약 11.28%이고, 현재 네 보행 semantic 중 하나가 정의된 합집합 영역은 약 18.82%입니다. 클래스와 semantic은 서로 겹칠 수 있으므로 개별 점유율을 단순 합산할 수 없습니다.

클래스별 컴포넌트 수, 피처 수, 삼각형 수와 누적 투영 면적의 전체 표는 [SeoulCityHall_256m_SourceClassStatistics.csv](Assets/Generated/Semantics/SeoulCityHall_256m_SourceClassStatistics.csv)에 생성됩니다. 이 CSV의 투영 면적은 피처 간 겹침을 포함합니다.

## 설계 원칙

### 기능을 늘리는 방식

**하나씩 추가합니다.** 아이디어 하나당 짧은 브랜치 하나를 쓰고, 그 기능만 켠 조건과 끈 조건을 각각 확인한 뒤 병합합니다. 조합 조건은 브랜치가 아니라 runtime profile로 만듭니다. 브랜치로 조합을 관리하면 조합 수만큼 브랜치가 늘고 실패 원인을 기능별로 나눌 수 없습니다.

**동시에 켜는 조합은 따로 정당화합니다.** 두 모듈이 함께 울리면 사용자가 두 신호 문법을 같이 배워야 하고, 그러면 공간 인지가 아니라 조작 방식을 측정하게 됩니다. 채널 간 중재는 아직 구현하지 않았으므로 겹치는 신호는 그냥 충돌합니다.

지금까지의 판단이 예입니다. `semantic.audio`는 실제 도시에서 중첩 레이어마다 울려 스위프를 묻으므로 기본 OFF로 내렸고, 스위프와 함께 켜는 profile은 만들지 않았습니다. 발소리는 함께 켜 두었는데, 자기 몸에 대한 신호라 별도 채널이고 95 Hz가 스위프의 165~660 Hz와 겹치지 않기 때문입니다. 새 모듈을 켠 채로 병합하려면 같은 수준의 근거가 필요합니다.

### 피드백

**사용자 질의를 주 정보원으로 둡니다.** 스위프가 답하는 `answer` 채널이 정보의 대부분을 차지하고, 자동으로 발화하는 `proximity`·`transition`은 최소로 유지합니다. 가설이 "능동 샘플링으로 공간 형태를 학습하는가"이므로, push 신호를 늘리면 사용자가 공간을 구성한 것인지 신호를 따라간 것인지 사후에 구분할 수 없습니다.

**지각 축을 의미마다 배타적으로 배정합니다.** 음색이 범주를, 신호 사이 간격이 거리를, 공간 위치와 광대역 어택이 방위를 담당합니다. 한 축이 두 의미를 나르면 변별이 되지 않습니다. 음량에는 정보를 싣지 않으며 `SignalIntent`에 세기 필드를 두지 않았습니다. 음향 거리 지각과 시스템 감쇠가 혼동되기 때문입니다.

**동시에 활성화되는 정보 차원을 제한합니다.** 발소리에 좌우 구분을 넣지 않은 것이 예입니다. 폰의 진동 모터는 하나이고 청각 대역은 스위프가 이미 쓰고 있습니다.

### 실험 위생

**연구 대상 모듈은 기본 ON, 그 위에 얹는 모듈은 기본 OFF로 병합합니다.** 기본값을 바꾸는 병합은 커밋 메시지에 남깁니다. 세션 로그의 해석이 달라지기 때문입니다.

**세션은 commit hash, 카탈로그 ID와 revision, map version으로 재현합니다.** CSV 매 행에 카탈로그와 revision을 적는 이유이고, 자극이 바뀌면 revision을 올립니다.

### 코드 구조

두 가지는 실험 설계가 아니라 검증 가능성 때문에 지킵니다. **판단 계산은 Unity 입력 API에서 분리해** EditMode에서 검증합니다(`BlindTouchGestures`, `RollSteering`, `StepCadence`) — 화면이 검은 상태로 폰에서 디버깅할 수 없고, 부호나 임계값 오류는 "참가자가 길을 찾지 못한다"로만 나타나 과제 난이도와 구분되지 않습니다. **분기는 플랫폼이 아니라 장치 존재 여부로** 합니다 — 테스트가 전부 PC에서 도는데 컴파일 분기는 PC에서 검증되지 않습니다.

나머지 구조(모듈과 카탈로그의 분리, 방위를 한 곳에 두는 것 등)는 [Docs/ImplementationAxes.md](Docs/ImplementationAxes.md)의 층 구조에 있습니다.

## 처리 파이프라인

```text
OpenStreetMap data
    → CityGen Database
    → CityGen Process: MapRoad / MapSurface / MapBuilding 등 2D 객체
    → CityGen Generate: Terrain / Highway / Plot / Landscape / 3D 메시
    → Semantic bake: 원본 클래스 수집, 좌표 보정, Terrain 경계 clipping
    → 1차 보행 semantic 매핑과 부족한 피처 보완
    → SemanticSpaceMap asset + 원본 클래스 통계 CSV
    → 런타임 위치 질의, 미니맵, 음향 안내, 실험 로그
```

베이커는 각 `MeshFilter`에서 부모 방향으로 가장 가까운 `CityGen3D.*` 컴포넌트를 찾아 `sourceClass`로 기록합니다. 보행 의미 매핑이 없는 피처도 버리지 않고 `Unknown`으로 보존하므로, 이후 분류 규칙을 확장해도 원본 클래스까지 추적할 수 있습니다.

CityGen의 `Map*`, `Highway`, OSM 좌표에는 Terrain 월드 원점 오프셋을 적용하고, 삼각형은 실제 Terrain 경계로 자릅니다. 런타임 질의를 위해 각 피처의 외곽 edge와 5 m 공간 인덱스를 생성합니다.

장소는 현재 열린 Scene의 `CityGen3D.Data.db` 참조로 판별합니다. 같은 Database GUID로 이미 만든 semantic asset이 있으면 그 파일을 갱신하고, 처음 보는 Database이면 `{Database 이름}_{Terrain 크기}m.asset`과 위치별 통계 CSV를 새로 만듭니다. 따라서 서울과 다른 도시의 결과를 서로 덮어쓰지 않고 함께 유지할 수 있습니다.

### 현재 보완 규칙

- Sidewalk 또는 Curb가 없으면 `Highway`의 sidewalk/curb shape를 폭이 있는 면으로 변환합니다.
- 별도 Curb가 없으면 Sidewalk 외곽을 폭 0.2 m, confidence 0.55의 Curb로 보완합니다.
- Crosswalk는 OSM의 `highway=crossing` node를 사용합니다.
- 현재 crossing 후보 31개 중 `unmarked` 1개와 Terrain 밖 25개를 제외한 5개를 베이크합니다.
- 보완 Crosswalk는 인접 road 방향을 기준으로 4 m × 10 m 사각형을 생성합니다.

## 실행

개발 환경은 Unity `6000.3.20f1`, URP `17.3.0`, Input System `1.19.0`, CityGen3D `1.20`입니다.

1. `Assets/Scenes/seoul.unity`를 엽니다.
2. CityGen의 Process와 Generate 결과가 Scene에 있는지 확인합니다.
3. `Tools > Pedestrian Probe > Bake & Setup Scene`을 실행합니다.
4. Console에서 의미별 피처 수와 원본 클래스 통계를 확인합니다.
5. Play를 실행합니다.

CityGen 데이터를 다시 Process 또는 Generate했다면 semantic map도 다시 베이크해야 합니다.

### 조작

입력 소스는 **장치가 있느냐**로 정해집니다. 플랫폼 분기가 아니므로 폰에 키보드를 연결하면 터치와 키보드가 함께 동작합니다.

#### 키보드와 마우스

| 입력 | 동작 |
| --- | --- |
| `WASD` / 방향키 | 이동 |
| `Shift` | 빠른 이동 |
| 마우스 | 시선 회전 |
| `Q` / `E` | 스위프 각도 좌 / 우 |
| `R` | 스위프 정면 복귀 |
| `[` / `]` | 사거리 − / + |
| `G` | `semantic.audio` 모듈 토글 |
| `B` / `F1` | 블라인드 화면과 디버그 화면 전환 |
| `N` | 미니맵 표시/숨김 |
| `M` | 전체 256 m / 주변 40 m 지도 전환 |
| `C` | CityGen 원본 클래스 / 보행 semantic 지도 전환 |
| `V` | 소리 세트(카탈로그) 순환 |
| `Z` / `X` / `F` | 기울기 조향 흉내 — 좌 굴림 / 우 굴림 / 눕히기 (에디터 전용) |

#### 터치

화면이 검은 상태에서 쓰이므로 고정 위치 버튼이 없습니다. 모든 제스처는 **손가락이 닿은 지점 기준**이고, 거리는 화면 DPI로 mm 환산해 기기가 달라도 같은 동작이 같은 각도를 뜻합니다.

| 영역 | 제스처 | 동작 |
| --- | --- | --- |
| 좌반부 | 위·아래 드래그 | 전진 / 후진 |
| 좌반부 | 좌·우 드래그 | 몸 회전 |
| 우반부 | 좌·우 드래그 | 스위프 각도 (절대 매핑, ±30 mm ↔ ±90°) |
| 우반부 | 위·아래 드래그 | 사거리 |
| 전체 | 세 손가락 탭 | 스위프 정면 복귀 |
| 전체 | 네 손가락 이상 탭 | `semantic.audio` 모듈 토글 |

두 손가락은 걷기와 조준을 함께 하는 평범한 그립이므로 탭은 세 손가락부터 셉니다. 좌우 스틱은 **동시에 씁니다** — 걸으면서 훑는 것이 실험이 보려는 동작이라 둘을 번갈아 쓰게 만들면 조작 방식을 측정하게 됩니다. 블라인드를 끄는 탭은 없습니다. 설정 메뉴에 있고 참가자 빌드에서는 컴파일 제외됩니다.

### 기울기 조향

설정 메뉴에서 켜면 **폰의 자세가 몸 방위를 정합니다.** 가로로 들면 북, 핸들 돌리듯 반시계 90°면 서입니다. 중력의 화면평면 성분이 매 프레임 절대 측정이므로 캘리브레이션도 드리프트도 없습니다. 눕히면 읽을 것이 없어져 방위가 멈추고 메뉴가 그렇게 알립니다.

켜져 있는 동안 전진은 **왼쪽 화면을 누르고 있기**입니다. 굴리는 중에 엄지 이동거리를 유지하기 어렵고, 속도가 일정하면 이동거리가 곧 경과 시간이 되어 변수가 하나 줄어듭니다.

Play 시작 시 화면은 블라인드 상태입니다. 디버그 화면은 Editor 또는 Development Build에서 현재 위치, 위·경도, heading, 현재 원본 클래스와 semantic, 겹친 모든 레이어, 가장 가까운 다른 피처, 스위프 상태와 카탈로그를 표시합니다.

블라인드를 벗어나는 경로는 개발 빌드에만 있습니다. 배포 빌드를 받은 참가자는 조건을 벗어날 수 없습니다.

## 음향과 기록

모듈은 소리를 직접 내지 않습니다. `SignalIntent`를 버스에 올리고, 어떻게 들릴지는 **카탈로그**가 정합니다. 덕분에 모듈을 건드리지 않고 신호 설계만 갈아 끼울 수 있고, 그것이 실험 조건이 됩니다.

| 카탈로그 | revision | 부호화 |
| --- | ---: | --- |
| `C002_DIRECTION_RANGE` | 5 | 방향과 거리 |
| `C003_PULSE_RANGE` | 3 | 펄스 간격으로 거리 |
| `C004_PITCH_RANGE` | 3 | 음높이로 거리 |
| `C101_PULSE_CATEGORY` | 3 | 펄스로 범주 구분 — **현재 씬 기본값** |

파형·기본 주파수·거칠기·지속 시간·거리별 저역 컷오프가 asset에 들어 있고, 클립은 런타임에 합성합니다. 신호 ID는 점 단위로 상위 폴백하므로 `probe.hit.sidewalk`가 정의돼 있지 않으면 `probe.hit`으로 내려갑니다.

`semantic.audio` 모듈의 옛 신호(인도 440 Hz 종소리, 연석 1.2 kHz 클릭, 횡단보도 660/880 Hz 교대음, 차도 110 Hz 경고 펄스, 구역 경계 삼중 경고음)는 그대로 남아 있지만 **기본으로 꺼져 있습니다.** 실제 도시에서 중첩 레이어마다 울려 스위프를 묻기 때문입니다.

### 기록

런타임 CSV는 `Application.persistentDataPath/PedestrianProbe`에 저장됩니다. 시간, 월드 XYZ, 위·경도, 몸 방위와 주의 방위(`heading_deg` · `gaze_bearing_deg` · `gaze_offset_deg`), semantic, `source_class`, 현재 겹친 의미와 원본 클래스, 피처 ID, confidence, 이벤트와 상세, `map_version`, `database_id`, 그리고 **`signal_catalog`와 `signal_catalog_revision`** 을 기록합니다.

마지막 두 열이 있어야 세션이 "어디를 걸었는가"뿐 아니라 **"무엇을 듣고 있었는가"** 를 말합니다. 카탈로그가 경쟁 조건이므로 이것 없이는 세션을 비교할 수 없습니다. 카탈로그는 런타임에 바뀔 수 있어 매 행마다 현재 값을 씁니다.

이벤트로는 접근·진입·이탈·경계 접촉·블라인드 전환·모듈 토글·신호 발화와 해제·기기 정보가 남습니다.

## 현재 한계와 다음 단계

### 의미 공간

- 의미 체계가 Road, Sidewalk, Curb, Crosswalk 네 종류뿐이고, 두 도시 모두 피처의 약 90%가 `Unknown`입니다.
- 일부 분류 규칙이 CityGen 원본 클래스뿐 아니라 Hierarchy 이름에도 의존합니다.
- Crosswalk는 실제 marking 메시가 아니라 OSM node에서 만든 규격화된 형상입니다.
- 통계 CSV의 투영 면적은 피처 간 중복을 제거하지 않습니다.
- 서울 asset은 bake version 3에 머물러 있습니다.

### 신호와 실험

- **사용자 실험이 아직 하나도 수행되지 않았습니다.** 신호가 읽히는지(EXP-SIG-00), 공간 형태가 구성되는지(EXP-SWEEP-01) 모두 미측정입니다.
- 카탈로그 4종의 우열은 물론, 각각이 변별 가능한지조차 확인되지 않았습니다.
- 실험 조건을 표현할 수단이 **기본값밖에 없습니다.** `FeedbackExperimentProfile`이 없어서 조건 A(baseline)를 얻으려면 손으로 켜야 합니다.
- `semantic.audio`를 기본 OFF로 내리면서 원래의 baseline이 기본 상태에서 사라졌습니다.

### 실행 환경

- **이동을 막는 것이 실험 구역 사각형뿐입니다.** 스위프가 2 m 앞에 경계가 있다고 답해도 걸어서 통과할 수 있습니다. `SemanticSpaceKind`에 통행 불가라는 종류가 없어서, 무엇을 막을지부터 정해야 합니다.
- **스위프가 첫 경계 하나만 봅니다.** 복도에서는 가장자리가 하나뿐이라 드러나지 않았지만, 거리에서는 "연석 2.1 → 차도 2.4 → 건너편 9.7"이 정보의 대부분입니다.
- 클립을 44.1 kHz로 합성하는데 실기는 24 kHz로 믹싱합니다. 지속음 루프 이음매가 어긋날 조건이지만 청취상 문제는 보고되지 않았습니다.
- 개발 APK가 500 MB를 넘습니다. 그리는 것이 하나도 없는데 CityGen 아트가 `Resources` 경유로 딸려옵니다. 릴리스 AAB는 그것을 털어내 38 MB입니다.
- 도시 빌드가 개발 APK 하나뿐입니다. 서명도 `Resources` 제외도 없어 545 MB이고 Play에 올릴 수 없습니다.

## 다음 단계

연구 쪽 우선순위입니다. 엔지니어링 순서는 [Docs/BranchMap.md](Docs/BranchMap.md) 8절에 따로 있습니다.

1. **실험 profile** — 모듈이 셋인데 조건을 표현할 수단이 기본값밖에 없습니다. 계획은 "모듈이 두 개 이상 생기면"으로 미뤄두었고, 그 조건은 진작 충족됐습니다.
2. **EXP-SIG-00** — 신호 자체의 변별을 먼저 확인합니다. 통과하지 못하면 이후 어떤 결과도 "신호를 못 알아들은 것"과 "공간을 못 읽은 것"을 구분할 수 없습니다.
3. **의미 확장** — 원본 `sourceClass`를 유지한 채 OSM tag와 형상으로 보행 가능 영역, 광장, 녹지, 건물 진입부를 구분합니다. `Unknown` 90%를 줄이는 일입니다.
4. **다음 피드백 모듈** — 계획의 `fb-02` 이후. 경계 흐름, 교차로 위상, 기준축, 사용자 랜드마크, 관계 재생.

## 생성 파일과 주요 코드

| 경로 | 역할 |
| --- | --- |
**생성물**

| 경로 | 역할 |
| --- | --- |
| `Assets/Generated/Semantics/NYC_DoyersChinatown_256m.asset` | NYC semantic map asset (현행) |
| `Assets/Generated/Semantics/SeoulCityHall_256m.asset` | 서울시청 semantic map asset (bake v3) |
| `Assets/Generated/Semantics/*_SourceClassStatistics.csv` | 장소별 원본 클래스 통계 |
| `Assets/Generated/Signals/SignalCatalog_*.asset` | 신호 카탈로그 4종 |
| `Assets/Testbed/` | 합성 자극 7종과 그 맵 |

**의미 공간**

| 경로 | 역할 |
| --- | --- |
| `Assets/Editor/SemanticMapBaker.cs` | Scene을 semantic asset과 통계로 베이크 |
| `Assets/Editor/PedestrianProbeLocationResolver.cs` | Scene의 Database·Terrain과 장소별 출력 경로 결정 |
| `Assets/Scripts/Semantics/SemanticSpaceMap.cs` | 피처 데이터, 공간 질의와 좌표 변환 |
| `Assets/Scripts/Semantics/SpatialRayProbe.cs` | 레이와 경계의 교차 계산 |
| `Assets/Scripts/Semantics/SemanticSpaceMapProbeField.cs` | 맵을 프로브가 훑는 장으로 노출 |

**피드백 기반과 모듈**

| 경로 | 역할 |
| --- | --- |
| `Assets/Scripts/Runtime/FeedbackCoordinator.cs` | 모듈 registry, 토글, 관측 분배 |
| `Assets/Scripts/Runtime/Signals/FeedbackSignalBus.cs` | 의도를 렌더러로 나르고 로그에 남김 |
| `Assets/Scripts/Runtime/Signals/SignalCatalog.cs` | 신호 정의와 점 단위 상위 폴백 |
| `Assets/Scripts/Runtime/Signals/AudioSignalRenderer.cs` | 보이스 풀, 공간화, 저역 필터 |
| `Assets/Scripts/Runtime/Signals/SignalClipFactory.cs` | 클립 절차적 합성 |
| `Assets/Scripts/Runtime/EgocentricSweepProbe.cs` | 자기중심 스위프 모듈 |
| `Assets/Scripts/Runtime/SemanticAudioGuide.cs` | 의미 공간 음향 안내 (기본 OFF) |
| `Assets/Scripts/Runtime/LocomotionStepFeedback.cs` | 발소리. 거리로 세는 보행 리듬 (순수 `StepCadence`) |
| `Assets/Scripts/Runtime/Signals/HapticSignalRenderer.cs` | 자기운동을 진동으로 |

**실행과 조작**

| 경로 | 역할 |
| --- | --- |
| `Assets/Scripts/Runtime/PedestrianProbeRuntime.cs` | 씬의 부품을 맵에서 조립 |
| `Assets/Scripts/Runtime/Input/ProbeInputRouter.cs` | 소스 병합과 명령 전달 |
| `Assets/Scripts/Runtime/Input/BlindTouchGestures.cs` | 터치 제스처 인식 (순수, 테스트 가능) |
| `Assets/Scripts/Runtime/Input/RollSteering.cs` | 중력에서 절대 방위 (순수, 테스트 가능) |
| `Assets/Scripts/Runtime/Input/DeviceTiltInputSource.cs` | 중력 센서 접근 |
| `Assets/Scripts/Runtime/ProbeHeading.cs` | 몸 방위·주의 방위·이동 기준틀 |
| `Assets/Scripts/Runtime/Settings/ProbeSettingsMenu.cs` | 실험자용 화면 설정 메뉴 |
| `Assets/Scripts/Runtime/Platform/ProbeSessionPlatform.cs` | 핸드헬드 설정과 입력 소스 부착 |
| `Assets/Scripts/Runtime/BlindModeOverlay.cs` | 블라인드 화면, 디버그 HUD, 터치 메뉴 |
| `Assets/Scripts/Runtime/SemanticSessionLogger.cs` | 실험 CSV 기록 |
| `Assets/Editor/PedestrianFreeCamSetup.cs` | 플레이어와 런타임 설정 |
| `Assets/Editor/Testbed/SyntheticTestMaps.cs` | 정답이 알려진 합성 자극 정의 |
| `Assets/Editor/Build/AndroidTestbedBuild.cs` | 테스트베드 개발 APK · 참가자 AAB · 실험자 AAB |
| `Assets/Editor/Build/AndroidVibratePermission.cs` | 생성된 매니페스트에 진동 권한 추가 |
| `Assets/Editor/Build/AndroidCityBuild.cs` | 도시 APK |

## 문서

| 문서 | 내용 |
| --- | --- |
| [Docs/ImplementationAxes.md](Docs/ImplementationAxes.md) | 층 구조와 설계 선택지가 코드 어디에 꽂히는지. **새 기능을 붙이기 전에 먼저 봅니다** |
| [Docs/NonvisualSpatialCognitionDesignSpace.md](Docs/NonvisualSpatialCognitionDesignSpace.md) | 무엇을 만들 수 있는지 — 접근 A~G와 경쟁 가설 |
| [Docs/FeedbackSignalSystemPlan.md](Docs/FeedbackSignalSystemPlan.md) | 신호 채널과 부호화 축 배정 |
| [Docs/ModularImplementationAndExperimentPlan.md](Docs/ModularImplementationAndExperimentPlan.md) | 브랜치 규칙과 모듈 추가 절차 |
| [Docs/BranchMap.md](Docs/BranchMap.md) | 현재 브랜치, 검증 현황, 부채, 다음 순서 |

에디터에서 읽는 짧은 실행 안내는 `Assets/PedestrianProbe_README.md`에 있습니다.
