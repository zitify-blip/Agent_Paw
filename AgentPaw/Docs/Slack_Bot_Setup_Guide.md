# Slack Bot 연동 가이드

Agent Paw 데스크톱 앱에서 Slack Bot을 연결하는 전체 과정을 안내한다.

---

## 사전 요구사항

- Slack 워크스페이스 관리자 권한 (또는 앱 설치 허용된 계정)
- Agent Paw 앱 설치 완료 및 로그인 상태

---

## 1. Slack App 생성

1. https://api.slack.com/apps 접속
2. **"Create New App"** 클릭
3. **"From scratch"** 선택
4. 입력:
   - **App Name**: `Agent Paw`
   - **Pick a workspace**: 연결할 Slack 워크스페이스 선택
5. **"Create App"** 클릭

---

## 2. Socket Mode 활성화

Socket Mode를 사용하면 공개 URL 없이 데스크톱 앱에서 직접 메시지를 수신할 수 있다.

1. 좌측 메뉴 → **Settings** → **Socket Mode**
2. **"Enable Socket Mode"** 토글 ON
3. App-Level Token 생성 팝업이 나타남:
   - **Token Name**: `agent-paw-socket`
   - **Add Scope**: `connections:write` 선택
4. **"Generate"** 클릭
5. 생성된 **App-Level Token** (`xapp-...`) 을 복사하여 안전하게 보관

> 이 토큰은 한 번만 표시된다. 분실 시 재생성 필요.

---

## 3. Bot Token Scopes 설정

1. 좌측 메뉴 → **Features** → **OAuth & Permissions**
2. **"Scopes"** 섹션으로 스크롤
3. **Bot Token Scopes** 에서 **"Add an OAuth Scope"** 클릭
4. 아래 scope를 하나씩 추가:

| Scope | 용도 |
|---|---|
| `chat:write` | 봇이 채널에 메시지 전송 |
| `channels:read` | 공개 채널 목록 조회 |
| `channels:history` | 공개 채널 메시지 수신 |
| `groups:read` | 비공개 채널 목록 조회 |
| `groups:history` | 비공개 채널 메시지 수신 |

---

## 4. Event Subscriptions 설정

1. 좌측 메뉴 → **Features** → **Event Subscriptions**
2. **"Enable Events"** 토글 ON
3. **"Subscribe to bot events"** 섹션에서 **"Add Bot User Event"** 클릭
4. 아래 이벤트를 추가:

| Event | 설명 |
|---|---|
| `message.channels` | 공개 채널에 메시지가 올 때 |
| `message.groups` | 비공개 채널에 메시지가 올 때 |

5. 하단 **"Save Changes"** 클릭

---

## 5. 앱 설치 (워크스페이스에 봇 추가)

1. 좌측 메뉴 → **Settings** → **Install App**
2. **"Install to Workspace"** 클릭
3. 권한 요청 화면에서 **"Allow"** 클릭
4. 설치 완료 후 **Bot User OAuth Token** (`xoxb-...`) 이 표시됨
5. 이 토큰을 복사하여 안전하게 보관

> 이미 설치된 상태에서 scope를 변경했다면 **"Reinstall to Workspace"** 를 눌러 재설치해야 한다.

---

## 6. Agent Paw 앱에서 토큰 입력

1. Agent Paw 앱 실행 → 좌측 메뉴 **Settings** 클릭
2. **Slack Bot** 섹션으로 스크롤
3. **Bot Token** 입력란에 `xoxb-...` 토큰 붙여넣기 → **"저장"** 클릭
4. **App-Level Token** 입력란에 `xapp-...` 토큰 붙여넣기 → **"저장"** 클릭
5. **"활성화"** 버튼 클릭
6. 상태 표시등이 초록색 **"실행 중"** 으로 변경되면 연결 성공

---

## 7. Slack 채널에 봇 추가

봇이 메시지를 수신하려면 채널에 직접 추가해야 한다.

