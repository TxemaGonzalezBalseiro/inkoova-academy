-- V019 · Tutorías CONVOCADAS, que es algo distinto de tutorías dadas.
--
-- Hasta ahora solo existía `tutoring_session`: lo que ya ocurrió, apuntado a posteriori para
-- descontar tiempo del saldo. Eso no sirve para convocar a nadie —cuando existe la fila, la
-- clase ya pasó— y sin convocatoria no hay invitación de calendario que mandar.
--
-- Una cita es una promesa: alumno, profesor, día y hora. No descuenta saldo al crearse, porque
-- todavía no se ha dado nada; descuenta cuando se marca como dada, y entonces —y solo entonces—
-- nace su `tutoring_session` y el devengo del profesor.
--
-- Reservar el tiempo al convocar sería lo contrario: una cancelación olvidada dejaría horas
-- muertas que el alumno pagó y no puede gastar.

CREATE TABLE IF NOT EXISTS tutoring_appointment (
    id          uuid         PRIMARY KEY,

    -- De qué bolsa saldrá el tiempo. En cascada: si la bolsa desaparece, la convocatoria contra
    -- ella no significa nada.
    grant_id    uuid         NOT NULL REFERENCES tutoring_grant (id) ON DELETE CASCADE,

    -- Quién la dará. Puede quedarse sin profesor si se le borra la ficha, y entonces la cita
    -- sigue en pie con un hueco visible en vez de desaparecer del calendario de nadie.
    tutor_id    uuid         NULL REFERENCES tutor (id) ON DELETE SET NULL,

    starts_at   timestamptz  NOT NULL,
    minutes     int          NOT NULL CHECK (minutes > 0 AND minutes <= 480),

    topic       varchar(200) NOT NULL,
    notes       text         NOT NULL DEFAULT '',

    -- Dónde. Un enlace de reunión o una sala; va tal cual al calendario.
    location    varchar(500) NOT NULL DEFAULT '',

    status      varchar(16)  NOT NULL DEFAULT 'scheduled'
        CHECK (status IN ('scheduled', 'done', 'cancelled')),

    -- La tutoría que nació de esta cita al marcarla como dada. NULL mientras no se ha dado.
    -- ON DELETE SET NULL: deshacer la tutoría devuelve la cita a «convocada», que es la verdad
    -- —se quedó sin dar—, en vez de llevarse por delante la convocatoria.
    session_id  uuid         NULL UNIQUE REFERENCES tutoring_session (id) ON DELETE SET NULL,

    -- ── lo que necesita el calendario ─────────────────────────────────────────────────────
    --
    -- El UID identifica el evento en el calendario de cada invitado para siempre. Tiene que
    -- sobrevivir a los cambios de hora: si cambiara, mover la tutoría crearía un evento nuevo y
    -- dejaría el viejo colgado en las agendas de todos.
    ics_uid     varchar(200) NOT NULL,

    -- Los clientes de calendario ignoran una actualización cuyo número de secuencia no sea mayor
    -- que el que ya tienen. Sin contador, la segunda vez que se mueve la hora no se entera nadie.
    ics_sequence int         NOT NULL DEFAULT 0 CHECK (ics_sequence >= 0),

    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid         NULL REFERENCES app_user (id) ON DELETE SET NULL,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    cancelled_reason varchar(300) NOT NULL DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tutoring_appointment_uid ON tutoring_appointment (ics_uid);

-- La agenda: lo convocado y pendiente, por fecha. Es la consulta del panel y la del alumno.
CREATE INDEX IF NOT EXISTS ix_tutoring_appointment_upcoming
    ON tutoring_appointment (starts_at) WHERE status = 'scheduled';

CREATE INDEX IF NOT EXISTS ix_tutoring_appointment_grant
    ON tutoring_appointment (grant_id, starts_at DESC);

CREATE INDEX IF NOT EXISTS ix_tutoring_appointment_tutor
    ON tutoring_appointment (tutor_id, starts_at DESC);
