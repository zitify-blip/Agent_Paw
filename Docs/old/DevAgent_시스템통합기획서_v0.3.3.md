# Multi-Model Product Dev-Agent 시스템통합기획서

| 항목 | 내용 |
|---|---|
| 문서 버전 | v0.3.3 |
| 작성일 | 2026-04-10 |
| 작성자 | 안승준 |
| 문서 상태 | 🔄 작성 중 |
| 참조 문서 | CON-001 서비스 컨셉 정의서 v2.0 |

---

# 변경 이력

| 버전 | 날짜 | 작성자 | 변경 내용 |
|---|---|---|---|
| v0.3.4 | 2026-04-11 | 안승준 | §4.6 CLI 호출 방식에 Windows 호환성 반영 (`shell: true`, `SIGKILL` → 크로스플랫폼 `kill()`) |
| | | | §7.2 모델별 역할 매트릭스 현행화 — Claude Sonnet 4.6·Opus 4.6, Gemini 2.5 Pro·2.5 Flash·2.0 Flash Lite로 갱신 |
| | | | §12 WebSocket 연결 시 토큰 인증 의무화 및 userId 스푸핑 방지 정책 추가 |
| | | | §12.2 알림 프로토콜에 역할 기반 수신자 필터링 구현 반영 (기존 명세와 코드 동기화) |
| | | | §13.3 구글 스페이스 OAuth 팝업 흐름 구현 완료 (TODO 해소), 연결 상태 DB 영속화 및 앱 재시작 시 복원 |
| | | | §13.3 API 키 입력 폼 UX 개선 — 등록 완료 시 마스킹 상태 표시, 변경 모드 분리 |
| | | | §15.5 CLAUDE_CLI_ENABLE·CLAUDE_CLI_DISABLE 감사 로그 코드 구현 완료 (기존 명세와 동기화) |
| v0.3.3 | 2026-04-10 | 안승준 | §4.6 Claude Code CLI 서브프로세스 인증 정책 신설 (API 키와 병행 사용, CLI 우선 → API 키 폴백) |
| | | | §7.2 모델별 역할 매트릭스에 Claude CLI 호출 경로 주석 추가 |
| | | | §13.3 설정 화면에 Claude Code CLI 토글·상태 표시 UI 컴포넌트 추가 |
| | | | §15.5 감사 로그에 CLAUDE_CLI_ENABLE·CLAUDE_CLI_DISABLE 액션 추가 |
| | | | §5 아키텍처 구조도에 CLI Subprocess 경로 추가 |
| v0.3.2 | 2026-04-10 | 안승준 | Major 7건+Minor 4건 검토 반영: §8.3 멤버 초대 AUDIT_LOG INSERT 명시, §13.3 구글 스페이스 연결/해제 AUDIT_LOG INSERT 명시 |
| | | | §15.5 감사 로그에 PROJECT_CREATE·PROJECT_DELETE·PROJECT_ARCHIVE·PROJECT_RESTORE 4개 액션 추가 |
| | | | §12 모듈 개요에 Viewer 추가, 다중 프로젝트 대상 선택을 위한 `/project` 명령어 및 활성 프로젝트 정책 신설 |
| | | | §8.2 Child 생성 시 부모 프로젝트 Editor 이상 권한 명시, §10.3 롤백 PB 백업 체크박스 조건 분기 추가 |
| | | | §14.8 MODEL_CONFIG에 created_at·updated_at 추가, §11 위키 Viewer 읽기 전용 정책 단락 추가 |
| | | | §13.2 세션 초과 트리거 표현 통일(3개를 초과), §4.7 MANUAL 스냅샷 포화 시 생성 차단 정책 추가 |
| v0.3.1 | 2026-04-10 | 안승준 | Critical 2건+Major 5건+Minor 2건 검토 반영: §4.1 프로젝트 생성 권한 순환 논리 해소(시스템 레벨 기능으로 분리, 모든 인증 User 허용) |
| | | | §4.1 매트릭스에서 "프로젝트 생성" 열 제거, "구글 스페이스 리모트 제어"를 쓰기/읽기 명령으로 분리, Viewer ※ 주석 정비 |
| | | | §13.2 로그인 PB 6단계에 AUDIT_LOG LOGIN 이벤트 기록 추가, §4.6 다중 기기 세션 일괄 종료 주체를 "본인(역할 무관)"으로 §13.3과 동기화 |
| | | | §8.2 프로젝트 생성 PB에 PROJECT_MEMBER·WORKSPACE·MODEL_CONFIG 자동 INSERT 명세 추가 |
| | | | §10 타임라인 Viewer 읽기 전용 접근 정책 추가, §12.1 `/rollback` Owner 전용 근거(원격 오조작 방지) 명시 |
| | | | §14.6 SNAPSHOT에 created_by FK 추가, §12 PC 앱 오프라인 시 구글 스페이스 명령 즉시 에러 회신 정책 추가 |
| v0.3.0 | 2026-04-10 | 안승준 | Critical 4건+Major 4건+Minor 2건 검토 반영: §8.1 PROJECT_MEMBER 기반 필터링 수정, §8.4 Owner 양도 PB에 PROJECT.owner_user_id 갱신 추가 |
| | | | §14.4 AUDIT_LOG에 project_id FK 추가, §8.5 프로젝트 삭제를 논리 삭제(DELETED) 후 비동기 물리 정리 방식으로 전환 |
| | | | §14.2 USER.is_active 갱신 주체를 본인/시스템으로 한정(프로젝트 Owner 전역 비활성화 불가), §15.4·§13.2 에러 메시지 동기화 |
| | | | §13.3 "모든 기기 로그아웃" 역할 제한 해제(본인이면 활성화), §9 Viewer 읽기 전용 워크스페이스 접근 정책 신설 |
| | | | §4.6 JWT 런타임 만료 시 선제 갱신(만료 5분 전) 및 Refresh 폴백 로직 추가 |
| | | | §12.1 `/snapshot list` 명령어 신설, §8.3 멤버 초대 알림 정책 추가 |
| v0.2.1 | 2026-04-10 | 안승준 | 내부 일관성 오류 11건 수정: §14.1 PROJECT에 status 컬럼 직접 삽입·is_deleted 제거, §14.5 EVENT_LOG에 triggered_by 직접 삽입, 하단 보완 사항 블록 제거 |
| | | | §13.2 로그인 PB 5-1단계 순서 교정 및 role 잔재 제거, §1 핵심 테이블 목록 갱신(10개), §5 아키텍처 구조도에 Auth Layer 추가 |
| | | | §2 용어 정리에 RBAC·PROJECT_MEMBER·OAuth 2.0·JWT·Service Account·AUDIT_LOG 추가 |
| | | | §8.5 프로젝트 삭제 PB에 AUDIT_LOG·WORKSPACE 삭제 순서 추가, §14.4 AUDIT_LOG target_user_id 설명 보강 |
| v0.2.0 | 2026-04-10 | 안승준 | §4.1 권한 정책을 프로젝트 단위 RBAC으로 전환 (PROJECT_MEMBER 테이블 신설) |
| | | | §4.7 스냅샷 용량 관리 정책 신설 (자동 정리, 보존 기간, 최대 개수) |
| | | | §7.1 오케스트레이터 Classifier 분류 불확실 시 User 확인 요청 추가 |
| | | | §8.3~8.6 멤버 관리·Owner 양도·프로젝트 삭제 PB·보관/복구 PB 신설 |
| | | | §9.1 시각 엔진 비동기 작업 큐 및 Midjourney 대기 UX 추가 |
| | | | §13.2 비활성 계정(is_active=false) 로그인 차단 처리 추가 |
| | | | §14.1 PROJECT에 status 컬럼, §14.2 USER에서 role 제거, §14.5 EVENT_LOG에 triggered_by 추가 |
| | | | §14.9 PROJECT_MEMBER·§14.10 WORKSPACE 테이블 신설 |
| | | | §15.4 사용자 계정 상태·§15.5 감사 로그 액션 유형 신설 |
| v0.1.1 | 2026-04-09 | 안승준 | §4.6 인증 정책 신설 (OAuth 2.0, 토큰 관리, 다중 기기, 구글 스페이스 인증, API 키) |
| | | | §13 모듈 6 인증 및 계정 관리 FSD 신설 (로그인 PB, 설정, 세션 관리, 로그아웃 PB) |
| | | | §14.2 USER 테이블 인증 필드 확장, §14.3 AUTH_TOKEN·§14.4 AUDIT_LOG 테이블 신설 |
| v0.1.0 | 2026-04-09 | 안승준 | 초안 작성 — 전체 아키텍처·정책·FSD·PB·DB 명세 통합 |

---

# Part 1. 개요 및 정책

## 1. 서비스 개요

Multi-Model Product Dev-Agent(이하 DevAgent)는 제품 기획, 시스템 설계, UI/UX 디자인, 소스 코드 구현 및 검증의 전 과정을 복수의 특화 AI 모델(Claude, Gemini, Llama, Midjourney)을 통해 통합 수행하는 일렉트론 기반의 지능형 개발 에이전트 시스템이다.

핵심 컨셉은 하이브리드 에이전틱 워크플로우(Hybrid Agentic Workflow)이다. PC 애플리케이션의 강력한 로컬 제어권과 구글 스페이스의 원격 협업 편의성을 결합하여 제품 개발의 전 주기를 관리한다.

주요 접근 대상: PM, 풀스택 개발자, 1인 창업자, 소규모 팀 리더.
연관 시스템: Claude API, Gemini API, Midjourney API, Llama(로컬), Google Space API, Git.
핵심 테이블: PROJECT, PROJECT_MEMBER, WORKSPACE, MODEL_CONFIG, SNAPSHOT, EVENT_LOG, WIKI_DOCUMENT, USER, AUTH_TOKEN, AUDIT_LOG.

---

## 2. 용어 정리

| 용어 | 정의 |
|---|---|
| DevAgent | 본 시스템의 공식 명칭이다. Multi-Model Product Dev-Agent의 약어이다. |
| 오케스트레이터 | System이 사용자 입력을 분석하여 적절한 AI 모델에 작업을 분배하는 중앙 제어 로직이다. |
| 스냅샷 | 특정 시점의 전체 프로젝트 상태(파일 시스템, 대화 로그, 모델 컨텍스트)를 캡처한 복구 지점이다. |
| 이벤트 소싱 | System이 모든 상태 변화를 순차적인 이벤트 로그로 저장하는 데이터 관리 패턴이다. |
| 프로젝트 하이라키 | 루트(Root) → 부모(Parent) → 자식(Child)의 3계층 데이터 상속 구조이다. |
| 기획 엔진 | PRD, 유즈케이스, User Journey Map을 생성하는 AI 모듈이다. Claude/Gemini를 사용한다. |
| 설계 엔진 | ERD, API 설계, IaC 초안을 작성하는 AI 모듈이다. Claude를 사용한다. |
| 시각 엔진 | 브랜드 가이드, 와이어프레임, UI 목업을 생성하는 AI 모듈이다. Midjourney/Gemini를 사용한다. |
| 구현 엔진 | 코드 생성, 파일 시스템 제어, 단위 테스트 작성을 수행하는 AI 모듈이다. Llama/Claude를 사용한다. |
| ADR | Architecture Decision Record. 아키텍처 의사결정 이력 문서이다. |
| PII | Personally Identifiable Information. 개인 식별 정보이다. |
| Context Injection | System이 이전 단계의 산출물을 다음 단계 모델의 프롬프트에 자동 주입하는 메커니즘이다. |
| RBAC | Role-Based Access Control. 프로젝트 단위로 역할(Owner/Editor/Viewer)에 따라 기능 접근을 제어하는 정책이다. |
| PROJECT_MEMBER | 프로젝트와 User의 다대다(N:M) 매핑 테이블이다. 프로젝트별 역할을 관리한다. |
| OAuth 2.0 | Google 계정 기반 인증 프로토콜이다. DevAgent의 유일한 로그인 수단이다. |
| JWT | JSON Web Token. 앱 세션 토큰으로 사용하며, Client 로컬 스토리지에 암호화 저장한다. |
| Service Account | Google API 서버 간 통신을 위한 인증 방식이다. 구글 스페이스 Bot이 사용한다. |
| AUDIT_LOG | 로그인, 로그아웃, 역할 변경 등 보안 관련 행위를 기록하는 감사 로그 테이블이다. |

