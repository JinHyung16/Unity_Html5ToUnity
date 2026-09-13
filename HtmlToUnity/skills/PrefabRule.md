# Prefab Rule — 배치 · 캔버스 · 조립 단위

상위: [PD](PD.md) (PD) · 사용자: [Transfer_Artist](Transfer_Artist.md)

프리팹을 만들기 전에 UI 인지 게임오브젝트인지 먼저 가른다. 폴더·규칙·어휘가 다르다.

---

## UI 인가 게임오브젝트인가

| | UI 프리팹 | 게임오브젝트 프리팹 |
|---|---|---|
| 무엇 | 창·패널·버튼·리스트 칸 | 적·탄환·미사일·유닛·이펙트 |
| Layer | `UI` | 기본(또는 전용 레이어) |
| 좌표 | `RectTransform` / 앵커 | `Transform` / 월드 좌표 |
| 폴더 | `Assets/<게임명>/Resources/UI/` | `Assets/<게임명>/` 아래 (로드는 `CLAUDE.md` 「로드 규칙」) |
| 어휘 | `Window` `Popup` `Panel` `Component` | `~Visual` (`EnemyVisual` `BulletVisual`) |
| 코드 위치 | `Assets/<게임명>/Scripts/UI/` · 공용 `Assets/Scripts/UI/` (`JinHyung.UI`) | `Assets/<게임명>/Scripts/View/` (`JinHyung.<게임>`) |

- `Window` / `Popup` / `Panel` / `View` 는 UI 프레임워크 전용 어휘다. 월드 오브젝트 이름에 쓰지 않는다.
- 프리팹 에셋은 접미 없이 오브젝트 이름 그대로 둔다 (`Enemy.prefab`).

---

## 폴더

```
Assets/<게임명>/Resources/UI/
├─ Windows/      전체 화면
├─ Popup/        버튼으로 여는 창 (80% 이하)
├─ Panel/        창 안 구획
├─ Components/   재사용 최소 단위
├─ Common/       기성 공용 프리팹 (버튼·텍스트·게이지·딤)
└─ Graph/        노드 그래프 위젯류
```

규약은 UI 와 월드를 가르는 것이다. 하위 폴더 이름은 프로젝트가 정하고, 경로와 로드 방식은 `CLAUDE.md` 폴더·로드 규칙을 따른다.

- 많이 생성될 것은 풀링한다 (UI 는 `PrefabAuto`, 월드는 전용 풀).
- 풀 반환 규약(`Dead = true` 등)을 정하고 생성 지점마다 반환 지점을 짝지어 확인한다.
- 리스트에서 뺄 때 `RemoveAt` 반복(O(n²)) 대신 단일 패스 압축을 쓴다.

---

## Canvas 설정 — 프로젝트당 한 번 정하고 절대 바꾸지 않는다

Reference Resolution 은 PD 착수 질문 3번의 답으로 채운다 (PD 「질문 문안」).

| 항목 | 값 |
|---|---|
| Render Mode | ScreenSpace - Camera |
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | X 1080 · Y 1920 (Portrait 기본). Landscape 기본은 1920×1080 |
| Screen Match Mode | Expand |

### 픽셀을 환산해 박지 않는다

원본 논리 해상도와 Canvas Reference 의 배율을 곱해 좌표를 박지 않는다. 환산은 팩토리 `Px()` 한 곳만 통과한다 (UIUX 「환산 상수는 한 곳에서만」).

크기·배치는 앵커 · 레이아웃 그룹 · `LayoutElement` · `ContentSizeFitter` · `AspectRatioFitter` 로 잡는다.
원본 CSS 값은 비율과 의도를 읽는 참고 자료이지 옮겨 적을 수치가 아니다.

### `renderMode` 는 상황마다 다르다 — 프리팹 값을 믿지 마라

| 언제 | renderMode | worldCamera |
|---|---|---|
| 에디터에서 프리팹을 열었을 때 | 프리팹에 저장된 값 | 없을 수 있다 |
| 런타임(실기) | `ScreenSpaceCamera` | UI 카메라 |

