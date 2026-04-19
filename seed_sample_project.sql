-- 개발용 샘플 프로젝트 시드
-- 소유자: jooniho@gmail.com (기존 유저)
-- 목적: Classifier 랜덤 배정 + 에이전트 간 handoff 동작 확인

BEGIN;

-- 기존에 있으면 재생성할 수 있도록 cascade 정리 (고정 project_id 사용)
DELETE FROM persona        WHERE project_id = 'sample-dev-001';
DELETE FROM workspace      WHERE project_id = 'sample-dev-001';
DELETE FROM project_member WHERE project_id = 'sample-dev-001';
DELETE FROM project        WHERE project_id = 'sample-dev-001';

-- Project
INSERT INTO project (
    project_id, project_name, description, hierarchy_type, parent_project_id,
    owner_user_id, git_repo_path, status, created_at, updated_at
) VALUES (
    'sample-dev-001',
    '샘플 프로젝트 (개발용)',
    '멀티 에이전트 handoff 흐름을 테스트하기 위한 개발용 샘플 프로젝트.',
    'ROOT', NULL,
    (SELECT user_id FROM "user" WHERE email = 'jooniho@gmail.com'),
    'C:\Users\Joon\AppData\Local\AgentPaw\repos\sample-dev-001',
    'ACTIVE', NOW(), NOW()
);

-- Project member (OWNER)
INSERT INTO project_member (
    member_id, project_id, user_id, role, invited_by, created_at
) VALUES (
    gen_random_uuid()::text,
    'sample-dev-001',
    (SELECT user_id FROM "user" WHERE email = 'jooniho@gmail.com'),
    'OWNER', NULL, NOW()
);

-- Workspace
INSERT INTO workspace (
    workspace_id, project_id, last_active_engine, context_window_size,
    auto_snapshot_enabled, created_at, updated_at
) VALUES (
    gen_random_uuid()::text,
    'sample-dev-001',
    NULL, 32000, TRUE, NOW(), NOW()
);

-- Personas (4개 — PM 허브 + 3개 도메인 전문가)
INSERT INTO persona (
    persona_id, project_id, group_id, name, label, description, avatar,
    system_prompt, instructions, keywords, icon, color, sort_order,
    is_builtin, is_pm, primary_model, fallback_model, temperature, max_tokens,
    created_at, updated_at
) VALUES
(
    gen_random_uuid()::text, 'sample-dev-001', NULL,
    'PM', '프로젝트 관리자',
    '모든 지시의 우선 수신·역할 배정·산출물 취합·종료 보고를 담당하는 허브 페르소나.',
    '',
    '당신은 프로젝트 관리자(PM) 에이전트다. User의 모든 지시를 최우선으로 수신하여 의도를 해석하고, 작업을 수행할 적절한 동료 페르소나를 선택하여 handoff 블록으로 위임한다. 역할 페르소나가 산출물을 반환하면 검토 후 다음 지시를 내리거나 User에게 개입을 요청하거나 종료 보고를 수행한다. 단독으로 응답하지 않고 반드시 handoff 블록으로 다음 행동 주체를 지정한다.',
    '', 'pm,프로젝트관리자,총괄,조율,허브,보고,배정,planner',
    'compass', 'indigo', -1,
    FALSE, TRUE, 'claude-opus', 'claude-sonnet', 0.5, 4096,
    NOW(), NOW()
),
(
    gen_random_uuid()::text, 'sample-dev-001', NULL,
    'SOFTWARE', '소프트웨어 개발',
    '요구사항·설계·구현·테스트 안내',
    '',
    '당신은 소프트웨어 개발 전문 에이전트다. 요구사항 분석, 아키텍처 설계, API 명세, 소스 코드, 테스트를 돕는다.',
    '', '개발,코드,프로그래밍,소프트웨어,api,typescript,react,node,테스트,디버깅,리팩터링,아키텍처,erd,스키마',
    'code', 'blue', 1,
    FALSE, FALSE, 'claude-sonnet', 'gemini-flash', 0.7, 4096,
    NOW(), NOW()
),
(
    gen_random_uuid()::text, 'sample-dev-001', NULL,
    'NOVEL', '소설 제작',
    '세계관·캐릭터·플롯·원고 집필 안내',
    '',
    '당신은 소설 집필 전문 에이전트다. 세계관 구축, 캐릭터 시트, 플롯 설계, 원고 생성을 돕는다.',
    '', '소설,원고,시놉시스,플롯,캐릭터,세계관,설정,집필,장르,단편,장편,로맨스,판타지,sf,본문',
    'book-open', 'amber', 2,
    FALSE, FALSE, 'claude-opus', 'claude-sonnet', 0.7, 4096,
    NOW(), NOW()
),
(
    gen_random_uuid()::text, 'sample-dev-001', NULL,
    'VIDEO', '영상 제작',
    '영상 기획·시나리오·스토리보드·편집 구성 안내',
    '',
    '당신은 영상 제작 전문 에이전트다. 기획안, 시놉시스, 씬 스크립트, 촬영 스토리보드, 편집 구성을 돕는다.',
    '', '영상,비디오,유튜브,쇼츠,시나리오,콘티,편집,스토리보드,촬영,나레이션',
    'film', 'red', 3,
    FALSE, FALSE, 'claude-sonnet', 'gemini-flash', 0.7, 4096,
    NOW(), NOW()
);

COMMIT;

-- 결과 확인
SELECT p.project_id, p.project_name, p.owner_user_id
FROM project p WHERE p.project_id = 'sample-dev-001';

SELECT name, label, is_pm, keywords FROM persona
WHERE project_id = 'sample-dev-001'
ORDER BY sort_order;
