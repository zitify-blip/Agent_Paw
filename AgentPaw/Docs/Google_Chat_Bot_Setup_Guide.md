# Google Chat Bot 연동 가이드

Agent Paw 데스크톱 앱에서 Google Chat Bot을 연결하는 전체 과정을 안내한다.

---

## 사전 요구사항

- **Google Workspace 계정** (개인 Gmail은 사용 불가)
- Google Cloud Platform (GCP) 프로젝트 생성 권한
- Agent Paw 앱 설치 완료 및 로그인 상태

---

## 1. GCP 프로젝트 생성

1. https://console.cloud.google.com 접속
2. 상단 바에서 프로젝트 선택 드롭다운 클릭
3. **"새 프로젝트"** 클릭
4. 입력:
   - **프로젝트 이름**: `agent-paw` (원하는 이름)
5. **"만들기"** 클릭
6. 생성 완료 후 해당 프로젝트가 선택된 상태인지 확인

---

## 2. API 활성화

2개의 API를 활성화해야 한다.

### 2-1. Google Chat API

1. GCP Console 상단 검색창에 `Google Chat API` 입력
2. 검색 결과에서 **Google Chat API** 클릭
3. **"사용"** 클릭

### 2-2. Cloud Pub/Sub API

1. GCP Console 상단 검색창에 `Pub/Sub` 입력
2. 검색 결과에서 **Cloud Pub/Sub API** 클릭
3. **"사용"** 클릭 (이미 활성화된 경우 "관리" 표시)

---

## 3. 서비스 계정 생성

서비스 계정은 Agent Paw 앱이 GCP 리소스에 접근하기 위한 인증 수단이다.

### 3-1. 계정 생성

1. GCP Console 좌측 메뉴 → **IAM 및 관리자** → **서비스 계정**
   (또는 상단 검색창에 `서비스 계정` 입력)
2. 상단 **"+ 서비스 계정 만들기"** 클릭
3. 입력:
   - **서비스 계정 이름**: `agent-paw-bot`
   - **서비스 계정 ID**: 자동으로 채워짐
   - **설명**: `Agent Paw Google Chat Bot` (선택사항)
4. **"만들고 계속하기"** 클릭

### 3-2. 역할 부여

5. **"역할 선택"** 드롭다운 클릭
6. 검색창에 `pubsub` 또는 `편집자` 입력
7. **Pub/Sub 편집자** (`roles/pubsub.editor`) 선택
   - 이 역할 하나로 구독자 + 뷰어 권한이 모두 포함된다
8. **"계속"** → **"완료"** 클릭

> 역할 검색 시 `pubsub`, `editor`, `편집자` 등으로 검색하면 나타난다.
> "Pub/Sub 구독자" 역할이 검색되지 않는 경우 "Pub/Sub 편집자"를 사용하면 된다.

### 3-3. JSON 키 다운로드

9. 서비스 계정 목록에서 방금 만든 `agent-paw-bot@프로젝트ID.iam.gserviceaccount.com` 행 클릭
10. 상단 탭에서 **"키"** 클릭
11. **"키 추가"** → **"새 키 만들기"**
12. 키 유형: **JSON** 선택
13. **"만들기"** 클릭
14. `.json` 파일이 자동 다운로드됨

> 이 JSON 파일을 안전하게 보관한다. 나중에 Agent Paw 앱에 업로드한다.
> 키는 한 번만 다운로드 가능하다. 분실 시 새 키를 생성해야 한다.

---

## 4. Pub/Sub Topic 생성

Pub/Sub는 Google Chat에서 보낸 메시지를 Agent Paw 앱으로 전달하는 통로이다.

### 4-1. Topic 생성

1. GCP Console 상단 검색창에 `Pub/Sub` 입력 → 클릭
2. **"주제(Topics)"** 페이지에서 **"+ 주제 만들기"** 클릭
3. 입력:
   - **주제 ID**: `agent-paw-chat`
   - **기본 구독 추가**: 체크 **해제**
4. **"만들기"** 클릭
5. 생성된 주제의 전체 경로를 복사해둔다:
   ```
   projects/프로젝트ID/topics/agent-paw-chat
   ```

### 4-2. Subscription 생성

6. 좌측 **"구독(Subscriptions)"** 클릭
7. **"+ 구독 만들기"** 클릭
8. 입력:
   - **구독 ID**: `agent-paw-pull`
   - **Cloud Pub/Sub 주제 선택**: 방금 만든 `agent-paw-chat` 선택
   - **전송 유형**: **Pull** (기본값 그대로)
   - **만료**: **"만료되지 않음"** 으로 변경 권장
