# Pedestrian Probe

서울시청 CityGen3D 전체 Terrain인 256m × 256m에서 비시각 보행 실험을 실행하는 프로토타입입니다.

## 최초 설정

1. `Assets/Scenes/seoul.unity`를 엽니다.
2. CityGen의 Process와 Generate가 끝나 있고 `Landscape`, 도로, 인도 및 Markings가 보이는지 확인합니다.
3. `Tools > Pedestrian Probe > Bake Semantic Map`을 실행합니다.
4. Console에 `Sidewalk`, `Curb`, `Crosswalk`, `Road`가 모두 1개 이상이고 `CityGen classes` 목록이 출력되는지 확인합니다.
5. `Tools > Pedestrian Probe > Setup Scene`을 실행합니다.
6. 씬을 저장하고 Play를 누릅니다.

베이크 결과는 현재 Scene이 참조하는 CityGen Database와 Terrain 크기를 기준으로 `Assets/Generated/Semantics/{LocationId}_{Size}m.asset`에 생성됩니다. CityGen 데이터를 다시 Process/Generate했다면 의미 지도도 다시 베이크해야 합니다.

실험 영역과 미니맵 경계는 활성 Terrain의 실제 월드 경계에서 가져옵니다. CityGen의 `Map*`, `Highway`, OSM 데이터처럼 Terrain 내부의 0..256 지도 좌표를 사용하는 원본에는 Terrain 월드 원점 오프셋을 적용합니다. 현재 서울 씬에서는 Terrain이 `(-128, -128)`에서 시작하므로 베이크된 경계는 X/Z `-128..128`입니다.

베이커는 보행용 네 의미 분류뿐 아니라 씬에서 찾은 모든 CityGen3D 메시를 수집합니다. 아직 보행 의미로 매핑하지 않은 메시도 `Unknown`으로 유지하고, `CityGen3D.MapBuilding` 같은 원본 컴포넌트 클래스명을 각 피처와 CSV의 `source_class` 열에 기록합니다.

원본 클래스 통계는 semantic asset 이름 뒤에 `_SourceClassStatistics.csv`를 붙인 장소별 파일로 생성됩니다. 현재 서울시청 256m와 NYC Doyers Chinatown 256m 결과가 있습니다. 클래스별 씬 컴포넌트 수, 피처 수, 삼각형 수, XZ 평면 투영 면적을 기록합니다. 투영 면적은 겹치는 피처를 합산한 값이므로 지표면의 중복 제거 면적과는 다릅니다.

## 조작

- `WASD` 또는 방향키: 이동
- `Shift`: 빠른 보행
- 마우스: 시선 회전
- `B` 또는 `F1`: Editor 또는 Development Build에서 검정 화면/디버그 화면 전환
- `M`: 디버그 화면의 전체 256m 지도와 플레이어 주변 40m 확대 지도 전환
- `N`: 미니맵 표시/숨김 전환
- `C`: 원본 CityGen 클래스 지도와 보행 의미 지도 전환

디버그 2D 지도 색상은 도로=빨강, 인도=파랑, 연석=주황, 횡단보도=노랑입니다. 플레이어는 초록색 방향 삼각형이며, 흰 선은 가장 가까운 다른 의미 공간까지의 방향을 나타냅니다.

Play 시작 시 화면은 검게 가려집니다. 헤드폰 사용을 전제로 합니다.

## 음향

- 인도: 440Hz 종소리
- 연석: 1.2kHz 클릭
- 횡단보도: 660/880Hz 교대음
- 도로: 110Hz 경고 펄스
- 실험 구역 경계: 삼중 경고음

공간에 가까워질 때 가장 가까운 경계 위치에서 접근음이 들리고, 공간 종류가 확정되어 바뀌면 두 번의 진입 확인음이 들립니다.

## 기록

CSV는 실행 환경의 `Application.persistentDataPath/PedestrianProbe`에 저장됩니다. 위치·위경도·시선 방향·현재 의미 공간과 접근, 진입, 이탈, 경계 접촉, 블라인드 모드 이벤트를 포함합니다.

## 베이크 실패 시

베이커는 네 의미 종류 중 하나라도 비어 있으면 에셋을 갱신하지 않습니다. Console의 후보 수를 확인하고 다음을 점검합니다.

- 현재 열린 씬이 `seoul.unity`인지
- CityGen Generate가 끝났는지
- Hierarchy의 `Landscape/Sidewalks`, `Roads` 또는 `Highways`, `Markings`가 비어 있지 않은지
- OSM Database가 `Assets/Database/seoul (0, 0).asset`에 있는지

생성 메시에서 연석을 별도로 찾지 못하면 인도 외곽선을 0.2m 폭의 낮은 신뢰도 연석으로 보완합니다. 생성 횡단보도를 찾지 못하면 OSM의 표시된 `highway=crossing` 노드로 보완하며 `crossing=unmarked`는 제외합니다.