---

## 3. 핵심 가치 및 설계 원칙

### 3.1. 핵심 가치

| 가치 | 정의 | 시스템 보장 방식 |
|---|---|---|
| 논리적 일관성 | 기획 → 설계 → 구현 단계를 거치며 데이터 모델과 비즈니스 로직이 변질되지 않는다 | System이 각 단계 산출물을 이전 단계와 자동 대조(Self-Criticism)하여 불일치 시 경고한다 |
| 실행 연속성 | 환경 설정 → 의존성 설치 → 코드 생성 → 빌드 → 테스트의 흐름이 중단되지 않는다 | System이 파이프라인을 자동 실행하고, 실패 시 지수 백오프 재시도 후 Admin에게 알림한다 |
| 지식 영속성 | 결과물뿐 아니라 도출 과정(Chain of Thought)을 모두 저장한다 | System이 모든 이벤트를 Event Sourcing으로 기록하고 시맨틱 검색으로 인출 가능하게 한다 |

### 3.2. 4대 설계 원칙

1. 결정론적 서술 (Deterministic) : 모든 정책에 모호함을 허용하지 않는다. 예외 상황에도 명확한 처리 방안을 결론 내린다.
2. MECE : 중복이나 누락 없이 모든 경우의 수, 권한, 상태 전이를 방어한다.
3. 구조화 및 시각화 : 복잡한 로직은 테이블, 순서도, 진리표로 표현한다.
4. 목적 선행 (Why First) : 기능의 구현 방법(How) 전에 필요성(Why)을 반드시 선행 명시한다.

---

## 4. 글로벌 정책

### 4.1. 권한 정책

DevAgent는 프로젝트 단위의 역할 기반 접근 제어(RBAC)를 적용한다. 동일 User가 프로젝트 A에서는 Owner, 프로젝트 B에서는 Editor 역할을 가질 수 있다. 역할은 PROJECT_MEMBER 테이블에서 프로젝트별로 관리한다.

프로젝트 생성은 시스템 레벨 기능이다. 모든 인증된 User가 새 프로젝트를 생성할 수 있으며, 생성자가 자동으로 해당 프로젝트의 Owner가 된다. 아래 매트릭스는 프로젝트 내 역할별 권한이다.

| 역할 | 코드 | 멤버 초대/관리 | 모델 설정 변경 | 스냅샷 롤백 | 위키 편집 | 구글 스페이스 쓰기 명령 | 구글 스페이스 읽기 명령 |
|---|---|---|---|---|---|---|---|
| Owner | R001 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Editor | R002 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Viewer | R003 | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

단, Owner 권한은 프로젝트당 1명만 보유 가능하다. System이 프로젝트 생성 시 생성자를 Owner로 PROJECT_MEMBER에 자동 INSERT한다.
※ 구글 스페이스 쓰기 명령: `/run`, `/snapshot`, `/rollback`. 읽기 명령: `/status`, `/wiki`, `/snapshot list`.
※ Viewer는 읽기 전용이며, 쓰기 명령을 전송하면 System이 거부한다.
※ 프로젝트에 소속되지 않은 User는 해당 프로젝트에 접근이 불가하다.

### 4.2. 타임존 정책

. 모든 타임스탬프는 UTC+0 기준으로 저장한다.
. Client는 User의 로컬 타임존으로 변환하여 출력한다.
. 예시 : KST 기준 2026-04-09 00:00:00 = UTC+0 기준 2026-04-08 15:00:00

### 4.3. 보안 정책

. 로컬 데이터 암호화 : AES-256 방식으로 암호화한다.
. PII 자동 마스킹 : AI 모델 전송 전 System이 개인정보를 자동 마스킹한다.
. API 키 관리 : 모든 API 키는 OS 키체인에 저장하며, 평문 저장을 금지한다.

### 4.4. 비용 최적화 정책

System이 작업의 중요도에 따라 AI 모델을 자동 할당한다.

| 작업 중요도 | 할당 모델 | 기준 |
|---|---|---|
| Critical (아키텍처 설계, 보안 검증) | Claude Opus / Gemini 1.5 Pro | 정확성 최우선, 비용 무관 |
| Standard (코드 생성, 문서화) | Claude Sonnet / Llama 3 | 정확성과 비용의 균형 |
| Lightweight (포맷팅, 요약, 분류) | Gemini Flash | 비용 최소화, 속도 우선 |

### 4.5. 에러 처리 정책

| 에러 유형 | System 동작 | 재시도 | 알림 |
|---|---|---|---|
| AI 모델 응답 실패 (4xx) | System이 요청 파라미터를 검증 후 재전송한다 | 최대 3회, 지수 백오프(1초→2초→4초) | 3회 실패 시 구글 스페이스로 Admin에게 알림 |
| AI 모델 응답 실패 (5xx) | System이 대체 모델로 폴백(Fallback)한다 | 대체 모델로 1회 시도 | 즉시 알림 |
| 로컬 파일 시스템 오류 | System이 작업을 중단하고 마지막 스냅샷으로 복구한다 | 불가 | 즉시 알림 + 자동 롤백 |
| 네트워크 단절 | System이 로컬 큐에 작업을 적재하고, 복구 시 순차 실행한다 | 네트워크 복구 후 자동 | 30초 이상 지속 시 알림 |

### 4.6. 인증 정책

DevAgent는 Google OAuth 2.0 기반의 소셜 로그인을 유일한 인증 수단으로 사용한다. 자체 비밀번호 인증은 지원하지 않는다.

#### 인증 흐름 개요

| 단계 | 트리거 | Client 동작 | Server 동작 | DB / 외부 연동 |
|---|---|---|---|---|
| 1. 로그인 요청 | User가 "Google로 로그인" 버튼을 클릭한다 | Client가 Google OAuth 2.0 인증 화면으로 리다이렉트한다 | - | - |
| 2. Google 인증 | User가 Google 계정으로 인증을 완료한다 | Client가 Authorization Code를 수신한다 | - | Google OAuth: Authorization Code 발급 |
| 3. 토큰 교환 | Client가 Authorization Code를 Server에 전달한다 | - | Server가 Google Token Endpoint에 Code를 전송하여 Access Token + Refresh Token을 수신한다 | Google OAuth: Token Exchange |
| 4. 사용자 정보 조회 | 토큰 교환 완료 시 | - | Server가 Google UserInfo API를 호출하여 email, name, profile_image를 조회한다 | Google UserInfo API |
| 5. 계정 생성/매칭 | 사용자 정보 수신 시 | - | Server가 email 기준으로 USER 테이블을 조회한다. 미존재 시 신규 INSERT, 존재 시 last_login_at을 갱신한다 | USER: SELECT → INSERT 또는 UPDATE |
| 6. 세션 발급 | 계정 매칭 완료 시 | Client가 대시보드 화면으로 전환한다 | Server가 JWT 기반 세션 토큰을 생성하여 Client에 반환한다. Refresh Token은 AUTH_TOKEN 테이블에 암호화 저장한다 | AUTH_TOKEN: INSERT |

단, 로그인 실패(Google 인증 거부, 네트워크 오류) 시 Client가 에러 메시지를 출력하고 로그인 화면으로 복귀한다.

#### 토큰 관리 정책

| 토큰 유형 | 저장 위치 | 유효 기간 | 갱신 방식 |
|---|---|---|---|
| Google Access Token | 메모리(런타임) | 1시간 (Google 기본값) | Refresh Token으로 자동 갱신. System이 만료 5분 전에 선제 갱신한다 |
| Google Refresh Token | AUTH_TOKEN 테이블 (AES-256 암호화) | 무기한 (Google 정책에 따라 6개월 미사용 시 만료) | 만료 시 User에게 재로그인을 요청한다 |
| 앱 세션 토큰 (JWT) | Client 로컬 스토리지 (암호화) | 24시간 | 앱 시작 시 및 런타임 중 API 요청 시마다 JWT 유효성을 검증한다. 만료 5분 전에 System이 Refresh Token으로 선제 갱신하며, 만료 후 요청 시에도 Refresh Token이 유효하면 자동 재발급한다. Refresh Token도 만료된 경우 User에게 재로그인을 요청한다 |

#### 다중 기기 세션 정책

. DevAgent는 동시에 최대 3대 기기에서 로그인을 허용한다.
. 4번째 기기에서 로그인 시 System이 가장 오래된 세션을 강제 만료하고, 해당 기기에 "다른 기기에서 로그인하여 현재 세션이 종료되었습니다." 메시지를 출력한다.
. User 본인이 설정 화면에서 자신의 모든 기기 세션을 일괄 종료할 수 있다. 역할 무관하다.

#### 구글 스페이스 인증

구글 스페이스 Bot 연동은 User OAuth와 별도의 Service Account를 병행 사용한다.

| 인증 방식 | 용도 | 설정 주체 |
|---|---|---|
| OAuth 2.0 (User Consent) | User가 최초 구글 스페이스 연동을 설정할 때. 스페이스 읽기/쓰기 권한 위임 | User가 설정 화면에서 "구글 스페이스 연결" 버튼을 클릭한다 |
| Service Account | Bot이 스페이스 메시지를 수신하고 알림을 발송할 때 | Admin이 Google Workspace Admin Console에서 Domain-wide Delegation을 사전 승인한다 |

단, User가 구글 스페이스 연동을 해제(Revoke)하면 System이 AUTH_TOKEN 테이블에서 해당 Google Space Refresh Token을 삭제하고, 구글 스페이스 리모트 제어 기능을 비활성화한다.

#### 외부 AI API 키 인증

