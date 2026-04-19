> **공통 섹션 참조**: 메타데이터, 용어 정리, 예외 처리, 영향 범위, 변경 이력, 승인 기록은 `공통섹션/` 폴더를 참조한다.

---
# [프로젝트명] - API 명세서
## [API명 또는 모듈명] API 명세
---

## 메타데이터

| 항목 | 내용 |
|------|------|
| **문서 제목** | [API명] API 명세서 |
| **문서 버전** | v1.0 |
| **작성일** | YYYY-MM-DD |
| **작성자** | [이름] |
| **문서 상태** | 🔄 작성 중 |
| **API명** | [API명] |
| **Base URL** | `https://api.[프로젝트명].com/v1` |
| **인증 방식** | Firebase Auth Bearer Token |

---

## 1. API 개요

### 설명
> 본 API가 제공하는 기능에 대한 설명을 기술한다.

### 주요 기능
- 기능 1
- 기능 2
- 기능 3

---

## 2. 인증

### 2.1. 인증 방법
모든 API 요청은 Firebase Auth 토큰을 필요로 한다.

**헤더 설정:**
```http
Authorization: Bearer <firebase_id_token>
Content-Type: application/json
```

**예시:**
```bash
curl -X POST https://api.[프로젝트명].com/v1/endpoint \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json"
```

---

## 3. API 엔드포인트

### 1. [기능명] - [메소드] /endpoint

#### 설명
> 본 엔드포인트의 역할을 기술한다.

#### HTTP 메소드
`POST` / `GET` / `PUT` / `DELETE`

#### URL
```
POST /api/v1/[endpoint]
```

#### 요청 헤더

| 헤더명 | 필수 | 타입 | 설명 |
|--------|------|------|------|
| Authorization | ✅ | string | Firebase ID Token |
| Content-Type | ✅ | string | `application/json` |

#### 요청 바디

```typescript
interface RequestBody {
  field1: string;        // 설명
  field2: number;        // 설명
  field3?: boolean;      // 선택 필드 설명
}
```

**예시:**
```json
{
  "field1": "value1",
  "field2": 100,
  "field3": true
}
```

#### 요청 파라미터

| 파라미터 | 위치 | 필수 | 타입 | 설명 | 예시 |
|---------|------|------|------|------|------|
| userId | path | ✅ | string | 사용자 ID | `"user123"` |
| page | query | ❌ | number | 페이지 번호 | `1` |
| limit | query | ❌ | number | 페이지당 개수 | `20` |

#### 검증 규칙

| 필드 | 규칙 | 설명 |
|------|------|------|
| field1 | 필수, 1-100자 | 빈 문자열 불가 |
| field2 | 필수, 0 이상 | 음수 불가 |
| field3 | 선택, boolean | true/false만 허용 |

#### 응답

**성공 응답 (200 OK)**
```typescript
interface SuccessResponse {
  success: true;
  data: {
    id: string;
    result: string;
    timestamp: string;
  };
  message: string;
}
```

**예시:**
```json
{
  "success": true,
  "data": {
    "id": "abc123",
    "result": "처리 완료",
    "timestamp": "2026-02-03T12:00:00Z"
  },
  "message": "요청이 성공적으로 처리되었다"
}
```

#### 에러 응답

**일반 에러 형식**
```typescript
interface ErrorResponse {
  success: false;
  error: {
    code: string;
    message: string;
    details?: any;
  };
}
```

**에러 코드**

| 상태 코드 | 에러 코드 | 메시지 | 설명 |
|----------|----------|--------|------|
| 400 | `invalid-input` | 입력값이 올바르지 않다 | 필수 필드 누락 또는 형식 오류 |
| 401 | `unauthorized` | 인증이 필요하다 | 토큰 없음 또는 만료됨 |
| 403 | `forbidden` | 권한이 없다 | 접근 권한 없음 |
| 404 | `not-found` | 리소스를 찾을 수 없다 | 존재하지 않는 리소스 |
| 409 | `conflict` | 충돌이 발생했다 | 중복 데이터 등 |
| 429 | `rate-limit` | 요청 횟수를 초과했다 | Rate limit 초과 |
| 500 | `internal-error` | 서버 오류가 발생했다 | 서버 내부 오류 |

**에러 예시:**
```json
{
  "success": false,
  "error": {
    "code": "invalid-input",
    "message": "field1은 필수 입력값이다",
    "details": {
      "field": "field1",
      "expected": "string (1-100자)",
      "received": ""
    }
  }
}
```

