-- V001 · Catálogo e identidad.
-- Todo cambio de esquema va por migración versionada, nunca a mano (ADR-006).
-- Las migraciones son de solo avance: un error se corrige con una migración nueva.

-- ─────────────────────────────────────────────────────────────────────────────
-- Identidad. ASP.NET Core Identity sin EF Core: el esquema es nuestro y el
-- acceso es Dapper (ADR-004). Nombres en snake_case como el resto de la BD.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE identity_user (
    id                      uuid PRIMARY KEY,
    -- Normalizado en mayúsculas para búsquedas insensibles a mayúsculas sin ILIKE.
    normalized_email        varchar(256) NOT NULL,
    email                   varchar(256) NOT NULL,
    email_confirmed         boolean      NOT NULL DEFAULT false,
    password_hash           text         NULL,
    security_stamp          text         NOT NULL,
    concurrency_stamp       text         NOT NULL,
    lockout_end             timestamptz  NULL,
    lockout_enabled         boolean      NOT NULL DEFAULT true,
    access_failed_count     integer      NOT NULL DEFAULT 0,
    created_at              timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_identity_user_normalized_email ON identity_user (normalized_email);

CREATE TABLE identity_user_role (
    user_id  uuid        NOT NULL REFERENCES identity_user (id) ON DELETE CASCADE,
    role     varchar(32) NOT NULL,
    PRIMARY KEY (user_id, role)
);

-- Tokens de verificación de email y de reset de contraseña. El valor se guarda
-- hasheado: un volcado de la tabla no permite tomar cuentas.
CREATE TABLE identity_user_token (
    id           uuid PRIMARY KEY,
    user_id      uuid        NOT NULL REFERENCES identity_user (id) ON DELETE CASCADE,
    purpose      varchar(64) NOT NULL,
    token_hash   text        NOT NULL,
    expires_at   timestamptz NOT NULL,
    consumed_at  timestamptz NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_identity_user_token_lookup ON identity_user_token (token_hash) WHERE consumed_at IS NULL;
CREATE INDEX ix_identity_user_token_user ON identity_user_token (user_id, purpose);

-- Refresh tokens rotatorios. Se guarda el hash y la familia para poder invalidar
-- toda la cadena si se detecta reutilización de un token ya rotado.
CREATE TABLE identity_refresh_token (
    id           uuid PRIMARY KEY,
    user_id      uuid        NOT NULL REFERENCES identity_user (id) ON DELETE CASCADE,
    family_id    uuid        NOT NULL,
    token_hash   text        NOT NULL,
    expires_at   timestamptz NOT NULL,
    revoked_at   timestamptz NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_identity_refresh_token_hash ON identity_refresh_token (token_hash);
CREATE INDEX ix_identity_refresh_token_family ON identity_refresh_token (family_id);

-- Logins sociales opcionales (Google/GitHub).
CREATE TABLE identity_user_login (
    provider      varchar(32)  NOT NULL,
    provider_key  varchar(256) NOT NULL,
    user_id       uuid         NOT NULL REFERENCES identity_user (id) ON DELETE CASCADE,
    PRIMARY KEY (provider, provider_key)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- Usuario de aplicación. Separado de identity_user para que el dominio no
-- dependa del esquema de credenciales.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE app_user (
    id                  uuid PRIMARY KEY REFERENCES identity_user (id) ON DELETE CASCADE,
    email               varchar(256) NOT NULL,
    display_name        varchar(120) NOT NULL,
    email_confirmed     boolean      NOT NULL DEFAULT false,
    stripe_customer_id  varchar(64)  NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    deleted_at          timestamptz  NULL
);

CREATE UNIQUE INDEX ux_app_user_email ON app_user (lower(email));
CREATE UNIQUE INDEX ux_app_user_stripe_customer ON app_user (stripe_customer_id)
    WHERE stripe_customer_id IS NOT NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- Producto: la unidad de entitlement (ADR-007). Curso, pack y programa son
-- productos, así que IAccessPolicy hace una sola pregunta.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE product (
    id                  uuid PRIMARY KEY,
    type                varchar(16)  NOT NULL CHECK (type IN ('course', 'pack', 'program')),
    slug                varchar(120) NOT NULL,
    title               varchar(200) NOT NULL,
    one_off_price_cents bigint       NULL CHECK (one_off_price_cents IS NULL OR one_off_price_cents >= 0),
    currency            char(3)      NOT NULL DEFAULT 'EUR',
    stripe_price_id     varchar(64)  NULL,
    created_at          timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_product_slug ON product (slug);
CREATE INDEX ix_product_type ON product (type);

CREATE TABLE course (
    id                 uuid PRIMARY KEY,
    product_id         uuid         NOT NULL REFERENCES product (id) ON DELETE RESTRICT,
    slug               varchar(120) NOT NULL,
    title              varchar(200) NOT NULL,
    short_description  text         NOT NULL,
    long_description   text         NOT NULL DEFAULT '',
    cover_image_url    text         NULL,
    status             varchar(16)  NOT NULL CHECK (status IN ('draft', 'published', 'comingsoon')),
    level              varchar(16)  NOT NULL CHECK (level IN ('intro', 'intermediate', 'advanced')),
    is_featured        boolean      NOT NULL DEFAULT false,
    is_new             boolean      NOT NULL DEFAULT false,
    created_at         timestamptz  NOT NULL DEFAULT now(),
    published_at       timestamptz  NULL
);

CREATE UNIQUE INDEX ux_course_slug ON course (slug);
CREATE UNIQUE INDEX ux_course_product ON course (product_id);
-- El catálogo público filtra por estado en cada petición.
CREATE INDEX ix_course_status ON course (status) WHERE status <> 'draft';

CREATE TABLE section (
    id          uuid PRIMARY KEY,
    course_id   uuid         NOT NULL REFERENCES course (id) ON DELETE CASCADE,
    sort_order  integer      NOT NULL CHECK (sort_order >= 0),
    title       varchar(200) NOT NULL
);

CREATE INDEX ix_section_course ON section (course_id, sort_order);

CREATE TABLE lesson (
    id                uuid PRIMARY KEY,
    section_id        uuid         NOT NULL REFERENCES section (id) ON DELETE CASCADE,
    sort_order        integer      NOT NULL CHECK (sort_order >= 0),
    slug              varchar(120) NOT NULL,
    title             varchar(200) NOT NULL,
    type              varchar(16)  NOT NULL CHECK (type IN ('slides', 'video', 'lab', 'quiz', 'download')),
    duration_minutes  integer      NOT NULL CHECK (duration_minutes >= 0),
    content_ref       text         NOT NULL,
    is_free_preview   boolean      NOT NULL DEFAULT false,
    is_required       boolean      NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX ux_lesson_section_slug ON lesson (section_id, slug);
CREATE INDEX ix_lesson_section ON lesson (section_id, sort_order);

-- ─────────────────────────────────────────────────────────────────────────────
-- Packs sectoriales y programa.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE pack (
    id                     uuid PRIMARY KEY,
    product_id             uuid         NOT NULL REFERENCES product (id) ON DELETE RESTRICT,
    slug                   varchar(120) NOT NULL,
    title                  varchar(200) NOT NULL,
    sector                 varchar(80)  NOT NULL,
    version                varchar(20)  NOT NULL,
    changelog              text         NULL,
    status                 varchar(16)  NOT NULL CHECK (status IN ('draft', 'published', 'comingsoon')),
    regulatory_check_date  date         NULL,
    updated_at             timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_pack_slug ON pack (slug);
CREATE UNIQUE INDEX ux_pack_product ON pack (product_id);

CREATE TABLE pack_file (
    id             uuid PRIMARY KEY,
    pack_id        uuid         NOT NULL REFERENCES pack (id) ON DELETE CASCADE,
    file_name      varchar(200) NOT NULL,
    content_ref    text         NOT NULL,
    size_in_bytes  bigint       NOT NULL CHECK (size_in_bytes >= 0),
    sha256         char(64)     NOT NULL,
    sort_order     integer      NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX ux_pack_file_name ON pack_file (pack_id, lower(file_name));

CREATE TABLE learning_program (
    id                          uuid PRIMARY KEY,
    product_id                  uuid         NOT NULL REFERENCES product (id) ON DELETE RESTRICT,
    slug                        varchar(120) NOT NULL,
    title                       varchar(200) NOT NULL,
    -- Códigos de plan cuyos suscriptores reciben todos los packs (T-04 añadido).
    plan_codes_including_packs  text[]       NOT NULL DEFAULT '{}'
);

CREATE UNIQUE INDEX ux_learning_program_slug ON learning_program (slug);

CREATE TABLE program_item (
    program_id                uuid    NOT NULL REFERENCES learning_program (id) ON DELETE CASCADE,
    course_id                 uuid    NOT NULL REFERENCES course (id) ON DELETE CASCADE,
    sort_order                integer NOT NULL,
    prerequisite_course_ids   uuid[]  NOT NULL DEFAULT '{}',
    PRIMARY KEY (program_id, course_id)
);