| API | 키 저장 위치 | 등록 방식 | 검증 |
|---|---|---|---|
| Claude API | OS 키체인 (AES-256) | Owner가 설정 화면에서 API 키를 입력한다 | System이 등록 즉시 테스트 요청을 전송하여 유효성을 검증한다. 실패 시 "유효하지 않은 API 키입니다."를 출력한다 |
| Gemini API | OS 키체인 (AES-256) | 동일 | 동일 |
| Midjourney API | OS 키체인 (AES-256) | 동일 | 동일 |
| Llama (로컬) | 로컬 모델 경로 | Owner가 설정 화면에서 모델 파일 경로를 지정한다 | System이 모델 로드 테스트를 수행한다. 실패 시 "모델 파일을 로드할 수 없습니다."를 출력한다 |

단, API 키는 UI에 마스킹(`sk-****...****1234`) 처리하여 출력한다. 평문 노출은 불가하다.

#### Claude Code CLI 인증 (구독 요금제 병행)

DevAgent는 Claude API 키 외에 Claude Code CLI 서브프로세스를 통한 구독 요금제(Max/Pro) 인증을 병행 지원한다. User가 터미널에서 `claude` CLI에 로그인하면, DevAgent가 해당 인증 정보를 서브프로세스 호출로 활용한다.

| 항목 | 내용 |
|---|---|
| 인증 수단 | Claude Code CLI (`claude -p` 서브프로세스 호출) |
| 사전 조건 | User가 터미널에서 `claude` CLI 로그인을 완료한 상태여야 한다 |
| 활성화 방식 | User가 설정 화면에서 "Claude Code CLI 모드" 토글을 활성화한다 |
| 저장 위치 | api_key_store 테이블 (provider: CLAUDE_CLI_ENABLED, AES-256 암호화) |
| CLI 감지 | System이 `claude --version` 명령으로 CLI 설치 여부를 자동 감지하여 설정 화면에 상태를 출력한다 |
| 호출 방식 | `execFile('claude', ['-p', prompt, '--output-format', 'text'], { shell: true })` — 타임아웃 180초, 최대 버퍼 10MB. Windows에서 `claude.cmd`/npm global 경로를 정상 탐색하기 위해 `shell: true` 옵션을 사용한다 |
| 프로세스 종료 | 앱 종료 시 System이 추적 중인 CLI 자식 프로세스를 `child.kill()`로 종료한다. Windows 호환성을 위해 Unix 시그널(SIGKILL)을 사용하지 않는다 |

**우선순위 및 폴백 정책:**

| 조건 | Claude 모델 호출 경로 |
|---|---|
| CLI 모드 ON + CLI 정상 | CLI 서브프로세스로 호출한다 |
| CLI 모드 ON + CLI 실패 + API 키 존재 | API 키 방식으로 자동 폴백한다 |
| CLI 모드 ON + CLI 실패 + API 키 미설정 | "Claude Code CLI 호출 실패. API 키도 설정되지 않았습니다." 에러를 출력한다 |
| CLI 모드 OFF + API 키 존재 | API 키 방식으로 호출한다 |
| CLI 모드 OFF + API 키 미설정 | 다음 폴백 모델로 전환한다 |

단, CLI 모드 활성화 시에도 API 키가 함께 등록되어 있으면 CLI 장애 시 자동 전환되므로, 두 인증 수단의 병행 등록을 권장한다.

### 4.7. 스냅샷 용량 관리 정책

스냅샷이 무한 축적되어 디스크를 고갈시키는 것을 방지하기 위해 System이 자동 정리 정책을 적용한다.

| 항목 | 정책 |
|---|---|
| 프로젝트당 최대 스냅샷 수 | 100개. 초과 시 System이 가장 오래된 AUTO 스냅샷부터 순차 삭제한다 |
| MANUAL 스냅샷 보호 | User가 수동 생성한 스냅샷(MANUAL)은 자동 삭제 대상에서 제외한다. Owner만 수동 삭제 가능하다 |
| PRE_ROLLBACK 스냅샷 보존 | 최근 5개만 보존한다. 6번째 롤백 시 가장 오래된 PRE_ROLLBACK 스냅샷을 삭제한다 |
| MANUAL 스냅샷 포화 처리 | MANUAL 스냅샷이 최대 수를 모두 점유하여 AUTO 삭제 대상이 없는 경우, System이 AUTO 스냅샷 자동 생성을 중단한다. 새 MANUAL 스냅샷 생성 시에도 System이 "수동 스냅샷이 상한에 도달했습니다. 기존 스냅샷을 삭제한 후 다시 시도해 주세요."를 출력하고 생성을 거부한다 |
| Vector DB 인덱스 정리 | 스냅샷 삭제 시 System이 연관된 Vector DB 인덱스를 동시 삭제한다 |
| Git 히스토리 | Git 커밋은 삭제하지 않는다. 스냅샷 삭제는 메타데이터(SNAPSHOT 테이블 + Vector DB)만 정리한다 |

단, Owner가 설정 화면에서 최대 스냅샷 수를 50~500 범위 내에서 변경할 수 있다.

---

# Part 2. 시스템 아키텍처

## 5. 전체 아키텍처 구조

```
┌─────────────────────────────────────────────────────────────┐
│                    PC Application (Electron)                 │
│  ┌──────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ Dashboard │  │ Timeline UI  │  │ Real-time Log Viewer   │ │
│  └─────┬────┘  └──────┬───────┘  └───────────┬────────────┘ │
│        └───────────────┼──────────────────────┘              │
│                        ▼                                     │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │              Auth Layer (Google OAuth 2.0 + JWT)         │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐ │ │
│  │  │ OAuth Client │  │ JWT Manager  │  │ RBAC Guard    │ │ │
│  │  │ (Google SSO) │  │ (Session)    │  │ (Per-Project) │ │ │
│  │  └──────────────┘  └──────────────┘  └───────────────┘ │ │
│  └─────────────────────────┬───────────────────────────────┘ │
│                            ▼                                 │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │              Orchestrator (Node.js Runtime)              │ │
│  │  ┌───────────┐ ┌───────────┐ ┌──────────┐ ┌──────────┐ │ │
│  │  │ Classifier │ │ Context   │ │ Self-    │ │ Pipeline │ │ │
│  │  │            │ │ Injector  │ │ Critic   │ │ Manager  │ │ │
│  │  └───────────┘ └───────────┘ └──────────┘ └──────────┘ │ │
│  └────────┬──────────┬──────────┬──────────┬───────────────┘ │
│           ▼          ▼          ▼          ▼                 │
│  ┌────────────┐┌──────────┐┌──────────┐┌──────────────────┐ │
│  │ 기획 엔진  ││ 설계 엔진││ 시각 엔진││   구현 엔진      │ │
│  │Claude/Gemi││ Claude   ││MJ/Gemini ││ Llama/Claude     │ │
│  └──────┬─────┘└─────┬────┘└──────────┘└────────┬─────────┘ │
│         └────────────┼──────────────────────────┘           │
│                      ▼                                      │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │           AI API Gateway                                │ │
│  │  ┌──────────────────┐  ┌──────────────────────────────┐ │ │
│  │  │ API Key 방식     │  │ Claude CLI Subprocess        │ │ │
│  │  │ (Claude/Gemini/  │  │ (Max/Pro 구독 요금제)        │ │ │
│  │  │  MJ/Llama)       │  │ CLI 우선 → API Key 폴백     │ │ │
│  │  └──────────────────┘  └──────────────────────────────┘ │ │
│  └─────────────────────────────────────────────────────────┘ │
│                        ▼                                     │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                 Data Layer                               │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐              │ │
│  │  │ Local Git│  │ NoSQL DB │  │ Vector DB│              │ │
│  │  │ (Code)   │  │ (Events) │  │(Snapshot)│              │ │
│  │  └──────────┘  └──────────┘  └──────────┘              │ │
│  └─────────────────────────────────────────────────────────┘ │
└──────────────────────────┬──────────────────────────────────┘
                           │ WebSocket / REST
                           ▼
              ┌──────────────────────┐
              │  Google Space Bot    │
              │  (The Communicator)  │
              │  OAuth 2.0 + SA     │
              └──────────────────────┘
```

---

## 6. 프로젝트 하이라키 및 데이터 상속

System은 프로젝트를 3계층 구조로 관리한다. 상위 계층의 데이터는 하위 계층으로 자동 상속되며, 하위 계층은 상속받은 데이터를 확장할 수 있으나 삭제 및 타입 변경은 불가하다.

| 계층 | 데이터 범위 | 상속 규칙 | 격리 규칙 |
|---|---|---|---|
| Layer 1. 루트 (Root) | 전사 표준 폰트, 컬러 팔레트, 로깅 프로토콜, 보안 체크리스트 | 하위 레이어에 자동 포함(Include), 전역 상수로 접근 | - |
| Layer 2. 부모 (Parent) | 공통 인증 모듈, 통합 알림, 핵심 도메인 엔티티(User, Product) | 하위에서 참조 및 확장(Extend) 가능 | 부모 필드 삭제·타입 변경 불가 |
| Layer 3. 자식 (Child) | 마이크로 서비스 로직, 전용 테마 CSS, 개별 테스트 코드 | - | 자식 간 데이터 침범 물리적 격리, 동일 부모 형제 간 API 통신만 허용 |

단, Layer 2의 핵심 도메인 엔티티 스키마를 변경할 경우 System이 영향받는 모든 Child 프로젝트에 마이그레이션 알림을 전송한다.

---

## 7. 모델 오케스트레이션 상세

### 7.1. 오케스트레이터 파이프라인

| 단계 | 트리거 | Client 동작 | Server 동작 | DB / 외부 연동 |
|---|---|---|---|---|
| 1. 입력 수신 | User가 채팅 인터페이스에 요청을 입력한다 | Client가 입력 텍스트를 Server에 전송한다 | - | - |
| 2. 분류 (Classification) | Server가 입력을 수신한다 | Client가 분류 진행 인디케이터를 출력한다 | Classifier가 입력을 분석하여 작업 유형(기획/설계/시각/구현)을 결정한다 | - |
| 2-1. 분류 불확실 | Classifier의 분류 신뢰도가 임계값(0.7) 미만인 경우 | Client가 분류 선택 UI를 출력한다. "이 요청을 어떤 작업으로 처리할까요? [기획] [설계] [시각] [구현]" | System이 분류를 보류하고 User 확인을 요청한다 | - |
| 3. 문맥 주입 (Context Injection) | 분류 완료 시 | - | Context Injector가 프로젝트 하이라키에서 관련 산출물을 수집하여 프롬프트에 주입한다 | Vector DB: 관련 스냅샷 및 위키 검색 |
| 4. 모델 실행 | 프롬프트 준비 완료 시 | Client가 실시간 스트리밍으로 모델 응답을 출력한다 | Pipeline Manager가 해당 엔진(기획/설계/시각/구현)에 작업을 디스패치한다 | 외부 AI API 호출 |
| 5. 결과 검증 (Self-Criticism) | 모델 응답 완료 시 | Client가 검증 진행 상태를 출력한다 | Self-Critic이 산출물을 PRD/이전 산출물과 대조하여 정합성을 판정한다 | NoSQL DB: 검증 결과 이벤트 저장 |
| 6. 산출물 저장 | 검증 통과 시 | Client가 완료 메시지를 출력한다 | Server가 산출물을 파일 시스템에 저장하고 위키를 자동 갱신한다 | Git: 커밋 생성, NoSQL DB: 이벤트 저장 |
| 6-1. 검증 실패 | 검증 미통과 시 | Client가 불일치 항목과 수정 제안을 출력한다 | Server가 불일치 사유를 포함하여 모델에 재생성을 요청한다 | NoSQL DB: 실패 이벤트 저장 |

