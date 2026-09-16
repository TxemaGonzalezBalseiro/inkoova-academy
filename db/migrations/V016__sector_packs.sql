-- V016 · Los seis packs sectoriales en el catálogo.
--
-- El mecanismo de acceso YA existía y funciona: `plan.includes_packs` está a cierto en el plan
-- anual (Elite) y en el vitalicio, y `PlanEntitlementService` concede un entitlement por cada
-- producto de tipo `pack` al contratar o renovar. Lo que faltaba eran los packs: la consulta
-- recorría una tabla vacía, así que quien pagaba Elite tenía derecho a seis packs que no
-- existían.
--
-- ── por qué entran como «próximamente» y no publicados ────────────────────────────────────
--
-- Sus ficheros todavía no están escritos: los README de `content/packs/` los listan con
-- `TODO(contenido)`. Publicarlos ahora enseñaría a un cliente de pago seis packs que se abren
-- vacíos, que es peor que decirle que están en preparación. En `comingsoon` se ven en el
-- catálogo, con su sector y su contenido previsto, y no prometen una descarga que no hay.
--
-- Pasarlos a `published` es una línea desde el panel el día que los ficheros estén, y el
-- entitlement ya estará concedido desde el primer día: nadie tendrá que renovar para verlos.
--
-- ── el precio suelto ──────────────────────────────────────────────────────────────────────
--
-- `one_off_price_cents` queda a NULL: hoy los packs no se venden por separado, se incluyen en
-- Elite y en el vitalicio. Inventar aquí un precio sería fijar una tarifa que nadie ha
-- decidido (convención 6). El día que se vendan sueltos se pone desde el panel.

INSERT INTO product (id, type, slug, title, one_off_price_cents, currency)
VALUES
    ('0199a1e0-0000-7000-8000-000000000015', 'pack', 'pack-seguros-financiero',
     'Pack sectorial · Seguros y financiero', NULL, 'EUR'),
    ('0199a1e0-0000-7000-8000-000000000016', 'pack', 'pack-salud',
     'Pack sectorial · Salud', NULL, 'EUR'),
    ('0199a1e0-0000-7000-8000-000000000017', 'pack', 'pack-administracion-publica',
     'Pack sectorial · Administración pública', NULL, 'EUR'),
    ('0199a1e0-0000-7000-8000-000000000018', 'pack', 'pack-rrhh',
     'Pack sectorial · Recursos humanos', NULL, 'EUR'),
    ('0199a1e0-0000-7000-8000-000000000019', 'pack', 'pack-industria-energia',
     'Pack sectorial · Industria y energía', NULL, 'EUR'),
    ('0199a1e0-0000-7000-8000-000000000020', 'pack', 'pack-retail-ecommerce',
     'Pack sectorial · Retail y comercio electrónico', NULL, 'EUR')
ON CONFLICT (slug) DO NOTHING;

-- El texto de «qué incluye» sale de los README de `content/packs/`, que es donde está definido
-- cada pack. No se inventa nada aquí: si un pack cambia de alcance, se cambia allí y se
-- actualiza esta ficha desde el panel.
INSERT INTO pack (id, product_id, slug, title, sector, version, changelog, status, regulatory_check_date)
VALUES
    ('0199a1e1-0000-7000-8000-000000000015', '0199a1e0-0000-7000-8000-000000000015',
     'pack-seguros-financiero', 'Seguros y financiero', 'Seguros y financiero', '1.0.0',
     'DORA, criterios de EIOPA y EBA sobre IA, scoring y pricing como alto riesgo, y externalización a proveedores de modelos.',
     'comingsoon', NULL),

    ('0199a1e1-0000-7000-8000-000000000016', '0199a1e0-0000-7000-8000-000000000016',
     'pack-salud', 'Salud', 'Salud', '1.0.0',
     'MDR, IA como producto sanitario, datos de salud y evaluación clínica.',
     'comingsoon', NULL),

    ('0199a1e1-0000-7000-8000-000000000017', '0199a1e0-0000-7000-8000-000000000017',
     'pack-administracion-publica', 'Administración pública', 'Administración pública', '1.0.0',
     'Contratación pública de IA, transparencia algorítmica y garantías del procedimiento.',
     'comingsoon', NULL),

    ('0199a1e1-0000-7000-8000-000000000018', '0199a1e0-0000-7000-8000-000000000018',
     'pack-rrhh', 'Recursos humanos', 'Recursos humanos', '1.0.0',
     'Selección y evaluación de personas como alto riesgo, sesgo y derechos de información.',
     'comingsoon', NULL),

    ('0199a1e1-0000-7000-8000-000000000019', '0199a1e0-0000-7000-8000-000000000019',
     'pack-industria-energia', 'Industria y energía', 'Industria y energía', '1.0.0',
     'IA en infraestructuras críticas, seguridad de máquinas y continuidad operativa.',
     'comingsoon', NULL),

    ('0199a1e1-0000-7000-8000-000000000020', '0199a1e0-0000-7000-8000-000000000020',
     'pack-retail-ecommerce', 'Retail y comercio electrónico', 'Retail y comercio electrónico', '1.0.0',
     'Recomendadores, precios personalizados, transparencia y prácticas prohibidas.',
     'comingsoon', NULL)
ON CONFLICT (slug) DO NOTHING;

-- La fecha de verificación normativa queda a NULL a propósito. Es la fecha en la que alguien
-- abrió DORA, las publicaciones de EIOPA, el MDR o lo que corresponda a cada pack. Ponerle una
-- fecha hoy sería afirmar que esa comprobación se ha hecho.
