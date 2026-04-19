# Telegram Bot 연동 가이드

Agent Paw 데스크톱 앱에서 Telegram Bot을 연결하는 전체 과정을 안내한다.
Google Chat과 달리 **Google Workspace 같은 유료 계정 없이 개인 텔레그램 계정만으로** 구축 가능하다.

---

## 사전 요구사항

- **Telegram 계정** (앱 또는 웹)
- Agent Paw 앱 설치 완료 및 로그인 상태
- 인터넷 연결 (long polling 방식이므로 공인 IP·공개 URL 불필요)

---

## 1. BotFather로 봇 생성

Telegram에서 봇은 **@BotFather** 라는 공식 봇을 통해 발급한다.

1. Telegram 앱에서 상단 검색창에 `@BotFather` 입력
2. 인증 마크(흰 체크) 달린 공식 계정 열기 → **"Start"** 클릭
3. 메시지 입력창에 `/newbot` 전송
4. BotFather가 봇 이름(Display name)을 묻는다 — 예: `Agent Paw`
5. 다음으로 봇 username을 묻는다 — **반드시 `bot` 으로 끝나야 한다**
   - 예: `agent_paw_bot`, `my_company_paw_bot`
6. 성공하면 BotFather가 아래 형식의 토큰을 응답한다:

   ```
   1234567890:ABCdefGHIjklMNOpqrsTUVwxyz_12345678
   ```

> 이 토큰은 **비밀번호에 준한다**. 외부에 노출 금지. 노출 시 BotFather에서 `/revoke` → `/token` 으로 재발급해야 한다.

---

## 2. (권장) 봇 프라이버시 모드 해제

기본값으로 봇은 그룹에서 **자신에게 직접 멘션된 메시지만** 수신한다. Agent Paw는 그룹 대화에서도 `@봇이름` 멘션 시에만 반응하므로 이 설정은 그대로 둬도 무방하다.
그룹 내 모든 메시지를 수신하길 원하면 아래를 수행한다.

1. BotFather에 `/mypots` 전송 → 봇 선택
2. **"Bot Settings"** → **"Group Privacy"** → **"Turn off"**

> AgentPaw는 그룹에서는 `@봇이름` 멘션이 포함된 메시지에만 응답하도록 구현되어 있다.
> Privacy mode를 켠 상태(기본값) + `@멘션` 방식을 권장한다. 불필요한 호출·비용을 방지한다.

---

## 3. (선택) 봇 설명·사진 설정

1. BotFather에 `/setdescription` → 봇 선택 → 한 줄 설명 입력
2. `/setabouttext` → 프로필 상단 표시 문구
3. `/setuserpic` → 프로필 이미지 업로드

---

## 4. Agent Paw 앱에서 토큰 등록

1. Agent Paw 실행 → 좌측 메뉴 **Settings** 클릭
2. **Telegram 봇** 카드로 스크롤
3. **봇 토큰** 입력란에 1단계에서 받은 토큰 붙여넣기 → **저장**
4. 저장 성공 시 상태 표시 옆에 `@봇이름` 이 자동으로 노출된다
   - 자동 노출이 안 되면 토큰 오타·개행 문자 유입을 의심한다
5. **활성화** 버튼 클릭 → 상태 dot이 초록색 **"실행 중"** 으로 변경되면 연결 성공

---

## 5. 봇을 채팅에 추가

### 5-1. 1:1 대화 (DM)

1. Telegram 앱에서 상단 검색창에 `@봇이름` 입력 (예: `@agent_paw_bot`)
2. 봇 선택 → **"Start"** 클릭
3. 메시지를 보내면 봇이 응답한다

### 5-2. 그룹 대화

1. 그룹 상세 → **"Add members"** → `@봇이름` 검색 후 추가
2. 그룹 내에서 봇을 호출하려면 반드시 `@봇이름` 멘션을 포함한다

   ```
   @agent_paw_bot 오늘 배포 상황 요약해줘
   ```

> 그룹에서 멘션 없이 메시지를 보내면 봇이 응답하지 않는다. 이는 의도된 동작이다.

---

## 6. 채팅 등록 확인

1. Telegram에서 봇에게 메시지를 1회 전송한다 (DM이든 그룹이든)
2. Agent Paw → Settings → **Telegram 채팅** 섹션 → **새로고침** 클릭
3. 해당 채팅이 목록에 자동으로 나타난다
4. 응답을 받고 싶은 채팅의 토글을 **활성화** 상태로 둔다

> 채팅 자동 등록은 최초 메시지 수신 시 수행된다. 봇을 그룹에 추가만 하고 메시지를 보내지 않으면 목록에 나타나지 않는다.

---

## 7. 동작 확인

DM 또는 그룹에서 다음 형태로 테스트한다.

```
(DM)      안녕
(그룹)     @agent_paw_bot 안녕
```

정상 동작 시 AgentPaw의 오케스트레이터 파이프라인이 메시지를 처리하고 페르소나가 응답을 작성하여 같은 채팅에 회신한다.

---

## 문제 해결

| 증상 | 원인 | 해결 |
|---|---|---|
| 저장 후 `@봇이름` 미노출 | 토큰 오타 / 네트워크 오류 | 토큰 재발급 또는 재저장, 인터넷 확인 |
| 상태가 "활성 (미실행)" 고정 | 토큰 설정 직후 자동 시작 실패 | **재시작** 버튼 클릭 |
| 메시지 무응답 (DM) | 채팅이 Settings에 등록·활성화되지 않음 | 6단계 수행 |
| 메시지 무응답 (그룹) | 멘션 누락 / 봇이 그룹에 없음 | `@봇이름` 포함 여부 확인, 봇이 멤버인지 확인 |
| "Conflict: terminated by other getUpdates" | 같은 토큰을 다른 곳에서도 polling 중 | 다른 AgentPaw 인스턴스·서버 종료 후 **재시작** |
| 토큰 노출 | GitHub 등에 실수로 push | BotFather에서 `/revoke` → `/token` 재발급 후 앱에 재등록 |

---

## 아키텍처 요약

```
Telegram ── long poll ──▶ AgentPaw 앱 (TelegramPollingService)
                              │
                              ▼
                       ChatDispatcherService
                              │
                              ▼
                       OrchestratorService
                              │
                              ▼
                       AgentPaw 앱 ── sendMessage ──▶ Telegram
```

- **Long polling 기반**: 공개 URL·웹훅 불필요. 앱 실행 중에만 메시지 수신.
- **토큰 암호화 저장**: `TELEGRAM_BOT_TOKEN`은 `ChatBotConfigService`가 DPAPI로 암호화 저장.
- **공통 메시지 처리**: `IChatPlatformSender` 구현체로 Google Chat·Slack과 동일 파이프라인 사용.
