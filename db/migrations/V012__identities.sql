-- V012 · Varias identidades: marca, buzón de correo y plantillas propias de cada una.
--
-- ── qué separa una identidad y qué no ─────────────────────────────────────────────────────
--
-- SEPARA: el nombre y el logo, el buzón desde el que salen los correos, las plantillas de esos
-- correos y la plantilla del certificado.
--
-- NO SEPARA: los alumnos, el catálogo ni la caja. Una persona tiene UNA cuenta en la
-- plataforma y entra una sola vez, aunque curse cosas de dos marcas distintas. Por eso ni
-- `app_user` ni `entitlement` ni `purchase` llevan identidad: meterla ahí convertiría esto en
-- tres plataformas separadas, que es justo lo que no se ha pedido.
--
-- Lo que sí lleva identidad es el CURSO: es lo que permite decir «el certificado de este curso
-- sale con esta marca, y sus correos desde este buzón».

CREATE TABLE IF NOT EXISTS academy_identity (
    id             uuid         PRIMARY KEY,
    slug           varchar(60)  NOT NULL UNIQUE,

    -- ── marca ────────────────────────────────────────────────────────────────────────────
    name           varchar(120) NOT NULL,
    tagline        varchar(200) NOT NULL DEFAULT '',
    logo_url       text         NOT NULL DEFAULT '',
    public_domain  varchar(200) NOT NULL DEFAULT '',
    support_email  varchar(256) NOT NULL DEFAULT '',

    -- ── buzón de salida ──────────────────────────────────────────────────────────────────
    -- Cada identidad manda desde su propio correo. Sin esto, un alumno de la marca B recibiría
    -- su confirmación desde la dirección de la marca A, que además es la forma más rápida de
    -- acabar en spam por no coincidir el dominio del remitente con el SPF.
    smtp_host      varchar(200) NOT NULL DEFAULT '',
    smtp_port      integer      NOT NULL DEFAULT 587,
    smtp_username  varchar(200) NOT NULL DEFAULT '',

    -- CIFRADA, nunca en claro. Es la única credencial que esta plataforma guarda en su propia
    -- base: las de Stripe viven en el entorno. Se cifra con una clave que está en el entorno,
    -- así que una copia de la base robada sin esa clave no entrega ningún buzón.
    smtp_password  text         NOT NULL DEFAULT '',

    smtp_security  varchar(16)  NOT NULL DEFAULT 'auto',
    from_address   varchar(256) NOT NULL DEFAULT '',

    -- Vacío = se usa `name`. Es lo que casi siempre se quiere.
    from_name      varchar(120) NOT NULL DEFAULT '',

    -- La identidad de la que se tira cuando algo no dice de cuál es: un curso sin marca
    -- asignada, un correo de sistema, la cabecera de la web. Exactamente una.
    is_default     boolean      NOT NULL DEFAULT false,

    is_active      boolean      NOT NULL DEFAULT true,
    created_at     timestamptz  NOT NULL DEFAULT now(),
    updated_at     timestamptz  NOT NULL DEFAULT now()
);

-- Una y solo una por defecto. Parcial para que las no-predeterminadas no choquen entre ellas.
CREATE UNIQUE INDEX IF NOT EXISTS ux_academy_identity_default
    ON academy_identity (is_default) WHERE is_default;

-- El dominio, cuando está puesto, no se puede repetir: es lo que se enseña como la dirección
-- de esa marca y dos identidades no pueden decir que son el mismo sitio.
CREATE UNIQUE INDEX IF NOT EXISTS ux_academy_identity_domain
    ON academy_identity (lower(public_domain)) WHERE public_domain <> '';

-- ── la identidad que ya existía ───────────────────────────────────────────────────────────
--
-- No se empieza de cero: lo que hay en `academy_setting` es la marca real de la academia y
-- pasa a ser la identidad predeterminada. Crear una vacía y dejar la vieja en su tabla
-- convertiría el estreno de esta pantalla en «tu academia ha perdido su nombre».

INSERT INTO academy_identity (id, slug, name, tagline, logo_url, public_domain, support_email, is_default)
SELECT
    '0199a1d0-0000-7000-8000-000000000001',
    'principal',
    COALESCE(NULLIF((SELECT value FROM academy_setting WHERE key = 'academyName'), ''), 'Inkoova Academy'),
    COALESCE((SELECT value FROM academy_setting WHERE key = 'academyTagline'), ''),
    COALESCE((SELECT value FROM academy_setting WHERE key = 'logoUrl'), ''),
    COALESCE((SELECT value FROM academy_setting WHERE key = 'publicDomain'), ''),
    COALESCE((SELECT value FROM academy_setting WHERE key = 'supportEmail'), ''),
    true
ON CONFLICT (id) DO NOTHING;

-- ── plantillas de correo ──────────────────────────────────────────────────────────────────
--
-- Una fila por identidad y plantilla. Lo que NO esté aquí cae en el fichero de `emails/`, que
-- sigue siendo el original: así una identidad nueva funciona desde el primer minuto sin que
-- nadie tenga que escribir ocho correos antes de poder mandar el primero.

CREATE TABLE IF NOT EXISTS email_template (
    identity_id uuid         NOT NULL REFERENCES academy_identity (id) ON DELETE CASCADE,
    name        varchar(60)  NOT NULL,
    subject     varchar(300) NOT NULL,
    html        text         NOT NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid         NULL REFERENCES app_user (id) ON DELETE SET NULL,

    PRIMARY KEY (identity_id, name)
);

-- ── plantilla del certificado ─────────────────────────────────────────────────────────────
--
-- HTML por identidad, convertido a PDF. Una fila por identidad como mucho.
--
-- Aviso que conviene no perder: el PDF generado desde HTML NO es byte a byte reproducible como
-- el que dibujaba el generador vectorial. Eso no rompe la verificación —el hash del
-- certificado se calcula sobre alumno, asunto y fecha, nunca sobre los bytes del PDF— pero sí
-- significa que dos descargas del mismo certificado pueden diferir en bytes.

CREATE TABLE IF NOT EXISTS certificate_template (
    identity_id uuid        PRIMARY KEY REFERENCES academy_identity (id) ON DELETE CASCADE,
    html        text        NOT NULL,
    updated_at  timestamptz NOT NULL DEFAULT now(),
    updated_by  uuid        NULL REFERENCES app_user (id) ON DELETE SET NULL
);

-- ── de qué marca es cada curso ────────────────────────────────────────────────────────────
--
-- NULL = la predeterminada. Se deja anulable a propósito: obligar a elegir marca en cada curso
-- para una academia que solo tiene una sería papeleo sin sentido.

ALTER TABLE course
    ADD COLUMN IF NOT EXISTS identity_id uuid NULL
        REFERENCES academy_identity (id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_course_identity ON course (identity_id);