단, Self-Criticism 재시도는 최대 2회로 제한한다. 2회 실패 시 System이 User에게 수동 검토를 요청한다.

### 7.2. 모델별 역할 매트릭스

| 엔진 | 주 모델 | 보조 모델 | 입력 | 출력 | 폴백 모델 |
|---|---|---|---|---|---|
| 기획 | Claude Sonnet 4.6 | Gemini 2.5 Flash | User 요구사항, 레거시 문서 | PRD, 유즈케이스, User Journey Map | Gemini 2.5 Pro → Claude Opus 4.6 |
| 설계 | Claude Sonnet 4.6 | - | PRD, 도메인 엔티티 | ERD, API 설계서, IaC 초안 | Claude Opus 4.6 |
| 시각 | Midjourney | Gemini 2.5 Flash | 브랜드 키워드, 와이어프레임 텍스트 | UI 목업 이미지 → Tailwind CSS + React 컴포넌트 | Gemini 2.5 Flash 단독 |
| 구현 | Llama 3 (로컬) | Claude Sonnet 4.6 | 설계서, 컴포넌트 구조 | 소스 코드, 폴더 구조, 단위 테스트 | Claude Sonnet 4.6 단독 |

**현행 모델 ID 매핑 (2026-04 기준):**

| shortName | API Model ID | 비고 |
|---|---|---|
| claude-opus | claude-opus-4-6-20250627 | Claude Opus 4.6 |
| claude-sonnet | claude-sonnet-4-6-20250627 | Claude Sonnet 4.6 |
| claude-haiku | claude-haiku-4-5-20251001 | Claude Haiku 4.5 |
| gemini-pro | gemini-2.5-pro | Gemini 2.5 Pro |
| gemini-flash | gemini-2.5-flash | Gemini 2.5 Flash |
| gemini-flash-lite | gemini-2.0-flash-lite | Gemini 2.0 Flash Lite (신규) |

단, DB에 저장된 폐기 모델명(`gemini-1.5-pro`, `gemini-2.0-flash`, `claude-*-4-20250514`)은 resolveModel 함수가 현행 모델 ID로 자동 변환한다.

단, Claude 모델(Sonnet/Opus)을 호출하는 모든 엔진은 Claude Code CLI 모드가 활성화된 경우 CLI 서브프로세스를 우선 사용한다. CLI 호출 실패 시 API 키 방식으로 자동 폴백하며, 양쪽 모두 불가 시 매트릭스의 폴백 모델 체인을 따른다.

---

# Part 3. FSD (Functional Specification Document)

## 8. 모듈 1 — 프로젝트 대시보드

DevAgent의 메인 화면이다. User가 프로젝트를 생성·관리하고, 전체 작업 현황을 한눈에 파악하는 허브 역할을 수행한다.

User가 프로젝트를 선택하면 Client가 해당 프로젝트의 워크스페이스로 전환한다.

주요 접근 대상: Owner, Editor, Viewer.
연관 시스템: Local Git, NoSQL DB.
핵심 테이블: PROJECT, WORKSPACE, USER.

### 8.1. 프로젝트 목록 화면

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Table] 프로젝트 목록 | Y | System이 PROJECT_MEMBER 테이블 기준으로 해당 User가 소속된 프로젝트만 필터링하여 출력한다 |
| [Button] 새 프로젝트 생성 | Y | 모든 인증된 User에게 활성화된다. 클릭 시 프로젝트 생성 팝업(§8.2)을 출력한다 |
| [Input] 프로젝트 검색 | N | 최소 2자 이상 입력 시 Client가 실시간 필터링한다 (max length = 100) |
| [Select] 정렬 기준 | N | 최근 수정순(기본값) / 생성일순 / 이름순 |

### 8.2. 프로젝트 생성 팝업

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Input] 프로젝트명 | Y | 최소 2자, 최대 50자 (max length = 50). 영문, 한글, 숫자, 하이픈(-), 언더스코어(_) 허용. 공백 불가 |
| [Select] 하이라키 유형 | Y | Root / Parent / Child 중 선택. Child 선택 시 부모 프로젝트 Select가 추가 노출된다 |
| [Select] 부모 프로젝트 | 조건부 | 하이라키 유형이 Child일 때만 필수. User가 Editor 이상 역할로 소속된 Parent 프로젝트 목록을 출력한다. Viewer 역할로만 소속된 Parent는 목록에서 제외한다 |
| [Input] 설명 | N | 최대 200자 (max length = 200) |
| [Button] 생성 | Y | 모든 필수 필드 입력 완료 시 활성화된다. Server가 단일 트랜잭션으로 다음을 순차 처리한다: (1) PROJECT INSERT (2) PROJECT_MEMBER INSERT (생성자를 Owner로) (3) WORKSPACE INSERT (기본값) (4) MODEL_CONFIG INSERT (4개 엔진 기본 설정) (5) 로컬 Git 저장소 초기화 (6) AUDIT_LOG INSERT (action: PROJECT_CREATE) |

단, 동일 부모 하위에 같은 이름의 Child 프로젝트 생성은 불가하다. System이 중복 검사를 수행하고 중복 시 에러 메시지를 출력한다.

### 8.3. 멤버 관리 화면

Owner가 프로젝트에 Editor/Viewer를 초대하고 역할을 변경하는 화면이다. Owner만 접근 가능하다.

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Table] 멤버 목록 | Y | 프로젝트에 소속된 전체 멤버를 역할·최근 접속 시간과 함께 출력한다. Owner에게는 "Owner" 뱃지를 표시한다 |
| [Input] 이메일로 초대 | Y | 이메일 형식 검증. 이미 소속된 User의 이메일 입력 시 "이미 프로젝트에 소속된 멤버입니다."를 출력한다 |
| [Select] 초대 역할 | Y | Editor / Viewer 중 선택. Owner 역할로의 초대는 불가하다 |
| [Button] 초대 | Y | System이 해당 이메일의 USER를 조회한다. 미가입 User인 경우 "등록되지 않은 사용자입니다. Google 로그인 후 초대가 가능합니다."를 출력한다. 초대 성공 시 System이 PROJECT_MEMBER에 INSERT하고 AUDIT_LOG에 MEMBER_INVITE 이벤트를 기록한다 |
| [Select] 역할 변경 | N | Owner가 기존 멤버의 역할을 Editor ↔ Viewer로 변경한다. System이 AUDIT_LOG에 ROLE_CHANGE 이벤트를 기록한다 |
| [Button] 멤버 제거 | N | Owner가 멤버를 프로젝트에서 제거한다. System이 PROJECT_MEMBER에서 해당 행을 DELETE하고 AUDIT_LOG에 기록한다 |

단, Owner 자신은 역할 변경 및 제거가 불가하다. Owner를 변경하려면 별도의 Owner 양도 기능을 사용한다.
※ 초대가 완료되면 System이 대상 User에게 앱 내 알림을 전송한다. 대상 User가 구글 스페이스를 연동한 상태이면 구글 스페이스에도 "[프로젝트명]에 [역할]로 초대되었습니다." 알림을 발송한다.

### 8.4. Owner 양도

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Select] 양도 대상 | Y | 현재 프로젝트의 Editor 멤버만 출력한다. Viewer에게는 양도 불가 |
| [Button] Owner 양도 | Y | System이 "Owner 권한을 [대상 이름]에게 양도합니다. 양도 후 본인은 Editor로 전환됩니다. 계속하시겠습니까?"를 출력한다 |

단, Owner 양도 시 System이 단일 트랜잭션으로 다음을 동시 처리한다: (1) PROJECT_MEMBER에서 기존 Owner를 Editor로, 대상 멤버를 Owner로 갱신 (2) PROJECT.owner_user_id를 대상 멤버의 user_id로 갱신 (3) AUDIT_LOG에 OWNER_TRANSFER 이벤트를 기록한다.

### 8.5. 프로젝트 삭제 프로그램 동작 명세 (PB)

| 단계 | 트리거 | Client 동작 | Server 동작 | DB / 외부 연동 |
|---|---|---|---|---|
| 1. 삭제 요청 | Owner가 프로젝트 설정에서 "프로젝트 삭제" 버튼을 클릭한다 | Client가 "삭제된 프로젝트는 복구할 수 없습니다. 프로젝트명을 입력하여 확인해 주세요."를 출력한다 | - | - |
| 2. 확인 | Owner가 프로젝트명을 정확히 입력하고 확인을 클릭한다 | Client가 삭제 진행 상태를 출력한다 | Server가 입력된 프로젝트명과 실제 프로젝트명을 대조 검증한다 | - |
| 3. 논리 삭제 | 검증 통과 시 | Client가 대시보드로 전환하고 "프로젝트가 삭제되었습니다." 메시지를 출력한다 | Server가 PROJECT.status를 DELETED로 갱신한다. 해당 프로젝트는 대시보드에서 즉시 미노출되며 모든 접근이 차단된다 | PROJECT: UPDATE (status = DELETED), AUDIT_LOG: INSERT (action: PROJECT_DELETE) |
| 4. 비동기 물리 정리 | 논리 삭제 완료 후 System이 백그라운드 작업을 시작한다 | - | Server가 순차적으로 연관 데이터를 물리 삭제한다: (1) WIKI_DOCUMENT (2) EVENT_LOG (3) SNAPSHOT + Vector DB 인덱스 (4) AUDIT_LOG 중 project_id 일치 기록 (5) MODEL_CONFIG (6) WORKSPACE (7) PROJECT_MEMBER | 각 테이블 DELETE |
| 5. Git 저장소 삭제 | DB 물리 정리 완료 시 | - | Server가 로컬 Git 저장소 디렉토리를 물리 삭제한다 | 파일 시스템: rm -rf [git_repo_path] |
| 6. PROJECT 행 삭제 | Git 삭제 완료 시 | - | Server가 PROJECT 테이블에서 해당 행을 물리 삭제한다 | PROJECT: DELETE |
| 6-1. 물리 정리 실패 | 4~5단계 중 오류 발생 시 | - | Server가 실패 지점을 기록하고 Admin에게 알림한다. PROJECT.status는 DELETED를 유지하며 재시도 큐에 등록한다 | 즉시 알림, 재시도 큐 등록 |

### 8.6. 프로젝트 보관/복구 프로그램 동작 명세 (PB)

보관(Archive) 상태의 프로젝트는 읽기 전용이다. Editor를 포함한 모든 멤버가 편집·실행·롤백을 수행할 수 없다.

