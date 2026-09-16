-- V021 · La cadena no se puede bifurcar.
--
-- Dos cobros procesados a la vez leen el mismo «último registro» y encadenan los dos desde su
-- misma huella. El resultado es una cadena en Y: los dos asientos dicen venir del mismo sitio y
-- ninguno viene después del otro. Es exactamente lo que el encadenamiento existe para impedir,
-- y no se ve hasta que alguien va a verificarla.
--
-- La numeración ya está protegida —`ux_fiscal_invoice_series_number`—, pero el número y la
-- posición en la cadena son cosas distintas: se puede tener numeración correcta y cadena rota.
--
-- Dos asientos del mismo emisor no pueden encadenar desde la misma huella. El segundo falla al
-- insertar, que es lo que se quiere: quien lo intente reintenta y lee el último de verdad.

CREATE UNIQUE INDEX IF NOT EXISTS ux_verifactu_record_link
    ON verifactu_record (issuer_tax_id, previous_hash);