1. Slack 앱 → 원하는 채널 열기
2. 채널 상단 이름 클릭 → **"통합"** 탭 (또는 **"Integrations"**)
3. **"앱 추가"** (Add an App) 클릭
4. `Agent Paw` 검색 → **"추가"** 클릭

또는 채널에서 직접 입력:

```
/invite @Agent Paw
```

---

## 8. 채널 등록 확인

1. Agent Paw 앱 → Settings → **Slack Channels** 섹션
2. **"Refresh"** 클릭 → 봇이 참여한 채널 목록이 표시됨
3. 응답을 원하는 채널을 **"활성화"** 토글

> 봇이 채널에 참여하면 자동으로 등록되기도 하지만, Refresh를 눌러 수동 동기화하는 것을 권장한다.

---

## 9. 테스트

Slack 채널에서 메시지 입력:

```
/help
```

Agent Paw 명령어 목록이 표시되면 연동 성공이다.

```
/projects
/project 내프로젝트
안녕하세요, 오늘 할 일을 정리해주세요
```

---

## 사용 가능한 명령어

| 명령어 | 동작 |
|---|---|
| `/help` | 명령어 목록 표시 |
| `/projects` | 활성 프로젝트 목록 |
| `/project <이름>` | 프로젝트 전환 |
| `/project current` | 현재 프로젝트 확인 |
| `/personas` | 페르소나 목록 |
| `/persona <이름>` | 페르소나 고정 |
| `/persona auto` | 자동 분류로 전환 |
| `/reset` | 세션 초기화 |

명령어 없이 일반 메시지를 보내면 AI가 응답한다.

---

## 문제 해결

### 봇이 응답하지 않음

| 확인 항목 | 해결 |
|---|---|
| Settings에서 Slack Bot 상태가 "실행 중"인가? | "활성화" 클릭 또는 "Restart" 클릭 |
| Bot Token / App Token이 모두 저장되어 있는가? | 두 토큰 모두 입력 후 저장 |
| Slack 채널에 봇이 추가되어 있는가? | 채널에서 `/invite @Agent Paw` 실행 |
| Slack Channels 목록에서 해당 채널이 "활성화" 상태인가? | Refresh 후 활성화 토글 |
| Agent Paw 앱이 실행 중인가? | Socket Mode는 앱이 켜져 있어야 동작 |
| Agent Paw에 로그인되어 있는가? | 로그인 후 재시도 |
| 프로젝트가 설정되어 있는가? | `/projects` → `/project <이름>` 으로 설정 |

### 토큰 오류

| 증상 | 원인 | 해결 |
|---|---|---|
| "invalid_auth" | Bot Token이 잘못됨 | OAuth & Permissions 페이지에서 토큰 재복사 |
| 연결 즉시 끊김 | App-Level Token이 잘못됨 | Socket Mode 페이지에서 토큰 재생성 |
| "missing_scope" | 필요한 scope가 없음 | OAuth & Permissions에서 scope 추가 후 **Reinstall** |

### Scope 변경 후 반영 안 됨

Scope를 추가/변경한 후에는 반드시 **Reinstall to Workspace** 를 해야 한다.

1. 좌측 메뉴 → **Install App**
2. **"Reinstall to Workspace"** 클릭
3. 새로운 Bot Token이 발급됨 → Agent Paw 앱에서 기존 토큰 삭제 후 재입력

---

## 아키텍처 참고

```
Slack 채널 메시지
    ↓
Slack Socket Mode (WebSocket, 아웃바운드)
    ↓
Agent Paw 앱 (SlackSocketModeService)
    ↓
ChatDispatcherService (명령어/일반 메시지 분류)
    ↓
/명령어 → 즉시 응답  |  일반 메시지 → AI 오케스트레이터
    ↓
SlackChatService → Slack 채널에 응답 전송
```

- **Socket Mode**: 앱에서 Slack으로 아웃바운드 WebSocket 연결을 맺는 방식이다. 공개 URL이나 서버가 필요 없다.
- **Agent Paw 앱이 켜져 있어야** 메시지를 수신하고 응답할 수 있다.
- 각 채널별로 독립적인 세션(프로젝트, 페르소나)이 유지된다.