| 단계 | 트리거 | Client 동작 | Server 동작 | DB / 외부 연동 |
|---|---|---|---|---|
| 1. 보관 요청 | Owner가 프로젝트 설정에서 "프로젝트 보관" 버튼을 클릭한다 | Client가 "보관된 프로젝트는 읽기 전용이 됩니다. 계속하시겠습니까?"를 출력한다 | - | - |
| 2. 보관 처리 | Owner가 확인을 클릭한다 | Client가 프로젝트 목록에서 해당 프로젝트에 "보관됨" 라벨을 출력한다 | Server가 PROJECT.status를 ARCHIVED로 갱신한다 | PROJECT: UPDATE (status = ARCHIVED), AUDIT_LOG: INSERT (action: PROJECT_ARCHIVE) |
| 3. 접근 제한 | 보관 완료 시 | Client가 보관된 프로젝트 진입 시 모든 편집 UI를 비활성화(Disabled)한다. 채팅 입력, 스냅샷 생성, 롤백, 위키 편집 버튼을 비활성화한다 | Server가 보관된 프로젝트에 대한 쓰기 요청을 거부한다 | - |
| 4. 복구 요청 | Owner가 보관된 프로젝트 설정에서 "프로젝트 복구" 버튼을 클릭한다 | Client가 "프로젝트를 활성 상태로 복구합니다. 계속하시겠습니까?"를 출력한다 | - | - |
| 5. 복구 처리 | Owner가 확인을 클릭한다 | Client가 "보관됨" 라벨을 제거하고 모든 편집 UI를 재활성화한다 | Server가 PROJECT.status를 ACTIVE로 갱신한다 | PROJECT: UPDATE (status = ACTIVE), AUDIT_LOG: INSERT (action: PROJECT_RESTORE) |

단, 보관 상태에서도 구글 스페이스의 `/status`, `/wiki` 명령은 정상 동작한다. `/run`, `/snapshot`, `/rollback` 명령은 System이 "보관된 프로젝트입니다. Owner가 복구한 후 사용할 수 있습니다."를 출력하고 거부한다.

---

## 9. 모듈 2 — 워크스페이스 (채팅 + 에이전트 인터페이스)

DevAgent의 핵심 작업 공간이다. User가 자연어로 요청을 입력하면 System이 오케스트레이터를 통해 적절한 AI 모델에 작업을 분배하고, 결과를 실시간 스트리밍으로 출력한다.

좌측에 프로젝트 파일 트리, 중앙에 채팅 인터페이스, 우측에 산출물 미리보기 패널을 배치한다.

주요 접근 대상: Owner, Editor, Viewer(읽기 전용).
연관 시스템: Claude API, Gemini API, Midjourney API, Llama(로컬), Local Git, Vector DB.
핵심 테이블: EVENT_LOG, SNAPSHOT, WIKI_DOCUMENT.

단, Viewer는 워크스페이스에 읽기 전용으로 접근한다. 대화 이력·파일 트리·산출물 미리보기를 열람할 수 있으나, 메시지 입력·스냅샷 생성·엔진 선택 등 모든 쓰기 UI는 비활성화(Disabled) 상태로 출력한다.

### 9.1. 채팅 인터페이스

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Input] 메시지 입력 | Y | 최대 10,000자 (max length = 10000). 파일 첨부 시 5MB 이하, pdf/md/txt/png/jpg 허용 |
| [Button] 전송 | Y | 입력값이 1자 이상일 때 활성화. Server가 오케스트레이터 파이프라인을 시작한다 |
| [Table] 대화 이력 | Y | System이 타임스탬프 순으로 정렬하여 출력한다. 각 메시지에 작업 유형 라벨(기획/설계/시각/구현)을 표시한다 |
| [Button] 스냅샷 생성 | N | User가 현재 시점의 수동 스냅샷을 생성한다. SNAPSHOT 테이블에 INSERT한다 |
| [Select] 엔진 강제 선택 | N | 기본값: 자동(오케스트레이터 판단). User가 특정 엔진을 수동 지정할 수 있다 |
| [Table] 비동기 작업 큐 | N | 시각 엔진(Midjourney) 등 장시간 소요 작업의 진행 상태를 출력한다. 각 작업에 예상 소요 시간, 진행률, 취소 버튼을 표시한다 |

단, 시각 엔진(Midjourney) 작업은 이미지 생성에 30초~3분이 소요될 수 있다. System이 작업을 비동기 큐에 등록하고 Client에 진행 상태를 실시간 폴링(5초 간격)으로 출력한다. User가 취소 버튼을 클릭하면 System이 대기 중인 작업을 큐에서 제거한다. 이미 생성 중인 작업은 취소가 불가하다.

### 9.2. 파일 트리 패널

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Table] 파일/폴더 트리 | Y | System이 로컬 Git 저장소의 현재 워킹 디렉토리를 실시간 반영한다 |
| [Button] 파일 열기 | Y | 선택된 파일을 우측 미리보기 패널에 출력한다. 코드 파일은 구문 강조(Syntax Highlighting)를 적용한다 |
| [Button] 변경 이력 | N | 선택된 파일의 Git 커밋 히스토리를 팝업으로 출력한다 |

### 9.3. 산출물 미리보기 패널

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Tab] 코드 / 문서 / 이미지 | Y | 산출물 유형에 따라 탭을 자동 전환한다 |
| [Button] IDE로 열기 | N | VS Code와 소켓 통신하여 해당 파일을 IDE에서 직접 연다 |
| [Button] 위키에 저장 | N | 현재 미리보기 중인 산출물을 WIKI_DOCUMENT로 저장한다 |

---

## 10. 모듈 3 — 타임라인 및 스냅샷 롤백

프로젝트의 전체 이벤트 히스토리를 시각화하고, 특정 시점으로 롤백할 수 있는 시간 여행(Time Travel) 인터페이스이다.

System이 이벤트 소싱으로 저장한 모든 상태 변화를 타임라인 형태로 출력하고, User가 원하는 시점으로 프로젝트 전체를 복구한다.

주요 접근 대상: Owner, Editor, Viewer(읽기 전용).
연관 시스템: Local Git, NoSQL DB, Vector DB.
핵심 테이블: SNAPSHOT, EVENT_LOG.

단, Viewer는 타임라인을 읽기 전용으로 열람할 수 있으나, 스냅샷 복구 버튼은 비활성화(Disabled) 상태로 출력한다.

### 10.1. 타임라인 화면

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Table] 이벤트 타임라인 | Y | System이 EVENT_LOG를 타임스탬프 역순으로 출력한다. 이벤트 유형별 아이콘(기획/설계/시각/구현/스냅샷)을 표시한다 |
| [Button] 스냅샷 복구 | Y | Owner/Editor만 활성화. Viewer에게는 비활성화(Disabled) 출력한다 |
| [Select] 필터 | N | 이벤트 유형별 필터링 (전체/기획/설계/시각/구현/스냅샷/에러) |

### 10.2. 롤백 확인 팝업

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Table] 롤백 영향 범위 | Y | System이 현재 상태와 복구 지점 사이의 차이(변경된 파일 수, 삭제될 이벤트 수)를 출력한다 |
| [Checkbox] 현재 상태 백업 | Y | 기본값: 체크됨. 롤백 전 현재 상태를 자동 스냅샷으로 저장한다 |
| [Button] 롤백 실행 | Y | User가 클릭하면 System이 Git reset, NoSQL 이벤트 소프트 삭제, Vector DB 스냅샷 컨텍스트 로드를 순차 실행한다 |

### 10.3. 롤백 프로그램 동작 명세 (PB)

| 단계 | 트리거 | Client 동작 | Server 동작 | DB / 외부 연동 |
|---|---|---|---|---|
| 1. 복구 지점 선택 | User가 타임라인에서 스냅샷을 클릭한다 | Client가 선택된 스냅샷을 하이라이트하고 롤백 확인 팝업을 출력한다 | - | - |
| 2. 현재 상태 백업 | User가 롤백 실행 버튼을 클릭하고 "현재 상태 백업" 체크박스가 체크된 경우 | Client가 백업 진행 상태를 출력한다 | Server가 현재 상태의 자동 스냅샷을 생성한다 | Git: 현재 커밋 해시 기록, SNAPSHOT: INSERT (trigger_type: PRE_ROLLBACK) |
| 2-1. 백업 건너뛰기 | User가 롤백 실행 버튼을 클릭하고 "현재 상태 백업" 체크박스가 미체크인 경우 | - | Server가 백업 없이 3단계로 진행한다 | - |
| 3. 파일 시스템 롤백 | 2단계 또는 2-1단계 완료 시 | Client가 롤백 진행 상태를 출력한다 | Server가 로컬 파일 시스템을 대상 Git 커밋으로 되돌린다 | Git: `git reset --hard [target_commit]` |
| 4. 이벤트 로그 정리 | 파일 시스템 롤백 완료 시 | - | Server가 대상 스냅샷 이후의 이벤트를 소프트 삭제(is_deleted = true)한다 | NoSQL DB: EVENT_LOG UPDATE |
| 5. 컨텍스트 복구 | 이벤트 정리 완료 시 | - | Server가 대상 스냅샷의 Vector DB 컨텍스트를 로드하여 AI 에이전트의 대화 맥락을 복원한다 | Vector DB: 스냅샷 인덱스 조회 |
| 6. 완료 | 컨텍스트 복구 완료 시 | Client가 롤백 완료 메시지와 복구된 시점 정보를 출력한다 | Server가 롤백 완료 이벤트를 기록한다 | EVENT_LOG: INSERT (type: ROLLBACK) |
| 6-1. 롤백 실패 | 3~5단계 중 오류 발생 시 | Client가 에러 메시지를 출력한다 | 2단계에서 백업 스냅샷을 생성한 경우 Server가 해당 스냅샷으로 재롤백한다. 백업을 건너뛴 경우(2-1단계) 복구가 불가하며 Server가 Admin에게 즉시 알림한다 | 자동 복구 시도 → 실패 시 Admin 알림 |

---

## 11. 모듈 4 — 지식 위키

프로젝트의 모든 산출물과 의사결정 이력을 자동 문서화하여 검색 가능한 지식 저장소로 관리하는 모듈이다.

System이 확정된 PRD, ADR, API 명세서를 자동으로 Markdown 위키 페이지로 생성하고, 시맨틱 검색 엔진을 통해 User가 자연어로 과거 지식을 인출한다.

주요 접근 대상: Owner, Editor, Viewer(읽기 전용).
연관 시스템: Vector DB, NoSQL DB.
핵심 테이블: WIKI_DOCUMENT, EVENT_LOG.

단, Viewer는 위키 목록 조회와 문서 열람만 가능하다. 새 위키 작성, 기존 문서 편집 등 모든 쓰기 UI는 비활성화(Disabled) 상태로 출력한다.

### 11.1. 위키 목록 화면

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Input] 시맨틱 검색 | Y | System이 입력 텍스트의 의도를 분석하여 관련 위키 페이지 + 대화 로그를 통합 추천한다 |
| [Table] 위키 문서 목록 | Y | 분류(의사결정 이력 / 기술 명세 / 트러블슈팅)별 필터링 가능 |
| [Button] 새 위키 작성 | N | Owner/Editor만 활성화. 수동으로 위키 페이지를 생성한다 |

### 11.2. 위키 상세 화면

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Tab] 문서 내용 / 관련 이벤트 | Y | 문서 본문과 해당 문서가 생성된 당시의 이벤트 로그를 탭으로 분리하여 출력한다 |
| [Button] 편집 | N | Owner/Editor만 활성화. Markdown 에디터를 출력한다 |
| [Button] 버전 이력 | N | 위키 문서의 변경 이력을 출력한다 |

