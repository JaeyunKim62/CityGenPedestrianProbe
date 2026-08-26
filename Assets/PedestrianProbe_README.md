# Pedestrian Probe

에디터에서 바로 필요한 것만 적습니다. 프로젝트 전체 설명과 데이터셋은 저장소 루트의 `README.md`, 설계는 `Docs/`에 있습니다.

## 무엇을 여느냐

두 갈래가 있고 목적이 다릅니다.

| | 씬 | 쓰임 |
| --- | --- | --- |
| 합성 테스트베드 | `Assets/Testbed/Scenes/SYN01~07` | 정답을 아는 자극. 베이크 필요 없음, 바로 Play |
| 도시 | `Assets/Scenes/NYC_DoyersChinatown.unity` | 실제 지형. 아래 베이크 절차 필요 |

신호나 조작을 확인할 때는 테스트베드가 빠릅니다. 도시는 CityGen 생성물이 있어야 합니다.

## 도시 씬 베이크

1. 도시 씬을 엽니다.
2. CityGen의 Process와 Generate가 끝나 있고 `Landscape`, 도로, 인도, Markings가 보이는지 확인합니다.
3. `Tools > Pedestrian Probe > Bake & Setup Scene`을 실행합니다. (베이크와 배치를 따로 하려면 `Bake Semantic Map`과 `Setup Scene`이 각각 있습니다.)
4. Console에 `Sidewalk`, `Curb`, `Crosswalk`, `Road`가 각각 1개 이상이고 `CityGen classes` 목록이 나오는지 봅니다.
5. 저장하고 Play.

결과는 씬이 참조하는 CityGen Database와 Terrain 크기를 기준으로 `Assets/Generated/Semantics/{LocationId}_{Size}m.asset`에 생깁니다. CityGen을 다시 Process/Generate했다면 다시 베이크해야 합니다.

실험 영역과 미니맵 경계는 활성 Terrain의 실제 월드 경계에서 가져옵니다. Terrain 내부의 0..256 지도 좌표를 쓰는 원본(`Map*`, `Highway`, OSM)에는 Terrain 원점 오프셋을 적용합니다.

베이커는 보행 의미 넷으로 매핑되지 않은 메시도 `Unknown`으로 보존하고 원본 컴포넌트 클래스명을 `source_class`에 남깁니다.

## 조작 (에디터)

| 입력 | 동작 |
| --- | --- |
| `WASD` / 방향키 | 이동 |
| `Shift` | 빠른 보행 |
| 마우스 | 시선 회전 |
| `Q` / `E` | 스위프 각도 좌 / 우 |
| `R` | 스위프 정면 복귀 |
| `[` / `]` | 사거리 − / + |
| `G` | `semantic.audio` 모듈 토글 |
| `V` | 소리 세트(카탈로그) 순환 |
| `B` / `F1` | 블라인드 / 디버그 화면 전환 |
| `N` · `M` · `C` | 미니맵 표시 · 256m↔40m · 원본 클래스↔보행 의미 |
| `Z` / `X` / `F` | 기울기 조향 흉내 — 좌 굴림 / 우 굴림 / 눕히기 |

`Z`·`X`·`F`는 센서가 없는 PC에서만 동작하고, 우상단 톱니 메뉴에서 `TILT STEERING`을 켜야 의미가 있습니다.

Play를 누르면 화면은 블라인드 상태로 시작합니다. **헤드폰을 전제로 합니다** — 발소리가 95 Hz라 노트북 스피커로는 거의 들리지 않습니다.

디버그 지도 색: 도로 빨강, 인도 파랑, 연석 주황, 횡단보도 노랑. 플레이어는 초록 삼각형, 흰 선은 가장 가까운 다른 의미 공간 방향입니다.

## 소리

주 신호는 **스위프**입니다. 좌우로 쏜 방향의 경계까지 닿아 답이 돌아오고, 어떻게 들릴지는 카탈로그 asset이 정합니다(`V`로 순환). 네 종류의 부호화가 서로 경쟁하는 실험 조건이며 자세한 것은 `Docs/FeedbackSignalSystemPlan.md`에 있습니다.

걸으면 발소리가 나고, 폰에서는 진동도 옵니다.

`semantic.audio`(인도 440 Hz 종소리, 연석 1.2 kHz 클릭, 횡단보도 660/880 Hz 교대음, 도로 110 Hz 펄스)는 **기본으로 꺼져 있습니다.** 실제 도시에서 중첩 레이어마다 울려 스위프를 묻기 때문입니다. `G`로 켤 수 있습니다.

## 기록

CSV는 `Application.persistentDataPath/PedestrianProbe`에 저장됩니다. 위치·위경도·몸 방위·주의 방위·의미 공간·원본 클래스·신호·설정 변경·기기 정보가 들어갑니다.

## 베이크 실패 시

베이커는 네 의미 종류 중 하나라도 비어 있으면 에셋을 갱신하지 않습니다. Console의 후보 수를 보고 점검합니다.

- CityGen Generate가 끝났는지
- Hierarchy의 `Landscape/Sidewalks`, `Roads` 또는 `Highways`, `Markings`가 비어 있지 않은지
- 해당 씬의 OSM Database가 `Assets/Database/`에 있는지

생성 메시에서 연석을 못 찾으면 인도 외곽선을 폭 0.2 m·낮은 신뢰도 연석으로 보완합니다. 횡단보도를 못 찾으면 OSM의 `highway=crossing` 노드로 보완하며 `crossing=unmarked`는 제외합니다.