- 중첩 Canvas 는 켜질 때마다 루트를 보고 `renderMode` · `worldCamera` · `sortingLayerID` 를 맞춘다. 프리팹에 박으면 한쪽이 깨진다.
- 프리팹 인스펙터의 중첩 Canvas 값이 런타임과 달라도 정상이다.
- 판정은 재생 중 `Canvas.renderMode` 를 읽어서 한다 (`PLAYMODE=1 Tools/unity-batch.sh`).

---

## 계층 — Window → Panel 두 단계까지만

정본은 `UIUX.md` 「계층 — Window → Panel 두 단계까지만」이다.
프리팹 쪽에서 지킬 것은 하나 — 원본 DOM 구조를 그대로 옮긴다.

## 앵커 패턴

| 용도 | anchorMin/Max | pivot | 비고 |
|---|---|---|---|
| 전체 배경 | (0,0)~(1,1) | (0.5,0.5) | sizeDelta 0 |
| 상단 고정 바 | (0,1)~(1,1) | (0.5,1) | `sizeDelta.y`=높이, `sizeDelta.x`=음수(좌우 인셋) |
| 세로 스택 블록 | (0,1)~(1,1) | (0.5,1) | `anchoredPosition.y` = -y |
| 고정폭 블록 | (0,1) 점앵커 | (0.5,1) | 절대 sizeDelta |
| 하단 버튼 바 | (0,0)~(1,0) | (0.5,0) | |
| 중앙 카드 | (0.5,0.5) | (0.5,0.5) | 팝업 기본 |

- 회전시킬 rect 는 앵커·피벗을 직접 잡는다. 상단 피벗 헬퍼는 그 축으로 돌아 엉뚱한 데 간다.
- 부모 변에 붙이는 아이콘은 앵커까지 옮긴다. 기본 앵커(0.5,0.5) 헬퍼에 위치만 주면 가운데 기준으로 앉는다.

---

## 조립 단위 — 큰 프리팹 안에 다 넣지 않는다

작은 단위를 재사용 가능하게 모듈화해 조립한다.

- 아이콘 / 이름 / 개수 줄이 상단 재화탭과 아이템 표기에 같이 쓰이면 프리팹 2개 대신 `ItemBaseComponent` 하나를 만들고 상위에서 표기할 정보만 넘긴다.
- 순서: 기성 Common 프리팹 → 기존 컴포넌트 재사용 → 프리팹 변형 추가 → 새 클래스.

---

## 프리팹 생성 방식은 확정표 10-b 가 가른다 — 이 문서가 정하지 않는다

누가 이 프리팹을 이어서 고치는가가 정한다. 기본값은 프리팹이 주인이다.

| 확정표 10-b | 빌더가 하는 일 | 이후 고칠 곳 |
|---|---|---|
| 프리팹이 주인 (기본값) | 첫 초안을 굽는다. 프리팹이 이미 있으면 건너뛴다 | 프리팹. 사람은 에디터에서, AI 는 에디터 API 로 연다 |
| 빌더가 주인 | 매번 통째로 다시 굽는다 | 빌더 코드. 손으로 고친 것은 다음 굽기에 사라진다 |

이유(기본값): 결과물을 이어서 다듬는 것이 목적이다. 빌더가 주인이면 디테일 수정이 전부 코드 수정이 된다. 「사람에게 엔진 조작을 요구하지 않는다」는 AI 가 에디터 API 로 프리팹을 고쳐서 지킨다.

적용: 기본값이 「프리팹이 주인」으로 바뀐 뒤 새로 착수하는 게임부터다. 이미 「빌더가 주인」으로 만든 게임은 그대로 둔다 [사람 확정 — 소급 안 함].

주의: 섞지 않는다. 「프리팹이 주인」인데 빌더를 다시 돌려 통째로 구우면 사람의 작업이 조용히 사라진다.

### 프리팹이 주인일 때