### 11.3. 지식 분류 체계

| 분류 코드 | 분류명 | 내용 | 자동 생성 트리거 |
|---|---|---|---|
| WIKI_ADR | 의사결정 이력 | 특정 기술/라이브러리 선택의 이유와 맥락 | 설계 엔진이 기술 선택을 결정할 때 |
| WIKI_SPEC | 기술 명세 | API 엔드포인트, ERD, 인터페이스 정의 | 설계 엔진 산출물이 Self-Criticism을 통과할 때 |
| WIKI_TROUBLE | 트러블슈팅 | 발생한 에러 + AI 해결책 + 수정 코드 | 구현 엔진이 에러를 감지하고 수정할 때 |

---

## 12. 모듈 5 — 구글 스페이스 연동 (리모트 컨트롤)

외부에서 구글 스페이스 채팅을 통해 DevAgent PC 앱에 명령을 전달하고 결과를 수신하는 원격 제어 모듈이다.

System이 구글 스페이스 Bot을 통해 명령을 수신하면, WebSocket으로 PC 앱에 전달하여 실행하고, 결과를 구글 스페이스로 회신한다.

단, PC 앱이 미실행 상태이거나 WebSocket 연결이 끊겨 있는 경우 Bot이 "PC 앱이 오프라인 상태입니다. 앱을 실행한 후 다시 시도해 주세요." 메시지를 즉시 회신한다. 명령 큐잉은 지원하지 않는다.

주요 접근 대상: Owner, Editor, Viewer(읽기 명령 한정).
연관 시스템: Google Space API, PC App (WebSocket).
핵심 테이블: EVENT_LOG, AUTH_TOKEN.

#### WebSocket 인증 정책

WebSocket 연결 시 클라이언트가 인증 토큰을 필수로 제공해야 한다. 미인증 연결은 System이 즉시 거부한다.

| 항목 | 내용 |
|---|---|
| 인증 방식 | 연결 URL 쿼리 파라미터로 토큰을 전달한다 (`ws://host:8765?token=xxx`) |
| 토큰 검증 | System이 AUTH_TOKEN 테이블에서 tokenType이 GOOGLE_SPACE_REFRESH이고 is_revoked가 false인 토큰과 대조한다 |
| 인증 실패 | System이 WebSocket 연결을 4001(토큰 미제공) 또는 4003(토큰 무효) 코드로 종료한다 |
| userId 강제 바인딩 | 인증된 연결의 userId를 메시지 처리 시 강제 적용한다. 클라이언트가 전송한 userId 필드는 무시하여 스푸핑을 방지한다 |

#### 활성 프로젝트 정책

User가 여러 프로젝트에 소속된 경우, 구글 스페이스에서 명령을 실행할 대상 프로젝트를 지정해야 한다. `/project` 명령으로 활성 프로젝트를 설정하며, 이후 모든 명령은 해당 프로젝트를 대상으로 실행한다.

| 명령어 | 권한 | System 동작 |
|---|---|---|
| `/project [프로젝트명]` | Owner, Editor, Viewer | System이 User가 소속된 프로젝트 중 이름이 일치하는 프로젝트를 활성 프로젝트로 설정한다. 미소속 프로젝트명 입력 시 "접근 권한이 없는 프로젝트입니다."를 출력한다 |
| `/project` | Owner, Editor, Viewer | System이 현재 활성 프로젝트명을 출력한다. 미설정 시 "활성 프로젝트가 설정되지 않았습니다. `/project [프로젝트명]`으로 설정해 주세요."를 출력한다 |

단, 활성 프로젝트가 미설정된 상태에서 `/project` 외의 명령을 전송하면 System이 "활성 프로젝트가 설정되지 않았습니다. `/project [프로젝트명]`으로 먼저 설정해 주세요."를 출력하고 실행을 거부한다.
※ User가 1개 프로젝트에만 소속된 경우 System이 해당 프로젝트를 자동으로 활성 프로젝트로 설정한다.

### 12.1. 지원 명령어

| 명령어 | 권한 | System 동작 |
|---|---|---|
| `/status` | Owner, Editor, Viewer | System이 현재 프로젝트 상태(진행 중 작업, 최근 이벤트)를 요약하여 출력한다 |
| `/run [요청]` | Owner, Editor | System이 오케스트레이터 파이프라인을 실행하고 결과를 구글 스페이스로 회신한다 |
| `/snapshot` | Owner, Editor | System이 현재 시점의 수동 스냅샷을 생성하고 확인 메시지를 출력한다 |
| `/snapshot list` | Owner, Editor, Viewer | System이 최근 10건의 스냅샷 목록(snapshot_id, 생성 시각, trigger_type, description)을 출력한다 |
| `/wiki [검색어]` | Owner, Editor, Viewer | System이 시맨틱 검색을 수행하고 상위 3건의 위키 문서 링크를 출력한다 |
| `/rollback [snapshot_id]` | Owner | System이 지정된 스냅샷으로 롤백을 실행한다. snapshot_id는 `/snapshot list`로 조회한다 |

단, Viewer가 쓰기 명령(`/run`, `/snapshot`, `/rollback`)을 전송하면 System이 "권한이 부족합니다. Owner 또는 Editor 권한이 필요합니다."를 출력하고 실행을 거부한다.
※ `/rollback`은 앱 내에서는 Owner/Editor 모두 실행 가능하나(§10.1), 구글 스페이스 리모트에서는 Owner만 실행 가능하다. 원격 환경에서는 롤백 영향 범위를 사전 확인하는 UI가 없으므로 오조작 방지를 위해 Owner로 제한한다.

### 12.2. 알림 프로토콜

System이 이벤트 발생 시 WebSocket으로 연결된 클라이언트에 알림을 전송한다. 알림은 이벤트별 수신 가능 역할에 따라 필터링하여 해당 역할의 클라이언트에만 전송한다. System이 각 클라이언트의 userId로 PROJECT_MEMBER 테이블에서 프로젝트 내 역할을 조회하여 수신 자격을 판정한다.

| 이벤트 | 알림 대상 | 메시지 형식 |
|---|---|---|
| 빌드 성공 | Owner, Editor | `✅ [프로젝트명] 빌드 성공 — [커밋 해시] [소요 시간]` |
| 빌드 실패 | Owner, Editor | `❌ [프로젝트명] 빌드 실패 — [에러 요약]. /run fix 로 자동 수정을 시도할 수 있습니다.` |
| AI 모델 질문 | Owner | `❓ [프로젝트명] [엔진명]이 사용자 확인을 요청합니다: [질문 내용]` |
| 마일스톤 달성 | Owner, Editor | `🎯 [프로젝트명] 마일스톤 달성 — [마일스톤명]` |
| 긴급 에러 | Owner | `🚨 [프로젝트명] 긴급 — [에러 유형]: [상세 내용]. 즉시 확인이 필요합니다.` |

단, Viewer는 모든 알림 수신 대상에서 제외한다. 프로젝트 비소속 클라이언트에게도 알림을 전송하지 않는다.

---

## 13. 모듈 6 — 인증 및 계정 관리

User가 DevAgent에 로그인하고, 외부 서비스(Google, AI API) 연동을 설정하며, 세션과 보안을 관리하는 모듈이다.

Google OAuth 2.0을 유일한 인증 수단으로 사용하며, 자체 비밀번호 인증은 지원하지 않는다. 외부 AI API 키는 Owner가 설정 화면에서 등록한다.

주요 접근 대상: Owner, Editor, Viewer.
연관 시스템: Google OAuth 2.0, Google UserInfo API, Google Space API, OS Keychain, Claude Code CLI.
핵심 테이블: USER, AUTH_TOKEN, AUDIT_LOG.

### 13.1. 로그인 화면

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Button] Google로 로그인 | Y | Client가 Google OAuth 2.0 Authorization Endpoint로 리다이렉트한다. scope: openid, email, profile |
| [Table] 최근 로그인 계정 | N | 이전에 로그인한 적 있는 Google 계정 목록을 출력한다. User가 선택하면 해당 계정으로 바로 인증을 시도한다 |

### 13.2. 로그인 프로그램 동작 명세 (PB)

| 단계 | 트리거 | Client 동작 | Server 동작 | DB / 외부 연동 |
|---|---|---|---|---|
| 1. 인증 요청 | User가 "Google로 로그인" 버튼을 클릭한다 | Client가 Google OAuth Authorization URL로 리다이렉트한다 | - | Google OAuth: Authorization 요청 |
| 2. 사용자 동의 | User가 Google 계정을 선택하고 권한을 승인한다 | Client가 Redirect URI로 Authorization Code를 수신한다 | - | Google OAuth: Authorization Code 발급 |
| 3. 토큰 교환 | Client가 Authorization Code를 Server에 전달한다 | Client가 로딩 인디케이터를 출력한다 | Server가 Google Token Endpoint에 Code + Client Secret을 전송하여 Access Token + Refresh Token을 수신한다 | Google OAuth: Token Exchange (POST /token) |
| 4. 사용자 정보 조회 | 토큰 교환 성공 시 | - | Server가 Access Token으로 Google UserInfo API를 호출하여 email, name, picture를 조회한다 | Google UserInfo API (GET /userinfo) |
| 5. 계정 매칭 | 사용자 정보 수신 시 | - | Server가 email로 USER 테이블을 조회한다. 미존재 시 신규 INSERT, 존재 시 last_login_at 갱신 | USER: SELECT → INSERT 또는 UPDATE |
| 5-1. 비활성 계정 | Server가 USER.is_active = false를 확인한 경우 | Client가 "계정이 비활성화되었습니다. 고객 지원에 문의해 주세요." 메시지를 출력하고 로그인 화면으로 복귀한다 | Server가 세션을 발급하지 않고 로그인을 거부한다 | AUDIT_LOG: INSERT (action: LOGIN_FAILED, detail: {"reason": "ACCOUNT_INACTIVE"}) |
| 6. 세션 발급 | 계정 매칭 완료 및 is_active = true 확인 시 | Client가 JWT를 로컬 스토리지에 암호화 저장하고 대시보드로 전환한다 | Server가 JWT 세션 토큰을 생성하여 반환한다. Google Refresh Token은 AES-256 암호화하여 AUTH_TOKEN에 INSERT한다 | AUTH_TOKEN: INSERT, AUDIT_LOG: INSERT (action: LOGIN) |
| 6-1. 인증 실패 | Google 인증 거부 또는 네트워크 오류 시 | Client가 "로그인에 실패했습니다. 다시 시도해 주세요." 메시지를 출력하고 로그인 화면으로 복귀한다 | Server가 실패 이벤트를 로깅한다 | AUDIT_LOG: INSERT (action: LOGIN_FAILED) |
| 6-2. 세션 초과 | 동일 User의 활성 세션이 3개를 초과하는 경우 | - | Server가 가장 오래된 세션의 JWT를 무효화하고 해당 기기에 강제 로그아웃 이벤트를 전송한다 | AUTH_TOKEN: UPDATE (is_revoked = true) |

