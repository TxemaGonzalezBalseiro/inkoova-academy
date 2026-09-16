-- V020 · El registro de facturación de Veri*Factu.
--
-- ── qué es esto y por qué no es la factura ───────────────────────────────────────────────
--
-- Veri*Factu no regula el PDF: regula el REGISTRO DE FACTURACIÓN, un asiento por cada factura
-- emitida (alta) y por cada una anulada, encadenado con el anterior y remitido a la AEAT. El
-- PDF es un documento para el cliente; el registro es lo que se declara.
--
-- Hasta ahora `fiscal_invoice` guardaba la factura y su huella, que sirve para el PDF pero no
-- para declarar: faltan el tipo de factura, el desglose, la marca temporal de generación y el
-- estado del envío. Sin estado de envío no se puede reintentar lo que no salió, y lo que no se
-- puede reintentar acaba sin declararse.

CREATE TABLE IF NOT EXISTS verifactu_record (
    id              uuid         PRIMARY KEY,

    -- La factura a la que corresponde. RESTRICT: el registro es la prueba de lo declarado y no
    -- puede desaparecer porque alguien borre una fila de facturas.
    invoice_id      uuid         NOT NULL REFERENCES fiscal_invoice (id) ON DELETE RESTRICT,

    -- 'alta' cuando se emite, 'anulacion' cuando se anula. Una anulación es un registro nuevo,
    -- no una modificación del anterior: el encadenamiento se rompería si un asiento pudiera
    -- cambiar después de encadenado.
    kind            varchar(16)  NOT NULL CHECK (kind IN ('alta', 'anulacion')),

    -- ── identificación del emisor, COPIADA ────────────────────────────────────────────────
    --
    -- No se lee de la marca al consultar: se copia al emitir. Si mañana cambia el domicilio
    -- fiscal o la razón social, las facturas de antes tienen que seguir diciendo lo que decían
    -- cuando se emitieron, que es lo que se declaró.
    issuer_tax_id   varchar(20)  NOT NULL,
    issuer_name     varchar(200) NOT NULL,

    -- ── la factura, tal como se declara ───────────────────────────────────────────────────
    --
    -- El tipo sale de la lista de la norma: F1 con destinatario identificado, F2 simplificada
    -- (la venta a consumidor sin NIF), R1..R5 rectificativas. Va como texto y no como enum de
    -- Postgres para que añadir un tipo no exija una migración de tipo.
    --
    -- Qué clave corresponde a cada venta se configura por marca desde V026, contra la
    -- enumeración ClaveTipoFacturaType del esquema oficial. Aquí solo se guarda la que se
    -- declaró, que es un dato histórico del asiento y no se toca nunca.
    invoice_type    varchar(4)   NOT NULL DEFAULT 'F2',
    series_number   varchar(40)  NOT NULL,
    issue_date      date         NOT NULL,
    description     varchar(500) NOT NULL,

    total_cents     bigint       NOT NULL,
    tax_cents       bigint       NOT NULL,
    currency        char(3)      NOT NULL DEFAULT 'EUR',

    -- Con qué factura se rectifica, cuando lo es. Texto y no clave ajena porque una
    -- rectificativa puede apuntar a una factura anterior al sistema.
    rectifies       varchar(40)  NULL,

    -- ── encadenamiento ────────────────────────────────────────────────────────────────────
    --
    -- Cada registro lleva la huella del anterior. El primero de la cadena encadena con ceros.
    previous_hash   char(64)     NOT NULL,
    hash            char(64)     NOT NULL,

    -- El algoritmo, guardado. Hoy solo hay uno, pero una cadena que no dice con qué se calculó
    -- no se puede volver a verificar el día que haya dos.
    hash_algorithm  varchar(16)  NOT NULL DEFAULT 'SHA-256',

    -- El texto exacto sobre el que se calculó la huella. Ocupa poco y es lo único que permite
    -- explicar una huella años después sin reconstruir el código de entonces.
    hash_input      text         NOT NULL,

    -- Marca temporal de generación del registro, CON huso. La norma la exige y no es lo mismo
    -- que la fecha de expedición: una factura del día 30 puede generar su registro el día 31.
    generated_at    timestamptz  NOT NULL,

    -- ── envío a la AEAT ───────────────────────────────────────────────────────────────────
    --
    -- 'pending' mientras no se ha remitido, 'sent' cuando la AEAT lo aceptó, 'rejected' cuando
    -- lo rechazó y 'not_required' cuando el envío está apagado (desarrollo, o facturación en
    -- modo no verificable). Sin estos cuatro, «no enviado» y «no hacía falta enviarlo» serían
    -- lo mismo, y el primero es un problema y el segundo no.
    submission_state varchar(16) NOT NULL DEFAULT 'pending'
        CHECK (submission_state IN ('pending', 'sent', 'rejected', 'not_required')),

    aeat_submission_id varchar(80) NULL,
    submitted_at       timestamptz NULL,

    -- Lo que contestó la AEAT cuando rechaza. Es lo que dice qué hay que corregir.
    submission_error   text        NOT NULL DEFAULT '',
    submission_attempts int        NOT NULL DEFAULT 0 CHECK (submission_attempts >= 0),

    created_at      timestamptz  NOT NULL DEFAULT now()
);

-- Un alta por factura. Una segunda sería declarar dos veces el mismo hecho imponible.
CREATE UNIQUE INDEX IF NOT EXISTS ux_verifactu_record_alta
    ON verifactu_record (invoice_id) WHERE kind = 'alta';

-- La cadena se lee en orden de generación. Es la consulta de la verificación y la del panel.
CREATE INDEX IF NOT EXISTS ix_verifactu_record_chain
    ON verifactu_record (issuer_tax_id, generated_at);

-- Lo que falta por remitir, que es lo que mira el job de reenvío.
CREATE INDEX IF NOT EXISTS ix_verifactu_record_pending
    ON verifactu_record (submission_state, generated_at)
    WHERE submission_state IN ('pending', 'rejected');

-- ── de qué marca sale cada factura ────────────────────────────────────────────────────────
--
-- Con varias identidades, el emisor de una factura no es «la academia»: es la marca desde la
-- que se vendió, con su razón social y su NIF. Anulable porque las facturas anteriores a las
-- identidades no tienen ninguna, y ponerles una sería decir que las emitió alguien que no las
-- emitió.

ALTER TABLE fiscal_invoice
    ADD COLUMN IF NOT EXISTS identity_id uuid NULL
        REFERENCES academy_identity (id) ON DELETE SET NULL;

COMMENT ON TABLE verifactu_record IS
    'Registro de facturación de Veri*Factu (RD 1007/2023). Un asiento por alta y por anulación, encadenado y con su estado de remisión a la AEAT.';
