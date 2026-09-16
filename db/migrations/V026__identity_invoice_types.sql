-- Clave de tipo de factura por marca (Veri*factu, RD 1007/2023).
--
-- Estaba escrita en el código, en VerifactuService, como una constante sin verificar: "F2 si
-- no hay rectificación, R1 si la hay". Qué clave corresponde a cada venta es una decisión fiscal
-- que toma quien lleva la contabilidad y que cambia con el negocio —el día que se empiece a
-- pedir el NIF al cliente, esa venta pasa de F2 a F1—, así que no puede vivir en un despliegue.
--
-- Los valores admitidos los fija ClaveTipoFacturaType del esquema oficial de la AEAT
-- (docs/verifactu/esquemas/SuministroInformacion.xsd). La restricción de abajo es esa
-- enumeración, no una elegida por nosotros: si la AEAT amplía la lista, se amplía aquí.
--
-- Por defecto F2 + R5, que es el par coherente para vender a consumidor final sin pedirle el
-- NIF: la rectificativa de una factura simplificada tiene su propia clave.

ALTER TABLE academy_identity
    ADD COLUMN IF NOT EXISTS invoice_type text NOT NULL DEFAULT 'F2',
    ADD COLUMN IF NOT EXISTS corrective_invoice_type text NOT NULL DEFAULT 'R5';

ALTER TABLE academy_identity
    DROP CONSTRAINT IF EXISTS ck_identity_invoice_type;

ALTER TABLE academy_identity
    ADD CONSTRAINT ck_identity_invoice_type
        CHECK (invoice_type IN ('F1', 'F2', 'F3'));

ALTER TABLE academy_identity
    DROP CONSTRAINT IF EXISTS ck_identity_corrective_invoice_type;

ALTER TABLE academy_identity
    ADD CONSTRAINT ck_identity_corrective_invoice_type
        CHECK (corrective_invoice_type IN ('R1', 'R2', 'R3', 'R4', 'R5'));