#### 비즈니스 로직 흐름

1. **입력 검증**
   - Server가 필수 필드를 확인한다
   - Server가 타입을 검증한다
   - Server가 값 범위를 검증한다

2. **인증/인가 확인**
   - Server가 토큰 유효성을 검증한다
   - Server가 권한을 확인한다

3. **데이터 처리**
   - Server가 [처리 단계 1]을 수행한다
   - Server가 [처리 단계 2]를 수행한다
   - Server가 [처리 단계 3]을 수행한다

4. **응답 생성**
   - Server가 결과 데이터를 구성한다
   - Server가 응답을 반환한다

#### 사용 예시

**JavaScript/TypeScript**
```typescript
const response = await fetch('https://api.[프로젝트명].com/v1/endpoint', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${idToken}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    field1: 'value1',
    field2: 100
  })
});

const result = await response.json();
```

**cURL**
```bash
curl -X POST https://api.[프로젝트명].com/v1/endpoint \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "field1": "value1",
    "field2": 100
  }'
```

#### Rate Limit
- **제한**: 100 요청/분
- **헤더**: `X-RateLimit-Remaining`, `X-RateLimit-Reset`

#### 주의사항
- [주의사항 1]
- [주의사항 2]

---

### 2. [다른 엔드포인트] - GET /another-endpoint

[위와 동일한 형식으로 작성한다]

---

## 4. 데이터 모델

### [모델명 1]

```typescript
interface ModelName {
  id: string;              // 고유 ID
  name: string;            // 이름
  createdAt: Timestamp;    // 생성 일시
  updatedAt: Timestamp;    // 수정 일시
}
```

**예시:**
```json
{
  "id": "model123",
  "name": "Example",
  "createdAt": "2026-02-03T12:00:00Z",
  "updatedAt": "2026-02-03T12:00:00Z"
}
```

---

## 5. Webhook

### [Webhook 이벤트명]

#### 설명
> 특정 이벤트 발생 시 Server가 등록된 URL로 POST 요청을 전송한다.

#### 페이로드

```json
{
  "event": "event.name",
  "timestamp": "2026-02-03T12:00:00Z",
  "data": {
    "field1": "value1"
  }
}
```

#### 서명 검증

요청 헤더의 `X-Signature`를 사용하여 검증한다.

```typescript
const signature = crypto
  .createHmac('sha256', webhookSecret)
  .update(JSON.stringify(payload))
  .digest('hex');
```

---

## 6. 상태 코드 정리

| 코드 | 의미 | 설명 |
|------|------|------|
| 200 | OK | 요청 성공 |
| 201 | Created | 리소스 생성 성공 |
| 204 | No Content | 요청 성공, 응답 본문 없음 |
| 400 | Bad Request | 잘못된 요청 |
| 401 | Unauthorized | 인증 실패 |
| 403 | Forbidden | 권한 없음 |
| 404 | Not Found | 리소스 없음 |
| 409 | Conflict | 충돌 |
| 429 | Too Many Requests | Rate limit 초과 |
| 500 | Internal Server Error | 서버 오류 |
| 503 | Service Unavailable | 서비스 이용 불가 |

---

## 7. 테스트

### 7.1. 테스트 환경

| 환경 | URL |
|------|-----|
| 개발 | `https://dev-api.[프로젝트명].com/v1` |
| 스테이징 | `https://staging-api.[프로젝트명].com/v1` |
| 프로덕션 | `https://api.[프로젝트명].com/v1` |

### Postman Collection
[Postman Collection 링크](링크)

### 테스트 케이스

#### 정상 케이스
```json
// 요청
{
  "field1": "valid value",
  "field2": 100
}

// 예상 응답
{
  "success": true,
  "data": {...}
}
```

#### 에러 케이스
```json
// 요청
{
  "field2": 100
  // field1 누락
}

// 예상 응답
{
  "success": false,
  "error": {
    "code": "invalid-input",
    "message": "field1은 필수 입력값이다"
  }
}
```

---

## 8. 참고 자료

### 8.1. 관련 문서
- [기획서](링크)
- [데이터베이스 스키마](링크)

### 8.2. 외부 문서
- [Firebase Authentication](https://firebase.google.com/docs/auth)
- [REST API 설계 가이드](링크)

---

## 변경 이력

| 버전 | 날짜 | 작성자 | 변경 내용 |
|------|------|--------|----------|
| v1.0 | YYYY-MM-DD | [이름] | 초안 작성 |
