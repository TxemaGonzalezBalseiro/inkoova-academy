-- V007 · Tutorías: catálogo de paquetes, lo concedido a cada alumno y lo consumido.
--
-- Una tutoría no es contenido, es tiempo. Por eso NO se modela como entitlement: un
-- entitlement responde "¿puede ver esto?" y no se gasta (ADR-007), mientras que una tutoría
-- se agota. Mezclarlas obligaría a que IAccessPolicy supiera restar horas, que es justo lo
-- que esa política no debe hacer.
--
-- Tres tablas y una sola regla: el saldo NUNCA se guarda. Se calcula restando lo consumido
-- de lo concedido. Un contador guardado se desincroniza en cuanto alguien corrige una sesión
-- a mano en la base; una resta no puede.

-- ── catálogo ──────────────────────────────────────────────────────────────────────────────
-- Lo que el negocio vende: "Pack de 5 tutorías", con su importe y sus horas. Se edita desde
-- administración sin desplegar, igual que los planes.
CREATE TABLE IF NOT EXISTS tutoring_package (
    id            uuid         PRIMARY KEY,
    slug          varchar(80)  NOT NULL UNIQUE,
    name          varchar(160) NOT NULL,
    description   text         NOT NULL DEFAULT '',

    -- Minutos, no horas. Media hora en decimal es 0,5 y en coma flotante deja de sumar
    -- exacto; en minutos el saldo es aritmética entera y siempre cuadra. La interfaz enseña
    -- horas, la base cuenta minutos.
    minutes       integer      NOT NULL CHECK (minutes > 0),

    price_cents   bigint       NOT NULL CHECK (price_cents >= 0),
    currency      char(3)      NOT NULL DEFAULT 'EUR',

    -- Caducidad en días desde la concesión. NULL = no caduca. Vive aquí como valor por
    -- defecto del paquete, pero la fecha real se calcula y se guarda en cada concesión:
    -- cambiar el paquete no debe acortar el plazo de quien ya lo compró.
    validity_days integer      NULL CHECK (validity_days IS NULL OR validity_days > 0),

    display_order integer      NOT NULL DEFAULT 0,
    is_active     boolean      NOT NULL DEFAULT true,
    created_at    timestamptz  NOT NULL DEFAULT now(),
    updated_at    timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_tutoring_package_active
    ON tutoring_package (is_active, display_order);

-- ── concesiones ───────────────────────────────────────────────────────────────────────────
-- Lo que un alumno concreto tiene. Una fila por compra o por concesión manual.
CREATE TABLE IF NOT EXISTS tutoring_grant (
    id             uuid         PRIMARY KEY,
    user_id        uuid         NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,

    -- De qué paquete salió. SET NULL: retirar un paquete del catálogo no puede borrar las
    -- horas de nadie. El nombre y los minutos se copian abajo justamente para eso.
    package_id     uuid         NULL REFERENCES tutoring_package (id) ON DELETE SET NULL,

    -- Copiados en el momento de conceder. Si el paquete pasa de 5 a 10 horas el mes que
    -- viene, quien compró 5 sigue teniendo 5. Sin esta copia, editar el catálogo reescribiría
    -- el pasado de todos los alumnos.
    package_name   varchar(160) NOT NULL,
    minutes_total  integer      NOT NULL CHECK (minutes_total > 0),

    -- 'purchase' (pagado) o 'manual' (beca, acuerdo con empresa, cortesía). La misma
    -- distinción que en entitlement, para poder auditar de dónde salió cada hora.
    source         varchar(16)  NOT NULL CHECK (source IN ('purchase', 'manual')),
    purchase_id    uuid         NULL REFERENCES purchase (id) ON DELETE SET NULL,

    granted_at     timestamptz  NOT NULL DEFAULT now(),
    expires_at     timestamptz  NULL,

    -- Se revoca, no se borra: las sesiones ya dadas tienen que seguir explicando el saldo.
    revoked_at     timestamptz  NULL,
    revoked_reason varchar(300) NULL,

    note           varchar(300) NOT NULL DEFAULT '',
    granted_by     uuid         NULL REFERENCES app_user (id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_tutoring_grant_user ON tutoring_grant (user_id, granted_at DESC);

-- ── consumo ───────────────────────────────────────────────────────────────────────────────
-- Cada tutoría dada. Es el libro mayor: el saldo es la resta de esto contra minutes_total.
CREATE TABLE IF NOT EXISTS tutoring_session (
    id           uuid         PRIMARY KEY,

    -- RESTRICT y no CASCADE: borrar una concesión con sesiones dentro dejaría horas
    -- consumidas sin nada que las explique. Para retirar horas se revoca la concesión.
    grant_id     uuid         NOT NULL REFERENCES tutoring_grant (id) ON DELETE RESTRICT,

    minutes      integer      NOT NULL CHECK (minutes > 0),
    occurred_at  timestamptz  NOT NULL,
    topic        varchar(200) NOT NULL,
    notes        text         NOT NULL DEFAULT '',
    recorded_by  uuid         NULL REFERENCES app_user (id) ON DELETE SET NULL,
    created_at   timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_tutoring_session_grant ON tutoring_session (grant_id, occurred_at DESC);