9. **"만들기"** 클릭
10. 구독 전체 경로를 복사해둔다:
    ```
    projects/프로젝트ID/subscriptions/agent-paw-pull
    ```

---

## 5. Topic에 Chat API 게시 권한 부여

Google Chat이 Pub/Sub Topic에 메시지를 발행할 수 있도록 권한을 부여한다. 이 단계를 빠뜨리면 봇이 메시지를 수신하지 못한다.

1. **Pub/Sub** → **주제** → `agent-paw-chat` 클릭
2. 우측에 **정보 패널** 표시 (안 보이면 우상단 **"정보 패널 표시"** 클릭)
3. **"주 구성원 추가"** 클릭
4. 입력:
   - **새 주 구성원**: `chat-api-push@system.gserviceaccount.com`
   - **역할**: 검색창에 `게시자` 또는 `publisher` 입력 → **Pub/Sub 게시자** 선택
5. **"저장"** 클릭

> `chat-api-push@system.gserviceaccount.com` 은 Google이 관리하는 시스템 계정이다. 직접 생성하는 것이 아니다.

---

## 6. Google Chat API 봇 구성

### 6-1. 구성 페이지 진입

1. GCP Console 상단 검색창에 `Google Chat API` 입력 → 클릭
2. **"관리"** 클릭
3. 상단 탭 중 **"구성(Configuration)"** 클릭

> "구성" 탭이 보이지 않으면 API가 활성화되지 않은 상태이다. 2번 단계를 확인한다.

### 6-2. 구성 입력

| 필드 | 입력값 |
|---|---|
| **앱 이름** | `Agent Paw` |
| **아바타 URL** | 비워도 됨 (선택사항) |
| **설명** | `AI 에이전트 봇` |

#### 기능 (체크박스)

- ✅ **1:1 메시지 수신** (Receive 1:1 messages)
- ✅ **스페이스 및 그룹 대화 참여** (Join spaces and group conversations)

#### 연결 설정 (Connection settings)

- **Cloud Pub/Sub topic name** 선택 (앱 URL 아님)
- Topic 경로 입력:
  ```
  projects/프로젝트ID/topics/agent-paw-chat
  ```

#### 공개 상태 (Visibility)

- **"조직 내 특정 사용자 및 그룹에서 이 Chat 앱을 사용할 수 있도록 설정"** 선택
- 아래 이메일 입력란에 **본인 Google Workspace 이메일** 추가

#### 로그

- **Cloud Logging에 오류 로깅** → ✅ 체크 권장

### 6-3. 저장

하단 **"저장(Save)"** 클릭

> 저장 후 상단에 "앱 상태: 라이브" 표시되면 구성 완료이다.

---

## 7. Agent Paw 앱에서 설정 입력

1. Agent Paw 앱 실행 → 좌측 메뉴 **Settings** 클릭
2. **Google Chat Bot** 섹션으로 스크롤
3. 입력:

| 필드 | 입력값 |
|---|---|
| **GCP Project ID** | GCP 프로젝트 ID (예: `agent-paw-12345`) |
| **Pub/Sub Topic** | `projects/프로젝트ID/topics/agent-paw-chat` |
| **Pub/Sub Subscription** | `projects/프로젝트ID/subscriptions/agent-paw-pull` |

4. **"Upload JSON"** 클릭 → 3-3에서 다운받은 서비스 계정 JSON 파일 선택
5. **"Save Config"** 클릭
6. **"활성화"** 버튼 클릭
7. 상태 표시등이 초록색 **"실행 중"** 으로 변경되면 연결 성공

> GCP Project ID는 GCP Console 상단 프로젝트 선택 드롭다운에서 확인할 수 있다.
> 프로젝트 이름이 아니라 프로젝트 **ID** 를 입력해야 한다.

---

## 8. Google Chat에서 봇 추가

### 8-1. 스페이스에 봇 추가

1. https://chat.google.com 접속 (또는 Google Chat 앱)
2. 좌측 **"스페이스"** → 기존 스페이스 열기 또는 **"스페이스 만들기"**
3. 스페이스 상단 이름 클릭 → **"앱 및 통합"** (또는 **"Integrations"**)
4. **"앱 추가"** 클릭
5. `Agent Paw` 검색 → **추가**

