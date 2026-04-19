# Agent Paw 디자인 시스템

| 항목 | 내용 |
|---|---|
| 문서 버전 | v0.1.0 |
| 작성일 | 2026-04-15 |
| 작성자 | 안승준 |
| 적용 범위 | AgentPaw WPF 클라이언트 (.NET 10 + WPF UI) |
| 디자인 컨셉 | Warm Analog Sketchbook — 종이 위에 올려놓은 카드 |

---

# 1. 디자인 원칙

. 일관성 : 동일 컴포넌트는 동일 토큰·동일 형태·동일 인터랙션을 사용한다.
. 종이 질감 : 모든 배경은 flat 색상 단독을 금지하고 PaperGrain · PawPrintTile 텍스처를 중첩한다.
. 따뜻한 톤 : 모든 배경은 양피지 계열(#F5F0E8 ~ #FAF7F2)을 기반으로 한다. 차가운 회색 계열을 금지한다.
. 강조는 골드로 통일 : 주요 버튼·강조 텍스트·진행 표시는 PawGold(#C8A96E)로 통일한다.
. 모든 텍스트는 PawFont(Pretendard) 적용 : 시스템 기본 폰트 사용을 금지한다.

---

# 2. 컬러 토큰

## 2.1. Core Tones (배경·표면·테두리)

| 토큰 | HEX | 용도 |
|---|---|---|
| PawParchmentColor | #F5F0E8 | 메인 배경(양피지) |
| PawCreamColor | #EDE8DC | 보조 배경, 카드 강조 영역 |
| PawCardColor | #FAF7F2 | 카드 표면 |
| PawSidebarBrownColor | #3A2F28 | 사이드바 배경 |
| PawSidebarHoverColor | #4A3F35 | 사이드바 호버 |
| PawSidebarBorderColor | #5C4A3A | 사이드바 보더 |
| PawSidebarTextColor | #D4C4A8 | 사이드바 본문 텍스트 |
| PawSidebarMutedColor | #9C8B78 | 사이드바 보조 텍스트 |
| PawSidebarHeadingColor | #EDE8DC | 사이드바 헤딩 텍스트 |
| PawSidebarVersionColor | #6B5E50 | 사이드바 버전 표기 |
| PawGoldColor | #C8A96E | 강조·포커스·골드 액센트 |
| PawBorderColor | #D4C4A8 | 카드·다이얼로그 보더 |
| PawArchiveBgColor | #F0EBE2 | 아카이브 영역 배경 |
| PawArchiveTextColor | #8B7355 | 아카이브 본문 텍스트 |
| PawArchiveSubTextColor | #A09080 | 아카이브 보조 텍스트 |
| PawTextPrimaryColor | #2D2A26 | 본문 1차 텍스트 |
| PawTextSecondaryColor | #6B6560 | 본문 2차 텍스트 |

## 2.2. Semantic Colors (의미 색상)

| 토큰 | HEX | 용도 |
|---|---|---|
| PawPlanningBlueColor | #4A7FB5 | 기획 카테고리, Planner 페르소나 |
| PawImplOrangeColor | #C97B3E | 구현 카테고리, 코드 작성 |
| PawArchYellowColor | #C9A93E | 아키텍처 카테고리 |
| PawSystemGreenColor | #6B9B4A | 시스템 정상·성공 상태 |
| PawErrorRedColor | #B85450 | 오류·경고 |

## 2.3. Tinted Brush (반투명)

| 토큰 | 베이스 색상 | Opacity | 용도 |
|---|---|---|---|
| PawGoldTint | PawGoldColor | 0.13 | 골드 배지·태그 배경 |

## 2.4. 사용 규칙

. 본문 텍스트 색상은 PawTextPrimary / PawTextSecondary 둘만 사용한다. 임의 회색 코드 직접 입력을 금지한다.
. 강조 버튼(Appearance="Primary")의 강조 색상은 PawGold만 사용한다. Semantic Color를 Primary 버튼 색상으로 사용하는 것을 금지한다.
. 오류 메시지 영역의 배경은 반드시 `SolidColorBrush Color="{StaticResource PawErrorRedColor}" Opacity="0.08"` 패턴으로 처리한다. 단색 배경 사용을 금지한다.

---

# 3. 타이포그래피

## 3.1. 폰트 패밀리

. PawFont : `Pretendard, Malgun Gothic, Segoe UI` 폴백 체인.
. 모든 TextBlock·TextBox·Button에 `FontFamily="{StaticResource PawFont}"` 적용을 강제한다.

## 3.2. 사이즈 체계

| 역할 | FontSize | FontWeight | 적용 위치 |
|---|---|---|---|
| 페이지 타이틀 | 18 | Bold | 페이지 헤더 H1 |
| 다이얼로그 타이틀 | 16~18 | Bold/SemiBold | 다이얼로그 헤더 |
| 섹션 헤더 | 16 | SemiBold | 카드 내부 섹션 제목 |
| 카드 타이틀 | 14 | SemiBold | 페르소나·인스트럭션 카드명 |
| 본문 | 12 | Normal | 일반 텍스트 |
| 보조 설명 | 11 | Normal | 카드 부가 설명 |
| 메타·태그 | 10 | Normal | 모델 배지, 메타 정보 |
| 캡션 | 9 | Normal | 가장 작은 부가 정보 |

. FontSize 13/15/17 같은 비표준 사이즈 사용을 금지한다.

---

# 4. 종이 텍스처 시스템

## 4.1. 텍스처 자원

| 토큰 | 종류 | Opacity | 용도 |
|---|---|---|---|
| PaperGrainBrush | DrawingBrush (60×60 노이즈) | 0.035 | 모든 페이지 본문 배경 위에 깔린다 |
| PawPrintTileBrush | DrawingBrush (160×170 발자국) | 0.025 | 콘텐츠 영역 백그라운드 |
| PawPrintSidebarBrush | DrawingBrush (120×130 발자국) | 0.05 | 사이드바 백그라운드 |
| PawPrintLoginBrush | DrawingBrush (180×190 발자국) | 0.035 | 로그인 페이지 백그라운드 |

## 4.2. 적용 패턴

페이지 루트는 반드시 아래 두 Border를 배경으로 깐다. 순서를 바꾸지 않는다.

```xml
<Border Background="{StaticResource PaperGrainBrush}" IsHitTestVisible="False" />
<Border Background="{StaticResource PawPrintTileBrush}" IsHitTestVisible="False" />
```

. 두 Border 모두 `IsHitTestVisible="False"`를 명시한다. 이벤트를 막지 않는다.
. 두 Border는 SidePage(WorkspacePage 등 내부 채팅 페이지)에는 적용하지 않는다.

---

# 5. 카드 스타일

## 5.1. PawCardStyle

| 속성 | 값 |
|---|---|
| Background | LinearGradientBrush(#FDFBF8 → #FAF7F2 → #F7F3EC, 대각선) |
| BorderBrush | LinearGradientBrush(#DDD0B8 → #C8B898, 대각선) |
| BorderThickness | 1 |
| CornerRadius | 8 |
| Padding | 20,16 |
| Effect | DropShadowEffect(Blur=16, Opacity=0.12, Depth=3, Direction=260, Color=#7A6545) |

## 5.2. 사용 규칙

. 모든 정보 카드는 `Style="{StaticResource PawCardStyle}"`만 사용한다. 카드별 커스텀 Border 스타일을 금지한다.
. 카드 간 간격은 `Margin="0,0,0,16"` 으로 고정한다.

---

# 6. 컴포넌트

## 6.1. 페르소나 아바타

페르소나 아바타는 **둥근 사각형(rounded rectangle)** 으로 통일한다. 원형(circle) 사용을 금지한다.
사이즈와 CornerRadius는 아래 매트릭스를 따른다.

| 적용 위치 | Width × Height | CornerRadius | BorderThickness | 폴백 이모지 FontSize |
|---|---|---|---|---|
| 카드 리스트 (ProjectSettings 등) | 40×40 | 6 | 1 | 18 |
| 페르소나 그리드 (PersonaPage 갤러리) | 60×60 | 8 | 1 | 28 |
| 템플릿 갤러리 | 78×78 | 8 | 1 | - |
| 채팅 메시지 (WorkspacePage) | 100×100 | 12 | 2 | 50 |
| 페르소나 편집 다이얼로그 미리보기 | 200×200 | 16 | 1 | 72 |

. 사이즈 결정 비율 : CornerRadius / Width ≈ 0.10 ~ 0.15. 임의 비율 사용을 금지한다.
. 폴백(아바타 없음) 표시 : `Background="{DynamicResource PawCreamBrush}"` 위에 🐾 이모지를 중앙 정렬한다.
. 보더 색상 : 채팅용은 페르소나 컬러(`PersonaColorToBrushConverter`), 그 외는 `PawBorder`를 사용한다.

## 6.2. ProgressRing (로딩 인디케이터)

. 색상 : 반드시 `Foreground="{DynamicResource PawGold}"`로 고정한다.
. 사이즈 : Width/Height 40 (페이지 전체 로딩) / 24 (인라인 로딩).
. 시스템 기본 색상(파랑) 사용을 금지한다.

## 6.3. 다이얼로그 오버레이 (Z-Index 체계)

다이얼로그·로딩 등 오버레이는 `Panel.ZIndex` 로 레이어링한다.

| 레이어 | ZIndex | 용도 |
|---|---|---|
| 로딩 오버레이 | 10 | 페이지 전체 로딩(`#88F5F0E8` 반투명) |
| 1차 다이얼로그 | 20 | 카드 편집·연결 다이얼로그(`#88A09080` 반투명) |
| 중첩 다이얼로그 | 30 | 다이얼로그 위에서 다시 띄우는 픽커(`#A0A09080` 반투명) |

. 다이얼로그 컨테이너 Border는 `MouseLeftButtonDown="DialogContent_Click"` 으로 클릭 버블링을 차단하여 외부 영역 클릭 시에만 닫히게 한다.
. 1차 다이얼로그 폭은 용도별로 표준화한다 — 폼: 560 / 링크 리스트: 450 / 갤러리: 640.

## 6.4. ESC 키 처리

다이얼로그가 열린 페이지의 UserControl은 `Focusable="True" PreviewKeyDown="Page_PreviewKeyDown"` 을 명시한다.
ESC 처리 우선순위는 **위 레이어부터 닫는 cascade** 로 구현한다.

```csharp
private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key != Key.Escape) return;

    if (NestedDialogOverlay.Visibility == Visibility.Visible) { /* 닫고 e.Handled=true */ }
    else if (PrimaryDialogOverlay.Visibility == Visibility.Visible) { /* 닫고 e.Handled=true */ }
}
```

. ESC 한 번 누름 = 한 레이어만 닫는다. 모든 다이얼로그를 한 번에 닫는 것을 금지한다.

## 6.5. 빈 상태 (Empty State) 텍스트

. 빈 상태 메시지는 본문 12pt + `Foreground="{DynamicResource PawTextSecondary}"` 로 통일한다.
. 종결 어미는 반드시 `~다`/`~한다`. `~습니다`, `~없어요`, `~없습니다` 사용을 금지한다.
. 예시: "연결된 지침이 없다.", "스냅샷이 없다.", "페르소나가 없다."

## 6.6. PlaceholderText 형식

입력 필드 PlaceholderText는 반드시 `예: ...` 형식으로 작성한다.

. 이름 입력 : `예: planner`
. 표시 이름 입력 : `예: 기획자`
. 설명 입력 : `예: 기술 아키텍처를 설계한다`
. 스냅샷 설명 : `예: v1.2 기능 추가 전 백업`

. 단순 안내문(`이름을 입력하세요`) 사용을 금지한다. 입력 예시를 노출하여 사용자가 형식을 즉시 파악할 수 있게 한다.

## 6.7. 버튼 순서 (다이얼로그 풋터)

. 풋터 버튼은 **오른쪽 정렬**, 순서는 `[취소(Secondary)] [저장/확인(Primary)]` 이다.
. `Margin="0,0,8,0"` 로 취소-저장 간 간격을 둔다.
. 다이얼로그 닫기 전용 버튼은 `[닫기(Secondary)]` 단독으로 우측 정렬한다.

## 6.8. 페르소나 연결 다이얼로그 (그룹 일괄 연결)

전역 페르소나 → 프로젝트 연결 시 그룹 단위 일괄 등록을 지원한다.

. 다이얼로그는 그룹 묶음 단위로 카드를 노출한다. 그룹 헤더 우측에 `[그룹 전체 연결]` 버튼을 둔다.
. 각 그룹 안의 페르소나는 들여쓰기 후 `[연결]` 보조 버튼을 둔다.
. 그룹이 없는 페르소나는 "미분류" 묶음으로 분리한다.

---

# 7. 아이콘

## 7.1. 사용 라이브러리

. WPF UI(`xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"`)의 SymbolIcon만 사용한다.
. 외부 PNG·SVG 아이콘 직접 임포트를 금지한다(페르소나 아바타는 예외).

## 7.2. 의미별 아이콘 매핑

| 의미 | 아이콘 |
|---|---|
| 추가 | Add24 |
| 그룹 추가 | FolderAdd24 |
| 페르소나 추가 | PersonAdd24 |
| 연결 | Link24 |
| 연결 해제 | Dismiss24 |
| 편집 | Edit24 |
| 삭제 | Delete24 |
| 새로고침·재시드 | ArrowSync24 |
| 폴더 열기 | FolderOpen24 |
| 뒤로가기 | ArrowLeft24 |
| 경고·오류 | Warning24 |
| 이미지 업로드 | Image24 |
| 이미지 갤러리 | AppsList24 |

. 동일 의미에 다른 아이콘을 혼용하는 것을 금지한다(예: 연결에 Plug24/Link24 혼용 금지).

---

# 8. 페르소나 컬러 시스템

## 8.1. 컬러 키

페르소나 카드·아바타 보더 색상은 페르소나의 `Color` 필드로 결정된다.
지원 키: `blue`, `orange`, `yellow`, `green`, `red`, `gold`.

| 키 | 매핑 토큰 |
|---|---|
| blue | PawPlanningBlue |
| orange | PawImplOrange |
| yellow | PawArchYellow |
| green | PawSystemGreen |
| red | PawErrorRed |
| gold | PawGold |

. 변환 책임은 `PersonaColorToBrushConverter`가 단일하게 담당한다. 코드 비하인드에서 직접 색상 매핑을 금지한다.

---

# 9. 컨버터 (Helpers)

UI 분기 로직은 ViewModel·Code-behind가 아닌 Converter로 처리한다.

| Converter | 역할 |
|---|---|
| BoolToVisibilityConverter | bool → Visible/Collapsed |
| InverseBoolToVisibilityConverter | bool → Collapsed/Visible |
| InverseBoolConverter | bool 반전 |
| NullToCollapsedConverter | null → Collapsed |
| ZeroToVisibleConverter | 0/빈 컬렉션 → Visible (빈 상태용) |
| EmptyToVisibleConverter | 빈 문자열 → Visible (폴백 노출용) |
| NotEmptyToVisibleConverter | 비어있지 않으면 Visible |
| RoleToVisibilityConverter | 메시지 Role 분기 |
| ForcePersonaTextConverter | Auto/고정 모드 텍스트 토글 |
| BoolToToggleTextConverter | 토글 텍스트 변환 |
| AvatarToImageConverter | 파일 경로/data URI → BitmapImage |
| IconNameToSymbolConverter | 아이콘 키 → SymbolRegular |
| PersonaColorToBrushConverter | 페르소나 컬러 키 → SolidColorBrush |

. 신규 분기 로직은 우선 기존 Converter 재사용을 검토한다. 단순 bool 기반 Visibility 분기에 Code-behind 사용을 금지한다.

---

# 10. 레이아웃 그리드

. 페이지 본문 좌우 여백 : `Margin="24,16"` 표준.
. 카드 내부 콘텐츠 폭 상한 : `MaxWidth="800"` (가독성).
. 헤더와 콘텐츠 사이 간격 : `Margin="0,0,0,16"`.
. 입력 필드 간 간격 : `Margin="0,0,0,10"` (라벨 + 입력 한 묶음).
. 라벨과 입력 사이 간격 : `Margin="0,0,0,4"`.

---

# 11. 변경 이력

| 버전 | 날짜 | 변경 내용 |
|---|---|---|
| v0.1.0 | 2026-04-15 | 최초 작성. §2 컬러 토큰, §3 타이포그래피, §4 종이 텍스처, §5 카드 스타일, §6 컴포넌트(아바타·ProgressRing·Z-Index·ESC·빈 상태·Placeholder·버튼 순서·페르소나 연결), §7 아이콘, §8 페르소나 컬러, §9 컨버터, §10 레이아웃 그리드 |