- 초안 빌더는 저장 전에 `AssetDatabase.LoadAssetAtPath` 로 있는지 보고, 있으면 건너뛰고 로그만 남긴다. 통째로 다시 굽는 것은 사람이 명시할 때만 하고 원장에 적는다.
- 초안 이후 수정은 기존 프리팹을 열어 고친다 (`PrefabUtility.LoadPrefabContents` → 수정 → `SaveAsPrefabAsset` → `UnloadPrefabContents`). 여러 개를 한꺼번에 고칠 때도 새로 굽지 않고 이 방식의 일괄 스크립트를 쓴다.
- 보이는 조정값(위치·크기·색·알파·연출 속도·타이밍)은 `[SerializeField]` 필드로 둔다. 기본값은 원본 값이고 출처를 `[Tooltip]` 이나 주석으로 남긴다. 코드 상수로 두지 않는다.
- 런타임이 프리팹 필드를 상수로 덮어쓰지 않는다. 바꾸는 것은 데이터·상태로 갈리는 값뿐이다.
- 런타임에 `new GameObject` + `AddComponent` 로 계층을 조립하지 않는다. `Instantiate` 한다.
- 코드로 Mesh 를 묶어 그리는 것은 사람이 지시할 때만 연다. 스스로 드로잉으로 가지 않는다.

---

## 원본의 배치 상수는 프리팹으로 온다 — 시트로 보내지 않는다

원본 캔버스 좌표를 시트 컬럼으로 옮기고 코드가 꽂으면 프리팹이 배치의 진실 소스라는 전제가 깨지고, 배치가 두 곳으로 갈린다.

- 정규화 −1~1 좌표는 앵커 그 자체다. `x = -0.4` → `anchorMin.x = anchorMax.x = 0.3` (`(1 + x) / 2`). 해상도가 바뀌어도 앵커가 따라가므로 데이터일 이유가 없다.
- 개체별 장식 개수가 다르면(1~N) 개체별 프리팹을 만든다. 1:N 시트로 좌표를 담지 않는다.
- 시트에 남는 것은 그 개체가 어느 프리팹을 쓰는가(`~Path` 한 컬럼)다.
- 컬럼명에 좌표·오프셋·크기가 들어가면 프리팹 자리인지 먼저 묻는다.
- 환산 배율을 새 필드로 만들지 않는다(「프리팹 값 × 배율」). 값 자체를 최종 단위(픽셀/초)로 바꿔 넣는다.

사례: 개체 12종의 장식 좌표를 1:N 시트 3개·99행으로 옮겨 `Instantiate` + 좌표 주입을 했고, 거동 컴포넌트를 걷으며 환산 배율(≈0.49)을 필드로 되살렸다.

## 함정 — 실제로 걸렸던 것들

### 레이아웃

| 함정 | 실제 |
|---|---|
| 빌드 시점 `rect.width` 로 비율 계산 | 레이아웃 전이라 기본값 100 이 잡힌다. 절대 폭을 `preferredWidth` 로 주고 `childForceExpandWidth=false` |
| `VerticalLayoutGroup` 의 `childControlHeight` | 코드로 붙이면 false 로 남아 자식이 기본 100 으로 앉는다. 쓰는 자리에서 명시 |
| `HorizontalLayoutGroup` 의 `childForceExpandWidth` | 기본 true. `preferredWidth` 가 무시되고 합이 부모 폭을 넘으면 0폭까지 눌린다 |
| `childControlHeight=true` 아래 버튼 | `preferredHeight` 가 -1 이면 0 높이로 수축해 클릭 영역이 사라진다 |
| 레이아웃 그룹의 자식에 `ContentSizeFitter` | 부모와 높이를 두고 싸운다. 스크롤 Content(그룹 밖)에만 |
| 그룹 안의 테두리·코너 장식 | `LayoutElement.ignoreLayout` 을 준다. 없으면 한 행으로 취급된다 |
| 그룹 아래 작은 아이콘에 `LayoutElement` 누락 | 점 하나가 행 폭으로 늘어나 타원이 된다 |

### 렌더링

