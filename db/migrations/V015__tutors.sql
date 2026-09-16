-- V015 · Profesores de tutorías y lo que se les paga.
--
-- ── por qué no se reutiliza la comisión de afiliados ──────────────────────────────────────
--
-- `commission` cuelga de una COMPRA y espera catorce días por el derecho de desistimiento: si
-- el cliente devuelve, la comisión se revierte. Una tutoría no funciona así. La clase se dio;
-- el profesor trabajó. No hay nada que revertir cuando pasan dos semanas, y hacerle esperar
-- por una ventana que no le aplica sería cobrarle un plazo que no es suyo.
--
-- Misma FORMA que una comisión —base, porcentaje, importe— y ciclo propio.

CREATE TABLE IF NOT EXISTS tutor (
    id                 uuid         PRIMARY KEY,

    -- Si además tiene cuenta en la plataforma. NULL para un profesor externo que no entra a la
    -- academia: se le pagan tutorías y no necesita usuario. SET NULL para que borrar la cuenta
    -- no borre al profesor ni, con él, lo que se le debe.
    user_id            uuid         NULL REFERENCES app_user (id) ON DELETE SET NULL,

    display_name       varchar(120) NOT NULL,
    email              varchar(256) NOT NULL,
    bio                text         NOT NULL DEFAULT '',

    -- Qué porcentaje se lleva de lo que el alumno pagó por esas horas. Dos decimales: hay
    -- acuerdos al 33,33 %.
    commission_percent numeric(5,2) NOT NULL DEFAULT 0
        CHECK (commission_percent >= 0 AND commission_percent <= 100),

    is_active          boolean      NOT NULL DEFAULT true,
    created_at         timestamptz  NOT NULL DEFAULT now(),
    updated_at         timestamptz  NOT NULL DEFAULT now()
);

-- Un profesor por correo: es como se le identifica al convocarlo y al liquidarle.
CREATE UNIQUE INDEX IF NOT EXISTS ux_tutor_email ON tutor (lower(email));

-- Quién dio cada tutoría. NULL en las ya apuntadas antes de que existieran los profesores: no
-- se puede adivinar quién las dio, y ponerle una a alguien sería inventar a quién pagar.
ALTER TABLE tutoring_session
    ADD COLUMN IF NOT EXISTS tutor_id uuid NULL REFERENCES tutor (id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_tutoring_session_tutor ON tutoring_session (tutor_id, occurred_at DESC);

-- ── lo que se devenga ─────────────────────────────────────────────────────────────────────
--
-- Una fila por tutoría dada, con lo que se le debe al profesor por ella.
--
-- La BASE no es el precio del paquete: es la parte proporcional de lo que el alumno pagó de
-- verdad por esas horas concretas. Una bolsa de 5 h por 499 € que se consume en una sesión de
-- 1 h devenga sobre 99,80 €, no sobre 499 €.
--
-- Y si la bolsa fue MANUAL —una beca, un acuerdo, una cortesía— la base es cero: no entró
-- dinero, y pagar un porcentaje de un ingreso que no existe es pagar de la propia caja sin
-- saberlo. El profesor cobra igual si se le quiere pagar, pero entonces es una decisión
-- explícita y no un efecto secundario del sistema.

CREATE TABLE IF NOT EXISTS tutor_earning (
    id           uuid         PRIMARY KEY,
    tutor_id     uuid         NOT NULL REFERENCES tutor (id) ON DELETE RESTRICT,

    -- Una tutoría devenga una sola vez. Sin esto, reapuntar la misma sesión pagaría dos veces.
    session_id   uuid         NOT NULL UNIQUE REFERENCES tutoring_session (id) ON DELETE CASCADE,

    base_cents   bigint       NOT NULL CHECK (base_cents >= 0),
    percent      numeric(5,2) NOT NULL CHECK (percent >= 0 AND percent <= 100),
    amount_cents bigint       NOT NULL CHECK (amount_cents >= 0),
    currency     char(3)      NOT NULL DEFAULT 'EUR',

    -- 'pending' mientras no se ha pagado, 'paid' cuando se liquidó. No hay 'cancelled': si la
    -- tutoría se deshace, la fila se va con ella por la clave ajena.
    status       varchar(16)  NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'paid')),

    created_at   timestamptz  NOT NULL DEFAULT now(),
    paid_at      timestamptz  NULL,
    payout_note  varchar(300) NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS ix_tutor_earning_pending
    ON tutor_earning (tutor_id) WHERE status = 'pending';

-- ── el precio de la bolsa, para poder prorratear ──────────────────────────────────────────
--
-- Hasta ahora una concesión guardaba sus minutos y su nombre, pero no lo que costó. Sin ese
-- dato no se puede repartir: habría que ir al paquete, y el paquete pudo cambiar de precio
-- después. Se copia igual que se copiaron los minutos, y por lo mismo.

ALTER TABLE tutoring_grant
    ADD COLUMN IF NOT EXISTS paid_cents bigint NOT NULL DEFAULT 0 CHECK (paid_cents >= 0);

COMMENT ON COLUMN tutoring_grant.paid_cents IS
    'Lo que el alumno pagó por esta bolsa, copiado al concederla. Cero en las manuales. Es la base del reparto con el profesor.';

-- Las bolsas que ya existían y salieron de un paquete: se les pone lo que costaba ese paquete.
-- Es lo que se pagó, salvo que el precio haya cambiado desde entonces; para las cuatro que hay
-- hoy en desarrollo es exacto, y en producción no hay ninguna todavía.
UPDATE tutoring_grant g
SET paid_cents = p.price_cents
FROM tutoring_package p
WHERE g.package_id = p.id
  AND g.source = 'purchase'
  AND g.paid_cents = 0;
