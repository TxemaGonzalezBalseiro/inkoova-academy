-- V018 · La moneda de lo que se pagó por una bolsa de tutorías.
--
-- V015 añadió `paid_cents` sin moneda. Un importe sin moneda no es un importe: es un número que
-- hay que adivinar, y el que lo adivine acabará repartiendo euros de una bolsa vendida en otra
-- divisa. El paquete la guarda desde el principio; la bolsa la copia igual que copia el precio.

ALTER TABLE tutoring_grant
    ADD COLUMN IF NOT EXISTS paid_currency char(3) NOT NULL DEFAULT 'EUR';

-- Las que ya existen y salieron de un paquete heredan la suya. Las manuales se quedan en EUR
-- con importe cero, donde la moneda no decide nada.
UPDATE tutoring_grant g
SET paid_currency = p.currency
FROM tutoring_package p
WHERE g.package_id = p.id
  AND g.paid_cents > 0;