| 함정 | 실제 |
|---|---|
| 한 GameObject 에 Graphic 하나 | TMP 오브젝트에 `AddComponent<Image>()` 는 null 반환 → NRE. 배경은 별도 자식으로 |
| `Image.type = Filled` | sprite 가 없으면 무시된다. 흰 스프라이트를 물린다 |
| `Image.Type.Tiled` | 원본 픽셀 크기로 가로·세로 양쪽 반복한다. `pixelsPerUnitMultiplier` 로 주기를 맞춘다 |
| 9-slice 보더 | 원본 픽셀 크기로 그려진다. 좌우 보더 합이 배치 크기를 넘으면 가운데가 사라진다. 그 아트를 쓰는 가장 작은 자리 기준으로 잡는다 |
| 원본이 `background-size:100% 100%` | 9-slice 가 아니라 통짜 스트레치다 |
| 세로로 크게 눌리는 장식 아트 | 속이 뭉개진다. 캡처 색으로 단색 판을 깔고 아트는 테두리로만 |
| 중첩 Canvas | `overrideSorting` + `sortingOrder` + `GraphicRaycaster` + 루트와 같은 `renderMode`/`worldCamera` 세트를 완성한다. `worldCamera` 가 비면 WorldSpace 렌더에서 하위가 통째로 안 그려진다 |
| 중첩 Canvas 의 order 를 프리팹에 굽기 | 루트 `sortingOrder` 는 창이 열릴 때 나중에 들어온다. `OnEnable` 에서 한 번만 읽으면 낮은 값으로 굳어 전면 히트 영역에 먹힌다. 루트 order 변화를 따라가는 컴포넌트를 붙인다 |
| 형제 순서로 해결되는데 중첩 Canvas | 쓰지 않는다 |
| 투명 히트 판 | 이미지 헬퍼는 대개 `raycastTarget=false` 가 기본이다. 명시적으로 true |
| 클리핑 누락 | 원본이 `clip()` / `overflow:hidden` 이면 `RectMask2D` 가 필요하다 |

### 판정

| 함정 | 실제 |
|---|---|
| 재생 중 레이캐스트 좌표 | 루트가 `ScreenSpaceCamera` 라 월드 좌표를 그대로 넣으면 전부 `NONE` 으로 「다 막혔다」고 오판한다. `RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, center)` 를 쓴다 |
| 판 안쪽 y 를 화면 절대값으로 | 판이 상단 stretch 로 앉으면 자식 y 는 판 상단 기준이다 |
| CSS 우선순위 | `#id .class` 가 `#id` 를 이긴다. id 선택자만 보고 판단하면 틀린다 |
| 기준 캡처가 렌더 전 상태 | 그대로 베끼면 조작 불가능한 빈 칸이 남는다. 원본 코드가 실제로 그리는 것을 만든다 |

---

## 완료 체크리스트

- [ ] UI/게임오브젝트 구분이 맞고 UI 프리팹의 Layer 가 `UI` 다
- [ ] UI 프리팹이 `Assets/<게임명>/Resources/UI/` 아래에 있고 월드 프리팹이 섞이지 않는다 (하위 폴더 이름은 프로젝트 확정값)
- [ ] Canvas 4항목이 규격값이다 (ScreenSpace-Camera / Scale With Screen Size / Reference / Expand)
- [ ] 계층이 Window → Panel 두 단계 안이고 창 루트에 낱개 위젯이 없다
- [ ] 절대 y·환산 픽셀이 아니라 앵커와 레이아웃 그룹으로 잡혔고 접힘/펼침 두 상태가 같이 맞는다
- [ ] 코드가 `sizeDelta` · `anchoredPosition` 을 계산해 넣는 자리가 없다
- [ ] `[SerializeField]` 참조에 빈 슬롯이 없다
- [ ] 10-b 가 「프리팹이 주인」이면 빌더를 다시 돌려도 기존 프리팹이 바뀌지 않는다
- [ ] 보이는 조정값이 프리팹 필드에 있다 — 코드 상수로 박힌 위치·크기·색·연출 수치가 0 (PD 「속 품질 — 이어서 다듬을 수 있어야 한다」)
- [ ] 재생 중 `RaycastAll` 로 눌러야 하는 것의 첫 결과가 자기 자신이다 (`PLAYMODE=1`)
