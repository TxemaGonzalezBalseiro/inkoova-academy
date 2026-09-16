-- V024 · Control de flujo del envío a la AEAT.
--
-- ── por qué no se puede remitir factura a factura ────────────────────────────────────────
--
-- El documento de descripción de servicios web de la AEAT (v1.0.3) impone dos límites que no
-- son recomendaciones:
--
--   · Máximo 1.000 registros por envío.
--   · Un tiempo de espera ENTRE envíos, que empieza en 60 segundos y que la AEAT devuelve
--     actualizado en cada respuesta (`TiempoEsperaEnvio`). Hay que esperar ese tiempo desde el
--     envío anterior antes de mandar el siguiente.
--
-- Remitir cada factura en cuanto se cobra —que es lo que hacía -- incumple el segundo en cuanto
-- entran dos ventas en el mismo minuto, y una plataforma de cursos vende sola a cualquier hora.
--
-- Así que los registros se acumulan como pendientes y los remite un proceso por lotes que
-- respeta la espera. Esta tabla es lo que recuerda hasta cuándo hay que esperar; sin ella, un
-- reinicio del proceso volvería a empezar y podría enviar antes de tiempo.

CREATE TABLE IF NOT EXISTS verifactu_flow (
    -- Por emisor: cada NIF tiene su propia cadena y su propio ritmo de envío.
    issuer_tax_id  varchar(20)  PRIMARY KEY,

    -- Lo último que dijo la AEAT que hay que esperar. Se guarda además del instante calculado
    -- para poder ver si la agencia ha subido el tiempo, que es su forma de pedir que aflojemos.
    wait_seconds   int          NOT NULL DEFAULT 60 CHECK (wait_seconds >= 0),

    -- Hasta cuándo no se puede volver a enviar. Es lo que se consulta antes de cada lote.
    next_allowed_at timestamptz NOT NULL DEFAULT now(),

    last_sent_at   timestamptz  NULL,
    updated_at     timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON TABLE verifactu_flow IS
    'Control de flujo del envío a la AEAT: cuánto hay que esperar entre lotes y hasta cuándo. Lo fija la propia AEAT en cada respuesta.';
