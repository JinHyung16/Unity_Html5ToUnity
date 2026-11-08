# 맵 데이터 저작자 표시

이 폴더의 지형 데이터는 OpenFront 의 공개 자산이다.

| 항목 | 값 |
|---|---|
| 저작자 | OpenFront (OpenFront Inc. and contributors) |
| 출처 | https://github.com/openfrontio/OpenFrontIO — `resources/maps/australia/` |
| 라이선스 | Creative Commons Attribution-ShareAlike 4.0 (CC BY-SA 4.0) — https://creativecommons.org/licenses/by-sa/4.0/ |
| 받은 날 | 2026-09-15 |

## 받은 파일과 바꾼 점

| 이 폴더 | 원래 파일 | 바꾼 점 |
|---|---|---|
| `Australia/map.bytes` | `map.bin` (3,000,000 byte) | 확장자만 `.bytes` 로 바꿨다 (Unity 가 바이트 자산으로 읽게). 내용은 그대로다 |
| `Australia/manifest.json` | `manifest.json` | 그대로 |
| `Australia/map4x.bytes` | `map4x.bin` (750,000 byte) | 확장자만 `.bytes` 로 바꿨다. 내용은 그대로다 — 축소 지도로 쓴다 (원본 FrontWars 축소 파일과 땅 62칸 · 높이 162,950칸 차이) |

## 받은 파일로 만든 것

| 만든 것 | 무엇에서 | 쓰는 곳 |
|---|---|---|
| `Art/UI/MapThumbnail_Australia.png` | `map.bytes` 를 줄여 구움 (`Editor/FrontWarsMapThumbnail.cs`) | 싱글 설정 창 지도 그림 |
| `Data/FrontWarsNationTable.json` | `manifest.json` 의 네이션 이름 · 좌표 · 국기 | 판 시작 때 네이션 배치 |

- 불러올 때 칸 바이트의 해안 비트(비트 6)를 지우고 원본 식으로 다시 계산한다. 파일은 바꾸지 않는다.
- 받은 세 파일과 위 「받은 파일로 만든 것」 표의 결과물은 같은 라이선스(CC BY-SA 4.0)로 공개할 의무가 있다.
- 게임 안에서는 메뉴 화면 오른쪽 아래에 「지도 데이터 © OpenFront · CC BY-SA 4.0」 을 표시한다 (문구 키 `menu.map_credit`).
