-- V008 · Enlace de los paquetes de tutorías con Stripe.
--
-- Igual que en `plan`: el precio de Stripe es INMUTABLE, así que cambiar el importe de un
-- paquete no puede editar el precio de allí. Lo que se hace es soltar el enlace, y la
-- sincronización crea uno nuevo. Por eso esto es una columna que puede volver a NULL y no
-- una tabla de enlaces: no hay historial que guardar aquí, lo tiene Stripe.

ALTER TABLE tutoring_package
    ADD COLUMN IF NOT EXISTS stripe_price_id varchar(64) NULL;

-- Parcial: hay muchos paquetes sin enlazar todavía y NULL no debe chocar contra NULL.
CREATE UNIQUE INDEX IF NOT EXISTS ux_tutoring_package_stripe_price
    ON tutoring_package (stripe_price_id)
    WHERE stripe_price_id IS NOT NULL;
