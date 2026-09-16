-- V022 · La huella que declara el proveedor, separada de la nuestra.
--
-- ── por qué hay dos huellas y no una ─────────────────────────────────────────────────────
--
-- El envío a la AEAT lo hace un proveedor que firma en nuestro nombre como colaborador social,
-- y ese proveedor CALCULA ÉL el encadenamiento: se le mandan los datos de la factura y devuelve
-- la huella, el QR y el identificador del envío. Esa es la huella que vale de cara a Hacienda y
-- la que va impresa en la factura, porque es la que el cliente puede verificar.
--
-- La nuestra —`hash`, con su `hash_input`— no se tira. Responde a otra pregunta: si ALGUIEN HA
-- TOCADO NUESTRA BASE DE DATOS desde que se emitió. La del proveedor no puede responder a eso,
-- porque vive en su sistema y no se puede recalcular aquí.
--
--   `hash`          → sello de integridad interno. Se calcula aquí y se puede reverificar aquí.
--   `declared_hash` → la huella fiscal. La calcula y la declara el proveedor. Es la del QR.
--
-- Confundirlas sería lo peligroso: imprimir la interna en la factura mandaría al cliente a
-- verificar contra la AEAT una huella que la AEAT no tiene.

ALTER TABLE verifactu_record
    ADD COLUMN IF NOT EXISTS declared_hash char(64) NULL;

ALTER TABLE verifactu_record
    ADD COLUMN IF NOT EXISTS declared_qr varchar(500) NOT NULL DEFAULT '';

COMMENT ON COLUMN verifactu_record.hash IS
    'Sello de integridad interno: prueba que esta fila no se ha tocado desde que se emitió. NO es la huella fiscal.';

COMMENT ON COLUMN verifactu_record.declared_hash IS
    'La huella fiscal, tal como la devuelve el proveedor que declara. Es la que va en la factura y en el QR. NULL mientras no se ha declarado.';

COMMENT ON COLUMN verifactu_record.declared_qr IS
    'El contenido del QR que devuelve el proveedor, para imprimirlo tal cual. Vacío mientras no se ha declarado.';

-- ── el PDF se rehace cuando llega la declaración ──────────────────────────────────────────
--
-- La factura se imprime con la huella declarada y su QR, así que hasta que el proveedor
-- contesta el PDF es provisional. Cuando la declaración llega tarde —un reintento del job tras
-- una caída de red— hay que volver a imprimirla y sustituir el fichero.
--
-- Se marca aquí y no se deduce: «el PDF que hay guardado no lleva la huella definitiva» es un
-- estado real que alguien tiene que poder consultar y arreglar.

ALTER TABLE fiscal_invoice
    ADD COLUMN IF NOT EXISTS pdf_is_provisional boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN fiscal_invoice.pdf_is_provisional IS
    'El PDF guardado se imprimió antes de que llegara la huella declarada. Se rehace al declararse.';
