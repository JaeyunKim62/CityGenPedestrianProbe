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
| 촉각 렌더러 | 미구현 | `ISignalRenderer` 하나 추가하면 붙는다 |

### 피드백 모듈

| 모듈 ID | 기본값 | 상태 | 설명 |
| --- | --- | --- | --- |
| `probe.egocentric-sweep` | **ON** | 연구 대상 | 자기중심 좌우 스위프. 몸 기준 각도로 경계를 훑고 거리를 답한다 |
| `semantic.audio` | OFF | 프로토타입 | 접근·진입·경계 안내. 도시에서 중첩 레이어마다 울려 스위프를 묻어서 꺼둔다 |

### 실행과 조작

| 기능 | 상태 | 설명 |
| --- | --- | --- |
| 입력 라우터 | 완료 | 소스가 프레임을 채우고 라우터가 병합. 플랫폼이 아니라 **존재 여부**로 고른다 |
| 키보드·마우스 소스 | 완료 | 이동·시선 |
| 터치 소스 | 완료 | 좌반부 보행·회전, 우반부 조준·사거리, 다중 탭 명령. mm 단위라 기기 무관 |
| 블라인드 화면과 디버그 HUD | 완료 | 상태·미니맵·화면 설정 메뉴. 디버그 요소는 개발 빌드 한정 |
| 실험 CSV 로그 | 완료 | 위치·분류·레이어·신호·토글·기기 정보 |
| 합성 테스트베드 | 완료 | 정답을 아는 자극 5종 (`Assets/Testbed`) |
| Android 빌드 | 완료 | 테스트베드·도시 APK. 실기에서 10 Hz 유지 확인 |
| 씬 전환 | 미구현 | 폰에서 자극을 바꾸려면 재설치가 필요하다 |
| 사용자 실험을 통한 신호 검증 | 미실시 | 음향 패턴과 confidence 값 검증 필요 |

`Unknown`은 반드시 데이터가 없는 상태를 뜻하지 않습니다. 원본 CityGen 피처는 존재하지만 아직 현재의 네 보행 의미 중 하나로 정의되지 않은 경우가 대부분입니다.

## 현재 데이터셋

현재 저장된 semantic asset bake version 3 기준입니다.

| 항목 | 현재 값 |
| --- | --- |
| 대상 지역 | 서울시청·서울광장 일대 |
| 중심 Origin | 위도 `37.5665016174`, 경도 `126.9779968262` |
| CityGen tile | `[0, 0]`, Area `1` |
| Terrain | `256 m × 256 m` |
| 월드 경계 | X `-128..128`, Z `-128..128` |
| OSM 데이터 | Node 4,083 / Way 399 / Relation 14 |
| CityGen 원본 클래스 | 26종 |
| Scene 컴포넌트 | 1,608개 |
| 베이크 피처 | 387개 |
| 베이크 삼각형 | 49,909개 |
| 공간 인덱스 cell | 5 m |

현재 보행 semantic 피처는 다음과 같습니다.

| 의미 | 피처 수 | 주요 원본 |
| --- | ---: | --- |
| `Unknown` | 337 | Landscape, MapBuilding, MapRoad, MapSurface, Plot |
| `Sidewalk` | 24 | Highway, Landscape |
| `Curb` | 4 | Highway |
| `Crosswalk` | 5 | OSM crossing node |
| `Road` | 17 | MapRoad |

0.1 m 격자로 피처 내부 중복을 제거해 분석하면, 원본 피처는 전체 실험 영역의 약 88.72%를 덮습니다. 어떤 원본 피처도 덮지 않는 영역은 약 11.28%이고, 현재 네 보행 semantic 중 하나가 정의된 합집합 영역은 약 18.82%입니다. 클래스와 semantic은 서로 겹칠 수 있으므로 개별 점유율을 단순 합산할 수 없습니다.

클래스별 컴포넌트 수, 피처 수, 삼각형 수와 누적 투영 면적의 전체 표는 [SeoulCityHall_256m_SourceClassStatistics.csv](Assets/Generated/Semantics/SeoulCityHall_256m_SourceClassStatistics.csv)에 생성됩니다. 이 CSV의 투영 면적은 피처 간 겹침을 포함합니다.

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

#### 터치

화면이 검은 상태에서 쓰이므로 고정 위치 버튼이 없습니다. 모든 제스처는 **손가락이 닿은 지점 기준**이고, 거리는 화면 DPI로 mm 환산해 기기가 달라도 같은 동작이 같은 각도를 뜻합니다.

