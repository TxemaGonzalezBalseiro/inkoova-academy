-- V013 · El certificado se personaliza por estilo, no por plantilla HTML.
--
-- ── por qué cambia lo de V012 ─────────────────────────────────────────────────────────────
--
-- V012 creó `certificate_template` con una columna `html`, pensando en convertir HTML a PDF.
-- Se ha decidido otra cosa, y es mejor: de la maqueta solo se tocan la CABECERA, el EMISOR y
-- los COLORES. El resto —sello, QR, código, hash y el pie verificable— lo sigue dibujando el
-- generador y nadie lo puede romper.
--
-- Lo que se gana al no meter HTML:
--
--   1. **El PDF sigue siendo reproducible byte a byte.** Un motor HTML→PDF mete la hora del
--      sistema y fuentes de la máquina en el resultado.
--   2. **Ni Chromium ni un motor de pago en la imagen de la API.**
--   3. **Nadie puede dejar un certificado sin QR.** Si el pie fuese editable, bastaría con
--      borrar un trozo de plantilla para que ese certificado dejara de poder comprobarse, y el
--      fallo no se vería hasta que alguien intentara verificarlo.
--
-- La tabla estaba vacía —se creó hoy y nada la escribía—, así que se transforma sin perder nada.

ALTER TABLE certificate_template DROP COLUMN IF EXISTS html;

ALTER TABLE certificate_template
    -- La banda superior. Vacío = se usa el nombre de la marca, que es lo normal.
    ADD COLUMN IF NOT EXISTS heading        varchar(80)  NOT NULL DEFAULT '',
    -- La línea bajo el filete: qué clase de documento es.
    ADD COLUMN IF NOT EXISTS subheading     varchar(80)  NOT NULL DEFAULT '',
    -- Quién lo emite, al pie junto al sello. Vacío = el nombre de la marca.
    ADD COLUMN IF NOT EXISTS issuer_name    varchar(120) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS issuer_note    varchar(120) NOT NULL DEFAULT '',
    -- Tres colores, en hexadecimal con almohadilla. Vacío = el de la marca de la academia.
    ADD COLUMN IF NOT EXISTS primary_color  varchar(9)   NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS accent_color   varchar(9)   NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS support_color  varchar(9)   NOT NULL DEFAULT '';

-- El nombre de la tabla ya no describe lo que guarda: son ajustes de estilo, no una plantilla.
ALTER TABLE certificate_template RENAME TO certificate_style;

COMMENT ON TABLE certificate_style IS
    'Cabecera, emisor y colores del certificado de cada marca. El resto del documento —sello, QR, código, hash y pie— lo dibuja el generador y no es configurable, para que ningún ajuste pueda dejar un certificado sin poder verificarse.';
