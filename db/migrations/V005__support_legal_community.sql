-- V005 · Roadmap, waitlist, auditoría, descargas, comunidad, facturación fiscal y consentimiento.

-- ── Roadmap (T-09) ───────────────────────────────────────────────────────────

CREATE TABLE roadmap_node (
    id                uuid PRIMARY KEY,
    -- Clave estable usada en las aristas; sobrevive a renombrar el título.
    node_key          varchar(60)  NOT NULL,
    title             varchar(200) NOT NULL,
    description       text         NOT NULL DEFAULT '',
    course_id         uuid         NULL REFERENCES course (id) ON DELETE SET NULL,
    lesson_id         uuid         NULL REFERENCES lesson (id) ON DELETE SET NULL,
    sort_order        integer      NOT NULL DEFAULT 0,
    prerequisite_keys text[]       NOT NULL DEFAULT '{}'
);

CREATE UNIQUE INDEX ux_roadmap_node_key ON roadmap_node (node_key);

-- ── Waitlist de cursos "próximamente" (T-06) ─────────────────────────────────

CREATE TABLE waitlist (
    id          uuid PRIMARY KEY,
    course_id   uuid         NOT NULL REFERENCES course (id) ON DELETE CASCADE,
    email       varchar(256) NOT NULL,
    created_at  timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_waitlist_course_email ON waitlist (course_id, lower(email));

-- ── Auditoría de acciones admin (T-11). Append-only. ─────────────────────────

CREATE TABLE audit_log (
    id             uuid PRIMARY KEY,
    actor_user_id  uuid         NOT NULL REFERENCES app_user (id) ON DELETE RESTRICT,
    action         varchar(80)  NOT NULL,
    entity_type    varchar(60)  NOT NULL,
    entity_id      varchar(80)  NULL,
    details_json   jsonb        NULL,
    occurred_at    timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX ix_audit_log_recent ON audit_log (occurred_at DESC);
CREATE INDEX ix_audit_log_entity ON audit_log (entity_type, entity_id);

-- ── Registro de descargas de packs (T-07 añadido) ────────────────────────────

CREATE TABLE download_log (
    id             uuid PRIMARY KEY,
    user_id        uuid         NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,
    pack_file_id   uuid         NOT NULL REFERENCES pack_file (id) ON DELETE CASCADE,
    pack_version   varchar(20)  NOT NULL,
    downloaded_at  timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX ix_download_log_file ON download_log (pack_file_id, downloaded_at DESC);
CREATE INDEX ix_download_log_user ON download_log (user_id, downloaded_at DESC);

-- ── Comunidad (T-12) ─────────────────────────────────────────────────────────

CREATE TABLE community_session (
    id                   uuid PRIMARY KEY,
    title                varchar(200) NOT NULL,
    description          text         NOT NULL DEFAULT '',
    starts_at            timestamptz  NOT NULL,
    duration_minutes     integer      NOT NULL CHECK (duration_minutes > 0),
    join_url             text         NOT NULL,
    required_plan_codes  text[]       NOT NULL DEFAULT '{}'
);

CREATE INDEX ix_community_session_upcoming ON community_session (starts_at);

CREATE TABLE discord_link (
    user_id           uuid        PRIMARY KEY REFERENCES app_user (id) ON DELETE CASCADE,
    discord_user_id   varchar(40) NOT NULL,
    linked_at         timestamptz NOT NULL DEFAULT now(),
    last_applied_role varchar(40) NULL
);

CREATE UNIQUE INDEX ux_discord_link_discord_user ON discord_link (discord_user_id);

-- ── Facturación fiscal Verifactu (T-14) ──────────────────────────────────────
-- El encadenamiento por hash es obligatorio: cada factura referencia el hash de
-- la anterior de su serie. Por eso número y serie son únicos y no se reutilizan.

CREATE TABLE fiscal_invoice (
    id                   uuid PRIMARY KEY,
    user_id              uuid         NOT NULL REFERENCES app_user (id) ON DELETE RESTRICT,
    series               varchar(10)  NOT NULL,
    number               integer      NOT NULL CHECK (number > 0),
    issue_date           date         NOT NULL,
    total_cents          bigint       NOT NULL,
    tax_cents            bigint       NOT NULL DEFAULT 0,
    currency             char(3)      NOT NULL DEFAULT 'EUR',
    stripe_invoice_id    varchar(64)  NOT NULL,
    previous_hash        char(64)     NOT NULL,
    hash                 char(64)     NOT NULL,
    pdf_content_ref      text         NULL,
    is_rectification     boolean      NOT NULL DEFAULT false,
    rectifies_invoice_id uuid         NULL REFERENCES fiscal_invoice (id) ON DELETE RESTRICT,
    aeat_submission_id   varchar(80)  NULL,
    created_at           timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_fiscal_invoice_series_number ON fiscal_invoice (series, number);
CREATE UNIQUE INDEX ux_fiscal_invoice_stripe ON fiscal_invoice (stripe_invoice_id) WHERE NOT is_rectification;
CREATE INDEX ix_fiscal_invoice_user ON fiscal_invoice (user_id, issue_date DESC);

-- ── Consentimiento de cookies y registro de tratamientos (T-14) ──────────────

CREATE TABLE consent_record (
    id              uuid PRIMARY KEY,
    user_id         uuid        NULL REFERENCES app_user (id) ON DELETE SET NULL,
    visitor_id      varchar(64) NOT NULL,
    analytics       boolean     NOT NULL DEFAULT false,
    marketing       boolean     NOT NULL DEFAULT false,
    policy_version  varchar(20) NOT NULL,
    recorded_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_consent_visitor ON consent_record (visitor_id, recorded_at DESC);