| 영역 | 제스처 | 동작 |
| --- | --- | --- |
| 좌반부 | 위·아래 드래그 | 전진 / 후진 |
| 좌반부 | 좌·우 드래그 | 몸 회전 |
| 우반부 | 좌·우 드래그 | 스위프 각도 (절대 매핑, ±30 mm ↔ ±90°) |
| 우반부 | 위·아래 드래그 | 사거리 |
| 전체 | 두 손가락 탭 | 스위프 정면 복귀 |
| 전체 | 세 손가락 탭 | `semantic.audio` 모듈 토글 |
| 전체 | 네 손가락 탭 | 블라인드 토글 (개발 빌드 한정) |

한 손가락 탭은 오작동을 막기 위해 아무 동작도 하지 않습니다. 현재 좌우 스틱은 **동시에 쓸 수 없습니다** — 손가락이 둘 이상이면 탭 후보로 넘어갑니다.

Play 시작 시 화면은 블라인드 상태입니다. 디버그 화면은 Editor 또는 Development Build에서 현재 위치, 위·경도, heading, 현재 원본 클래스와 semantic, 겹친 모든 레이어, 가장 가까운 다른 피처, 스위프 상태와 카탈로그를 표시합니다.

블라인드를 벗어나는 경로는 개발 빌드에만 있습니다. 배포 빌드를 받은 참가자는 조건을 벗어날 수 없습니다.

## 음향과 기록

현재 음향은 초기 실험값입니다.

| 상황 | 신호 |
| --- | --- |
| Sidewalk | 440 Hz 종소리 |
| Curb | 1.2 kHz 클릭 |
| Crosswalk | 660/880 Hz 교대음 |
| Road | 110 Hz 경고 펄스 |
| 실험 구역 경계 | 삼중 경고음 |

런타임 CSV는 `Application.persistentDataPath/PedestrianProbe`에 저장됩니다. 시간, 월드 XYZ, 위·경도, heading, semantic, `source_class`, 현재 겹친 의미와 원본 클래스, 피처 ID, confidence, 접근·진입·이탈·경계·블라인드 이벤트, map version과 Database ID를 기록합니다.

## 현재 한계와 다음 단계

- 현재 의미 체계는 Road, Sidewalk, Curb, Crosswalk 네 종류뿐입니다.
- 일부 분류 규칙이 CityGen 원본 클래스뿐 아니라 Hierarchy 이름에도 의존합니다.
- Crosswalk 5개는 실제 marking 메시가 아니라 OSM node에서 만든 규격화된 형상입니다.
- 원본 `MapSurface`, `Plot`, `MapBuilding`, `Landscape`의 상당 부분은 아직 `Unknown`입니다.
- 통계 CSV의 투영 면적은 피처 간 중복을 제거하지 않습니다.
- 장소별 Database와 출력 선택은 서울시청 256m와 NYC Doyers Chinatown 256m에서 생성·베이크·실행을 확인했습니다.
- 음향 패턴과 confidence는 사용자 실험으로 검증되지 않았습니다.

다음 단계는 원본 `sourceClass`를 유지하면서 OSM tag와 형상 정보를 함께 사용해 보행 가능 영역, 광장/공터, 녹지, 건물과 건물 진입부를 구분하는 것입니다. 그 뒤 semantic별 위치 판정 정확도와 음향 안내의 이해도를 실제 사용자 실험으로 평가할 예정입니다.

## 생성 파일과 주요 코드

| 경로 | 역할 |
| --- | --- |
| `Assets/Generated/Semantics/SeoulCityHall_256m.asset` | 서울시청 semantic map asset |
| `Assets/Generated/Semantics/SeoulCityHall_256m_SourceClassStatistics.csv` | 서울시청 원본 클래스 통계 |
| `Assets/Generated/Semantics/NYC_DoyersChinatown_256m_SourceClassStatistics.csv` | NYC Doyers Chinatown 원본 클래스 통계 |
| `Assets/Editor/SemanticMapBaker.cs` | Scene을 semantic asset과 통계로 베이크 |
| `Assets/Editor/PedestrianProbeLocationResolver.cs` | Scene의 Database·Terrain과 장소별 출력 경로 결정 |
| `Assets/Scripts/Semantics/SemanticSpaceMap.cs` | 피처 데이터, 공간 질의와 좌표 변환 |
| `Assets/Scripts/Runtime/SemanticDebugMapRenderer.cs` | 원본 클래스/보행 의미 미니맵 |
| `Assets/Scripts/Runtime/BlindModeOverlay.cs` | 블라인드·디버그 UI와 입력 |
| `Assets/Scripts/Runtime/SemanticAudioGuide.cs` | 의미 공간 음향 피드백 |
| `Assets/Scripts/Runtime/SemanticSessionLogger.cs` | 실험 CSV 기록 |
| `Assets/Editor/PedestrianFreeCamSetup.cs` | 플레이어와 런타임 설정 |

Unity Project 창에서 읽는 짧은 실행 안내는 `Assets/PedestrianProbe_README.md`에 있습니다.
