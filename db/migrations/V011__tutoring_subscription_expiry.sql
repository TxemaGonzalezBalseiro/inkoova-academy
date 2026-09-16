-- V011 · Las tutorías caducan con la suscripción, y el catálogo de paquetes por horas.
--
-- ── por qué un modo y no una fecha ────────────────────────────────────────────────────────
--
-- Hasta ahora una bolsa caducaba en una fecha fija, calculada al concederla. Eso no sirve para
-- lo que se vende: las horas viven mientras el alumno siga suscrito. Si renueva, siguen ahí;
-- si deja de renovar, se pierden.
--
-- Se podría haber resuelto con un job que empujara `expires_at` en cada renovación. Sería
-- peor: entre la renovación y la pasada del job la bolsa aparecería caducada, y si el job
-- fallara un día, las horas de todo el mundo caducarían sin que nadie hubiera hecho nada. Un
-- MODO se evalúa en el momento de mirar y no puede desincronizarse.
--
-- `expires_at` se conserva para las bolsas de fecha fija, que siguen existiendo: un acuerdo
-- suelto con una empresa no tiene por qué depender de ninguna suscripción.

ALTER TABLE tutoring_package
    ADD COLUMN IF NOT EXISTS expiry_mode varchar(16) NOT NULL DEFAULT 'fixed'
        CHECK (expiry_mode IN ('fixed', 'subscription', 'never'));

ALTER TABLE tutoring_grant
    ADD COLUMN IF NOT EXISTS expiry_mode varchar(16) NOT NULL DEFAULT 'fixed'
        CHECK (expiry_mode IN ('fixed', 'subscription', 'never'));

COMMENT ON COLUMN tutoring_grant.expiry_mode IS
    'fixed: caduca en expires_at. subscription: mientras la suscripción dé acceso. never: no caduca.';

-- ── el catálogo ───────────────────────────────────────────────────────────────────────────
--
-- Importes en céntimos y con impuestos incluidos, la misma convención que `plan`.
-- Los cuatro van atados a la suscripción, así que `validity_days` queda a NULL: en este modo
-- la fecha no la fija el paquete, la fija la suscripción de cada alumno.
--
-- ON CONFLICT DO NOTHING sobre el slug: reaplicar el script no pisa un precio ya cambiado
-- desde el panel.

INSERT INTO tutoring_package
    (id, slug, name, description, minutes, price_cents, currency,
     validity_days, display_order, is_active, expiry_mode)
VALUES
    ('0199a1c0-0000-7000-8000-000000000001', 'tutoria-1h',  '1 tutoría',
     'Una hora uno a uno sobre lo que estés construyendo.',
     60,  9900,  'EUR', NULL, 1, true, 'subscription'),

    ('0199a1c0-0000-7000-8000-000000000002', 'tutorias-2h', 'Pack de 2 horas',
     'Dos horas de tutoría, para un problema que no se cierra en una sesión.',
     120, 19900, 'EUR', NULL, 2, true, 'subscription'),

    ('0199a1c0-0000-7000-8000-000000000003', 'tutorias-5h', 'Pack de 5 horas',
     'Cinco horas para acompañar un proyecto durante varias semanas.',
     300, 49900, 'EUR', NULL, 3, true, 'subscription'),

    ('0199a1c0-0000-7000-8000-000000000004', 'tutorias-10h', 'Pack de 10 horas',
     'Diez horas: acompañamiento continuado de un sistema en producción.',
     600, 89900, 'EUR', NULL, 4, true, 'subscription')
ON CONFLICT (slug) DO NOTHING;