### 13.3. 설정 화면 — 외부 서비스 연동

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Input] Claude API Key | N | 등록 완료 시 초록 점 + `••••••••` 마스킹 + [변경] 버튼만 출력한다. [변경] 클릭 시 입력 폼을 노출하고 `변경 중` 뱃지와 [취소] 버튼을 출력한다. 미등록 시 플레이스홀더 + 발급처 링크를 출력한다. 입력 시 System이 즉시 테스트 요청으로 유효성을 검증한다 |
| [Input] Gemini API Key | N | 동일 |
| [Input] Midjourney API Key | N | 동일 |
| [Input] Llama 모델 경로 | N | Owner가 로컬 모델 파일 경로를 지정한다. System이 모델 로드 테스트를 수행한다. 실패 시 "모델 파일을 로드할 수 없습니다."를 출력한다 |
| [Toggle] Claude Code CLI 모드 | N | ON/OFF 토글. 활성화 시 Claude 모델 요청을 CLI 서브프로세스로 우선 처리한다. System이 `claude --version`으로 CLI 설치 여부를 감지하여 상태 인디케이터(초록: 감지됨, 빨강: 미감지)를 출력한다. 토글 변경 시 System이 AUDIT_LOG에 CLAUDE_CLI_ENABLE 또는 CLAUDE_CLI_DISABLE 이벤트를 기록한다 |
| [Button] 구글 스페이스 연결 | N | 미연결 시 노출. System이 Google Space API OAuth 팝업(BrowserWindow)을 열어 User 동의를 받는다. scope: chat.spaces, chat.messages, chat.messages.create. 동의 완료 시 System이 Authorization Code로 Refresh Token을 교환하고, 기존 GOOGLE_SPACE_REFRESH 토큰을 폐기한 후 새 토큰을 AES-256 암호화하여 AUTH_TOKEN에 저장한다. System이 AUDIT_LOG에 SPACE_CONNECT 이벤트를 기록한다 |
| [Button] 구글 스페이스 연결 해제 | N | 연결된 상태에서만 노출. System이 AUTH_TOKEN에서 해당 User의 GOOGLE_SPACE_REFRESH 토큰을 is_revoked = true로 갱신하고 리모트 제어 기능을 비활성화한다. System이 AUDIT_LOG에 SPACE_DISCONNECT 이벤트를 기록한다 |
| [Indicator] 구글 스페이스 상태 | Y | 앱 로드 시 System이 AUTH_TOKEN 테이블에서 해당 User의 유효한 GOOGLE_SPACE_REFRESH 토큰 존재 여부를 조회하여 연결 상태(초록 점: 연결됨, 회색 점: 연결되지 않음)를 출력한다. 앱 재시작 후에도 상태가 영속된다 |
| [Button] 모든 기기 로그아웃 | N | 로그인한 본인이면 역할 무관하게 활성화된다. System이 AUTH_TOKEN 테이블에서 해당 User의 모든 APP_SESSION을 is_revoked = true로 갱신한다 |

### 13.4. 설정 화면 — 활성 세션 관리

| UI 컴포넌트 | 필수 | Validation / 시스템 동작 |
|---|---|---|
| [Table] 활성 세션 목록 | Y | 기기명, 마지막 접속 시간, IP 대역을 출력한다. 현재 기기에 "현재 세션" 라벨을 표시한다 |
| [Button] 개별 세션 종료 | N | 특정 기기의 세션만 강제 종료한다. 현재 세션은 종료 불가(비활성화) |

### 13.5. 로그아웃 프로그램 동작 명세 (PB)

| 단계 | 트리거 | Client 동작 | Server 동작 | DB / 외부 연동 |
|---|---|---|---|---|
| 1. 로그아웃 요청 | User가 로그아웃 버튼을 클릭한다 | Client가 "로그아웃하시겠습니까?" 확인 팝업을 출력한다 | - | - |
| 2. 세션 무효화 | User가 확인을 클릭한다 | Client가 로컬 스토리지의 JWT를 삭제한다 | Server가 AUTH_TOKEN에서 해당 세션을 is_revoked = true로 갱신한다 | AUTH_TOKEN: UPDATE |
| 3. 감사 로그 | 세션 무효화 완료 시 | Client가 로그인 화면으로 전환한다 | Server가 로그아웃 이벤트를 기록한다 | AUDIT_LOG: INSERT (action: LOGOUT) |

---

# Part 4. DB 명세

## 14. 데이터베이스 스키마

### 14.1. PROJECT

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| project_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK. System이 자동 생성한다 |
| project_name | VARCHAR | 50 | Y | - | ✅ | ✅(Owner만) | ❌ | 최소 2자, 최대 50자. 영문/한글/숫자/하이픈/언더스코어 허용 |
| description | TEXT | 200 | N | - | ✅ | ✅(Owner만) | ❌ | 최대 200자 |
| hierarchy_type | ENUM | - | Y | - | ✅ | ❌ | ❌ | ROOT / PARENT / CHILD. 생성 후 변경 불가 |
| parent_project_id | UUID | 36 | N | - | ✅ | ❌ | ❌ | hierarchy_type이 CHILD인 경우 필수. PROJECT.project_id FK |
| owner_user_id | UUID | 36 | Y | - | ✅ | ✅(Owner만) | ❌ | USER.user_id FK |
| git_repo_path | VARCHAR | 500 | Y | Y | 자동채번 | ❌ | ❌ | System이 프로젝트 생성 시 로컬 Git 저장소 경로를 자동 할당한다 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 기준. System이 자동 설정한다 |
| status | ENUM | - | Y | - | ✅(기본 ACTIVE) | ✅(Owner만) | ❌ | ACTIVE / ARCHIVED / DELETED. §15.1 참조. DELETED 전이 시 System이 즉시 접근을 차단하고 백그라운드에서 §8.5 PB에 따라 연관 데이터를 물리 정리한다 |
| updated_at | TIMESTAMP | - | Y | - | ✅(시스템) | ✅(시스템) | ❌ | UTC+0 기준. 변경 시 System이 자동 갱신한다 |

### 14.2. USER

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| user_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| email | VARCHAR | 255 | Y | Y | ✅ | ❌ | ❌ | 이메일 형식 검증. Google 계정 이메일. 생성 후 변경 불가 |
| display_name | VARCHAR | 50 | Y | - | ✅ | ✅ | ❌ | 최소 2자, 최대 50자. Google 프로필 이름으로 초기 설정 |
| profile_image_url | VARCHAR | 500 | N | - | ✅ | ✅(시스템) | ❌ | Google 프로필 이미지 URL. 로그인 시 System이 자동 갱신한다 |
| oauth_provider | ENUM | - | Y | - | ✅(기본 GOOGLE) | ❌ | ❌ | GOOGLE. 현재 Google만 지원한다. 향후 확장 대비 |
| oauth_uid | VARCHAR | 128 | Y | Y | ✅ | ❌ | ❌ | Google 고유 사용자 ID (sub claim). 이메일 변경에도 불변 |
| last_login_at | TIMESTAMP | - | N | - | ✅(시스템) | ✅(시스템) | ❌ | UTC+0. 로그인 성공 시 System이 자동 갱신한다 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |
| is_active | BOOLEAN | - | Y | - | ✅(기본 true) | ✅(본인 또는 시스템) | ❌ | false 시 모든 프로젝트에서 로그인 거부. User 본인이 계정 비활성화를 요청하거나 System이 보안 정책(장기 미접속 등)에 의해 비활성화한다. 프로젝트 Owner는 프로젝트에서 멤버를 제거(§8.3)할 수 있으나, 전역 계정 비활성화는 불가하다 |

### 14.3. AUTH_TOKEN

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| token_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| user_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | USER.user_id FK |
| token_type | ENUM | - | Y | - | ✅ | ❌ | ❌ | GOOGLE_REFRESH / GOOGLE_SPACE_REFRESH / APP_SESSION |
| token_value | TEXT | - | Y | - | ✅ | ✅(시스템) | ❌ | AES-256 암호화 저장. 평문 저장 불가 |
| device_name | VARCHAR | 100 | N | - | ✅ | ❌ | ❌ | 세션 발급 기기명. APP_SESSION 타입에만 해당한다 |
| device_ip | VARCHAR | 45 | N | - | ✅ | ❌ | ❌ | 세션 발급 시점의 IP 주소. IPv4/IPv6 모두 지원 |
| expires_at | TIMESTAMP | - | N | - | ✅ | ❌ | ❌ | UTC+0. APP_SESSION은 발급 후 24시간. GOOGLE_REFRESH는 null(무기한) |
| is_revoked | BOOLEAN | - | Y | - | ✅(기본 false) | ✅(시스템) | ❌ | true 시 해당 토큰은 무효. 로그아웃·강제 종료·연동 해제 시 System이 갱신한다 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |

단, 동일 User의 token_type = APP_SESSION이면서 is_revoked = false인 행이 3개를 초과하면 System이 가장 오래된 행을 is_revoked = true로 자동 갱신한다.

### 14.4. AUDIT_LOG

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| audit_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| user_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | USER.user_id FK. 행위 주체 |
| project_id | UUID | 36 | N | - | ✅ | ❌ | ❌ | PROJECT.project_id FK. 프로젝트 관련 액션(ROLE_CHANGE, OWNER_TRANSFER, MEMBER_INVITE, MEMBER_REMOVE, API_KEY_CHANGE) 시 필수. LOGIN/LOGOUT 등 전역 액션은 null |
| action | ENUM | - | Y | - | ✅ | ❌ | ❌ | LOGIN / LOGIN_FAILED / LOGOUT / ROLE_CHANGE / OWNER_TRANSFER / API_KEY_CHANGE / SPACE_CONNECT / SPACE_DISCONNECT / SESSION_FORCE_REVOKE / MEMBER_INVITE / MEMBER_REMOVE / PROJECT_CREATE / PROJECT_DELETE / PROJECT_ARCHIVE / PROJECT_RESTORE. §15.5 참조 |
| target_user_id | UUID | 36 | N | - | ✅ | ❌ | ❌ | ROLE_CHANGE, OWNER_TRANSFER, MEMBER_INVITE, MEMBER_REMOVE 시 대상 User. USER.user_id FK |
| detail | JSONB | - | N | - | ✅ | ❌ | ❌ | 변경 상세. 예: {"from": "VIEWER", "to": "EDITOR"} |
| ip_address | VARCHAR | 45 | N | - | ✅ | ❌ | ❌ | 행위 발생 시점의 IP 주소 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |

### 14.5. EVENT_LOG

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| event_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| project_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | PROJECT.project_id FK |
| event_type | ENUM | - | Y | - | ✅ | ❌ | ❌ | PLAN / DESIGN / VISUAL / IMPLEMENT / SNAPSHOT / ROLLBACK / ERROR |
| payload | JSONB | - | Y | - | ✅ | ❌ | ❌ | 이벤트 상세 데이터. 구조는 event_type별로 상이하다 |
| model_used | VARCHAR | 50 | N | - | ✅ | ❌ | ❌ | 사용된 AI 모델명 (예: claude-sonnet, gemini-flash) |
| triggered_by | UUID | 36 | N | - | ✅ | ❌ | ❌ | 이벤트를 트리거한 User의 user_id. USER.user_id FK. System 자동 이벤트는 null |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |
| is_deleted | BOOLEAN | - | Y | - | ✅(기본 false) | ✅(시스템) | ❌ | 롤백 시 소프트 삭제 |

