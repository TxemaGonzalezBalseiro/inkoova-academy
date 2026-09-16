-- V017 · Datos identificativos y enlaces de contacto de cada marca.
--
-- El aviso legal, la política de privacidad y los términos llevan hoy `TODO(T-14)` donde debe
-- ir la razón social, el NIF, el domicilio y los datos registrales. Están escritos en el
-- código de la SPA, así que ni se pueden rellenar sin desplegar ni pueden ser distintos por
-- marca — y con varias marcas eso es un problema real: el titular de cada sitio puede ser una
-- sociedad distinta o una persona física.
--
-- ── por qué van vacíos y no con un valor de ejemplo ───────────────────────────────────────
--
-- El artículo 10 de la LSSI obliga a publicar estos datos, y son exactamente el tipo de dato
-- que no se puede aproximar: un NIF inventado en un aviso legal es peor que un hueco, porque
-- el hueco se ve y el dato falso se cree. Entran vacíos y la página seguirá avisando de cuáles
-- faltan hasta que alguien los escriba.

ALTER TABLE academy_identity
    -- Razón social o nombre y apellidos si el titular es una persona física.
    ADD COLUMN IF NOT EXISTS legal_name       varchar(200) NOT NULL DEFAULT '',

    -- NIF, CIF o el identificador fiscal que corresponda.
    ADD COLUMN IF NOT EXISTS tax_id           varchar(40)  NOT NULL DEFAULT '',

    -- Domicilio completo, en una línea o varias.
    ADD COLUMN IF NOT EXISTS legal_address    text         NOT NULL DEFAULT '',

    -- Datos de inscripción registral. Solo aplican a sociedades: una persona física no tiene,
    -- y por eso puede quedarse vacío sin que eso sea un dato pendiente.
    ADD COLUMN IF NOT EXISTS registry_details text         NOT NULL DEFAULT '',

    -- Correo del titular a efectos legales. Puede no ser el de soporte: el de atención al
    -- cliente y el que recibe una reclamación formal no tienen por qué ser el mismo buzón.
    ADD COLUMN IF NOT EXISTS legal_email      varchar(256) NOT NULL DEFAULT '',

    -- Enlaces del pie. Vacío = ese enlace no se pinta, en vez de llevar a una página de otro.
    ADD COLUMN IF NOT EXISTS linkedin_url     text         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS company_url      text         NOT NULL DEFAULT '';

COMMENT ON COLUMN academy_identity.legal_name IS
    'Razón social o nombre del titular. Exigido por el art. 10 LSSI; vacío significa pendiente, nunca se rellena con un ejemplo.';