### 8-2. DM으로 봇 사용

1. Google Chat 좌측 **"채팅"** → **"새 채팅"**
2. `Agent Paw` 검색 → 선택
3. 1:1 대화에서 봇과 직접 대화 가능

---

## 9. Space 등록 확인

1. Agent Paw 앱 → Settings → **Google Chat Spaces** 섹션
2. **"Refresh"** 클릭 → 봇이 참여한 Space 목록이 표시됨
3. 응답을 원하는 Space를 **활성화** 토글

---

## 10. 테스트

Google Chat 스페이스 또는 DM에서 입력:

```
@Agent Paw /help
```

Agent Paw 명령어 목록이 표시되면 연동 성공이다.

```
@Agent Paw /projects
@Agent Paw /project 내프로젝트
@Agent Paw 안녕하세요, 오늘 할 일을 정리해주세요
```

> Google Chat에서는 봇에게 메시지를 보낼 때 `@Agent Paw` 멘션이 필요하다.

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
| Settings에서 Google Chat Bot 상태가 "실행 중"인가? | "활성화" 클릭 또는 "Restart PubSub" 클릭 |
| Save Config을 눌렀는가? | 값 입력 후 반드시 Save Config 클릭 |
| 서비스 계정 JSON이 업로드되어 있는가? | Upload JSON으로 파일 선택 |
| Google Chat Spaces 목록에서 해당 Space가 "활성화" 상태인가? | Refresh 후 활성화 토글 |
| Agent Paw 앱이 실행 중인가? | Pub/Sub Pull 방식은 앱이 켜져 있어야 동작 |
| Agent Paw에 로그인되어 있는가? | 로그인 후 재시도 |
| 프로젝트가 설정되어 있는가? | `/projects` → `/project <이름>` 으로 설정 |

### Google Chat에서 봇 검색이 안 됨

| 원인 | 해결 |
|---|---|
| Chat API 공개 상태에 본인 이메일이 없음 | 6번 구성 → 공개 상태에서 이메일 추가 후 저장 |
| Google Workspace 계정이 아닌 개인 Gmail 사용 | Google Chat API 봇은 Workspace 계정에서만 사용 가능 |
| Chat API 구성을 저장하지 않음 | 구성 페이지에서 "저장" 클릭 확인 |

### Pub/Sub 관련 오류

| 증상 | 원인 | 해결 |
|---|---|---|
| "권한 거부" | 서비스 계정에 Pub/Sub 편집자 역할 없음 | 3-2 역할 부여 확인 |
| 메시지 수신 안 됨 | Topic에 `chat-api-push` 게시 권한 누락 | 5번 게시 권한 부여 확인 |
| "구독을 찾을 수 없음" | Subscription 경로가 잘못됨 | 전체 경로 형식 확인: `projects/ID/subscriptions/이름` |

### 권한 요약

| 대상 | 역할 | 부여 위치 |
|---|---|---|
| **내 서비스 계정** (`agent-paw-bot@...`) | Pub/Sub 편집자 | IAM (프로젝트 수준) |
| **Google 시스템** (`chat-api-push@system.gserviceaccount.com`) | Pub/Sub 게시자 | Topic 권한 패널 |

---

## 비용

| 항목 | 무료 범위 | 비고 |
|---|---|---|
| Google Chat API | 무료 (GWS에 포함) | Google Workspace 구독 필요 |
| Cloud Pub/Sub | 월 10GB 무료 | 채팅 메시지는 극소량 |
| 서비스 계정 | 무료 | |

일반적인 챗봇 사용은 무료 범위 내에서 충분하다.

---

## 아키텍처 참고

```
Google Chat Space에서 @Agent Paw 멘션
    ↓
Google Chat API → Pub/Sub Topic에 메시지 발행
    ↓
Agent Paw 앱 (PubSubPullService가 Subscription에서 Pull)
    ↓
ChatDispatcherService (명령어/일반 메시지 분류)
    ↓
/명령어 → 즉시 응답  |  일반 메시지 → AI 오케스트레이터
    ↓
GoogleChatService → Google Chat Space에 응답 전송
```

- **Pub/Sub Pull 방식**: Agent Paw 앱이 주기적으로 Subscription에서 메시지를 가져오는 방식이다.
- **Agent Paw 앱이 켜져 있어야** 메시지를 수신하고 응답할 수 있다.
- 각 Space별로 독립적인 세션(프로젝트, 페르소나)이 유지된다.