### 14.6. SNAPSHOT

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| snapshot_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| project_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | PROJECT.project_id FK |
| git_commit_hash | VARCHAR | 40 | Y | - | ✅ | ❌ | ❌ | 해당 시점의 Git 커밋 해시 |
| vector_index_id | VARCHAR | 100 | Y | - | ✅ | ❌ | ❌ | Vector DB 인덱스 식별자 |
| trigger_type | ENUM | - | Y | - | ✅ | ❌ | ❌ | MANUAL / AUTO / PRE_ROLLBACK |
| created_by | UUID | 36 | N | - | ✅ | ❌ | ❌ | 스냅샷 생성자. USER.user_id FK. MANUAL 시 생성 요청 User, AUTO/PRE_ROLLBACK 시 null(System 자동) |
| description | TEXT | 200 | N | - | ✅ | ❌ | ❌ | User가 입력한 스냅샷 설명 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |

### 14.7. WIKI_DOCUMENT

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| wiki_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| project_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | PROJECT.project_id FK |
| category | ENUM | - | Y | - | ✅ | ✅(Owner/Editor) | ❌ | WIKI_ADR / WIKI_SPEC / WIKI_TROUBLE |
| title | VARCHAR | 200 | Y | - | ✅ | ✅(Owner/Editor) | ❌ | 최대 200자 |
| content | TEXT | - | Y | - | ✅ | ✅(Owner/Editor) | ❌ | Markdown 형식 |
| source_event_id | UUID | 36 | N | - | ✅ | ❌ | ❌ | 자동 생성 시 원본 이벤트 ID. EVENT_LOG.event_id FK |
| version | INTEGER | - | Y | - | ✅(기본 1) | ✅(시스템) | ❌ | 편집 시 System이 자동 증가한다 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |
| updated_at | TIMESTAMP | - | Y | - | ✅(시스템) | ✅(시스템) | ❌ | UTC+0 |

### 14.8. MODEL_CONFIG

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| config_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| project_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | PROJECT.project_id FK |
| engine_type | ENUM | - | Y | - | ✅ | ✅(Owner만) | ❌ | PLAN / DESIGN / VISUAL / IMPLEMENT |
| primary_model | VARCHAR | 50 | Y | - | ✅ | ✅(Owner만) | ❌ | 주 모델명 (예: claude-sonnet) |
| fallback_model | VARCHAR | 50 | N | - | ✅ | ✅(Owner만) | ❌ | 폴백 모델명 |
| temperature | DECIMAL | 3,2 | Y | - | ✅(기본 0.7) | ✅(Owner만) | ❌ | 0.00 ~ 2.00 범위. 범위 초과 시 System이 거부한다 |
| max_tokens | INTEGER | - | Y | - | ✅(기본 4096) | ✅(Owner만) | ❌ | 최소 256, 최대 128000 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |
| updated_at | TIMESTAMP | - | Y | - | ✅(시스템) | ✅(시스템) | ❌ | UTC+0. 설정 변경 시 System이 자동 갱신한다 |

### 14.9. PROJECT_MEMBER

프로젝트별 멤버 역할을 관리하는 다대다(N:M) 매핑 테이블이다.

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| member_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| project_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | PROJECT.project_id FK. (project_id + user_id) 복합 유니크 |
| user_id | UUID | 36 | Y | - | ✅ | ❌ | ❌ | USER.user_id FK |
| role | ENUM | - | Y | - | ✅ | ✅(Owner만) | ❌ | OWNER / EDITOR / VIEWER. 프로젝트 내 역할 |
| invited_by | UUID | 36 | N | - | ✅ | ❌ | ❌ | 초대한 User의 user_id. Owner가 프로젝트 생성 시에는 null |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |

단, 동일 프로젝트에 role = OWNER인 행은 최대 1개만 허용한다. 2개 이상 INSERT 시 System이 거부한다.

### 14.10. WORKSPACE

프로젝트 내 워크스페이스(작업 공간) 설정을 관리하는 테이블이다. 프로젝트당 1개의 워크스페이스를 가진다(1:1).

| 컬럼명 | 타입 | 길이 | 필수 | 유니크 | C | U | D | 제약 / 비고 |
|---|---|---|---|---|---|---|---|---|
| workspace_id | UUID | 36 | Y | Y | 자동채번 | ❌ | ❌ | PK |
| project_id | UUID | 36 | Y | Y | ✅ | ❌ | ❌ | PROJECT.project_id FK. 1:1 관계 |
| last_active_engine | ENUM | - | N | - | ✅(시스템) | ✅(시스템) | ❌ | PLAN / DESIGN / VISUAL / IMPLEMENT. 마지막으로 사용한 엔진 유형 |
| context_window_size | INTEGER | - | Y | - | ✅(기본 32000) | ✅(Owner만) | ❌ | 오케스트레이터의 컨텍스트 윈도우 크기(토큰). 최소 4000, 최대 128000 |
| auto_snapshot_enabled | BOOLEAN | - | Y | - | ✅(기본 true) | ✅(Owner만) | ❌ | true 시 주요 산출물 생성 완료 시 자동 스냅샷을 생성한다 |
| created_at | TIMESTAMP | - | Y | - | 자동채번 | ❌ | ❌ | UTC+0 |
| updated_at | TIMESTAMP | - | Y | - | ✅(시스템) | ✅(시스템) | ❌ | UTC+0 |

---

## 15. 상태 정의

### 15.1. 프로젝트 상태

| 코드 | 상태명 | 트리거 조건 | 전이 가능 상태 |
|---|---|---|---|
| PS001 | 활성 (Active) | System이 프로젝트를 생성한다 | 보관(PS002), 삭제(PS003) |
| PS002 | 보관 (Archived) | Owner가 프로젝트를 보관 처리한다 | 활성(PS001), 삭제(PS003) |
| PS003 | 삭제 (Deleted) | Owner가 프로젝트를 삭제한다 | - (복구 불가) |

단, PS003 상태로 전이 시 System이 "삭제된 프로젝트는 복구할 수 없습니다. 계속하시겠습니까?"를 출력하고 User의 재확인을 요구한다.

### 15.2. 이벤트 유형

| 코드 | 유형명 | 설명 |
|---|---|---|
| ET001 | PLAN | 기획 엔진에서 생성한 이벤트이다 |
| ET002 | DESIGN | 설계 엔진에서 생성한 이벤트이다 |
| ET003 | VISUAL | 시각 엔진에서 생성한 이벤트이다 |
| ET004 | IMPLEMENT | 구현 엔진에서 생성한 이벤트이다 |
| ET005 | SNAPSHOT | 스냅샷 생성 이벤트이다 (수동 또는 자동) |
| ET006 | ROLLBACK | 롤백 실행 이벤트이다 |
| ET007 | ERROR | 에러 발생 이벤트이다 |

### 15.3. 스냅샷 트리거 유형

| 코드 | 유형명 | 트리거 조건 |
|---|---|---|
| ST001 | MANUAL | User가 수동으로 스냅샷 생성 버튼을 클릭한다 |
| ST002 | AUTO | System이 주요 산출물 생성 완료 시 자동으로 스냅샷을 생성한다 |
| ST003 | PRE_ROLLBACK | System이 롤백 실행 직전에 현재 상태를 자동 백업한다 |

### 15.4. 사용자 계정 상태

| 코드 | 상태명 | 트리거 조건 | 전이 가능 상태 |
|---|---|---|---|
| US001 | 활성 (Active) | User가 Google OAuth 로그인에 성공한다 | 비활성(US002) |
| US002 | 비활성 (Inactive) | User 본인이 계정 비활성화를 요청하거나 System이 보안 정책에 의해 비활성화한다 | 활성(US001) |

단, 비활성(US002) 상태의 User가 로그인을 시도하면 System이 "계정이 비활성화되었습니다. 고객 지원에 문의해 주세요."를 출력하고 로그인을 거부한다.
※ 활성(US001) 복구는 User 본인이 고객 지원을 통해 요청한다.

### 15.5. 감사 로그 액션 유형

| 코드 | 액션명 | 설명 |
|---|---|---|
| AL001 | LOGIN | User가 로그인에 성공한다 |
| AL002 | LOGIN_FAILED | User가 로그인에 실패한다 (인증 거부, 비활성 계정 등) |
| AL003 | LOGOUT | User가 로그아웃한다 |
| AL004 | ROLE_CHANGE | Owner가 멤버의 역할을 변경한다 |
| AL005 | OWNER_TRANSFER | Owner가 프로젝트 소유권을 다른 멤버에게 양도한다 |
| AL006 | API_KEY_CHANGE | Owner가 외부 AI API 키를 등록·변경·삭제한다 |
| AL007 | SPACE_CONNECT | User가 구글 스페이스를 연동한다 |
| AL008 | SPACE_DISCONNECT | User가 구글 스페이스 연동을 해제한다 |
| AL009 | SESSION_FORCE_REVOKE | System 또는 Owner가 세션을 강제 종료한다 |
| AL010 | MEMBER_INVITE | Owner가 프로젝트에 새 멤버를 초대한다 |
| AL011 | MEMBER_REMOVE | Owner가 프로젝트에서 멤버를 제거한다 |
| AL012 | PROJECT_CREATE | User가 새 프로젝트를 생성한다 |
| AL013 | PROJECT_DELETE | Owner가 프로젝트를 삭제한다 (논리 삭제 시점에 기록) |
| AL014 | PROJECT_ARCHIVE | Owner가 프로젝트를 보관 처리한다 |
| AL015 | PROJECT_RESTORE | Owner가 보관된 프로젝트를 복구한다 |
| AL016 | CLAUDE_CLI_ENABLE | User가 Claude Code CLI 모드를 활성화한다 |
| AL017 | CLAUDE_CLI_DISABLE | User가 Claude Code CLI 모드를 비활성화한다 |

---

# 영향 범위 (Impact)

| 영역 | 영향 내용 | 비고 |
|---|---|---|
| Client | Electron 기반 PC 앱 신규 개발. React + TypeScript 프론트엔드 | Tailwind CSS 적용 |
| Server | Node.js 기반 오케스트레이터, 4개 AI 엔진 통합 모듈, WebSocket 서버 | 로컬 실행, 외부 API 연동 |
| Database | NoSQL DB(이벤트 로그), Vector DB(스냅샷 컨텍스트), Local Git(코드/에셋) | SQLite 또는 LevelDB 검토 |
| UI | 대시보드, 워크스페이스, 타임라인, 위키, 로그인, 설정(인증/API키/세션) 6개 주요 화면 | Figma 디자인 필요 |

---

# 승인 기록

| 역할 | 이름 | 승인일 | 서명 |
|---|---|---|---|
| 작성자 | 안승준 | 2026-04-09 | - |
| 검토자 | [이름] | - | - |
| 승인자 | [이름] | - | - |

---

**문서 끝**
