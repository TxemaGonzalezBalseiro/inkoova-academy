-- V023 · Fuera la «huella declarada»: sobra con librería propia.
--
-- V022 añadió `declared_hash` y `declared_qr` pensando en un proveedor externo que declarase por
-- nosotros y devolviera él la huella. Se descartó esa vía: la declaración la hace la propia
-- plataforma con su certificado, así que la huella la calcula ella y ya vive en `hash`.
--
-- Lo que devuelve la AEAT al aceptar un registro es el CSV —código seguro de verificación—, y
-- eso tiene su columna desde el principio: `aeat_submission_id`.
--
-- El QR tampoco se guarda: se compone al imprimir a partir de los datos de la factura y del
-- entorno configurado, siguiendo las especificaciones del código QR de la AEAT. Guardarlo sería
-- guardar una copia que envejece el día que cambie la URL del servicio.
--
-- Se dejan caer sin datos que perder: nunca llegaron a escribirse.

ALTER TABLE verifactu_record DROP COLUMN IF EXISTS declared_hash;
ALTER TABLE verifactu_record DROP COLUMN IF EXISTS declared_qr;

ALTER TABLE fiscal_invoice DROP COLUMN IF EXISTS pdf_is_provisional;

COMMENT ON COLUMN verifactu_record.hash IS
    'La huella del registro, calculada según las especificaciones de la AEAT (SHA-256, hex en mayúsculas). Es el dato fiscal y a la vez el sello que permite detectar aquí una fila alterada.';

COMMENT ON COLUMN verifactu_record.aeat_submission_id IS
    'El CSV que devuelve la AEAT al aceptar el registro.';
