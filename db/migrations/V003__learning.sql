-- V003 · Progreso, cuestionarios y certificados.

CREATE TABLE lesson_progress (
    user_id            uuid        NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,
    lesson_id          uuid        NOT NULL REFERENCES lesson (id) ON DELETE CASCADE,
    completed_at       timestamptz NULL,
    last_position_ref  varchar(200) NULL,
    updated_at         timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, lesson_id)
);

-- Consulta de progreso por curso: se resuelve por lesson_id contra las lecciones del curso.
CREATE INDEX ix_lesson_progress_lesson ON lesson_progress (lesson_id) WHERE completed_at IS NOT NULL;

CREATE TABLE quiz (
    id         uuid PRIMARY KEY,
    slug       varchar(120) NOT NULL,
    title      varchar(200) NOT NULL,
    kind       varchar(16)  NOT NULL CHECK (kind IN ('admission', 'blockcheck')),
    course_id  uuid         NULL REFERENCES course (id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX ux_quiz_slug ON quiz (slug);

CREATE TABLE quiz_question (
    id           uuid PRIMARY KEY,
    quiz_id      uuid         NOT NULL REFERENCES quiz (id) ON DELETE CASCADE,
    sort_order   integer      NOT NULL,
    category     varchar(80)  NOT NULL,
    text         text         NOT NULL,
    explanation  text         NOT NULL DEFAULT '',
    -- [{ "index": 0, "text": "...", "isCorrect": false }, ...]
    options_json jsonb        NOT NULL
);

CREATE INDEX ix_quiz_question_quiz ON quiz_question (quiz_id, sort_order);

CREATE TABLE quiz_attempt (
    id               uuid PRIMARY KEY,
    quiz_id          uuid        NOT NULL REFERENCES quiz (id) ON DELETE CASCADE,
    user_id          uuid        NULL REFERENCES app_user (id) ON DELETE CASCADE,
    -- Cookie opaca que permite reclamar el intento tras registrarse (T-08).
    anonymous_key    varchar(64) NULL,
    score            integer     NOT NULL CHECK (score >= 0),
    total_questions  integer     NOT NULL CHECK (total_questions > 0),
    verdict          varchar(20) NOT NULL CHECK (verdict IN ('ready', 'almost', 'comebacklater')),
    answers_json     jsonb       NOT NULL,
    taken_at         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_quiz_attempt_owner CHECK (user_id IS NOT NULL OR anonymous_key IS NOT NULL)
);

CREATE INDEX ix_quiz_attempt_user ON quiz_attempt (user_id, taken_at DESC);
CREATE INDEX ix_quiz_attempt_anonymous ON quiz_attempt (anonymous_key) WHERE anonymous_key IS NOT NULL;

CREATE TABLE certificate (
    id                 uuid PRIMARY KEY,
    code               varchar(20)  NOT NULL,
    scope              varchar(16)  NOT NULL CHECK (scope IN ('course', 'program')),
    user_id            uuid         NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,
    -- course.id o learning_program.id según scope; sin FK porque apunta a dos tablas.
    subject_id         uuid         NOT NULL,
    issued_at          timestamptz  NOT NULL,
    hash               char(64)     NOT NULL,
    pdf_content_ref    text         NULL,
    revoked_at         timestamptz  NULL,
    revocation_reason  text         NULL
);

-- La verificación pública busca por código; es la consulta más expuesta del sistema.
CREATE UNIQUE INDEX ux_certificate_code ON certificate (code);
-- Un certificado por (usuario, curso o programa): la emisión es idempotente.
CREATE UNIQUE INDEX ux_certificate_user_subject ON certificate (user_id, subject_id);
