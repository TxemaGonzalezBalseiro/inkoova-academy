-- V006 · Identidad de la academia: nombre, lema, logo, dominio público y correo de contacto.
--
-- Estos ajustes NO son secretos, a diferencia de las claves de Stripe o del buzón de correo:
-- son datos de presentación que el propio negocio cambia sin desplegar. Por eso viven en la
-- base y no en el entorno. Nada de lo que se guarda aquí debe ser una credencial.
--
-- Tabla clave-valor a propósito: son cinco cadenas sin relaciones entre ellas, y una columna
-- por ajuste obligaría a una migración cada vez que se añade uno.

CREATE TABLE IF NOT EXISTS academy_setting (
    -- Clave en camelCase, la misma que viaja en el JSON del panel y del endpoint público.
    key        varchar(60) PRIMARY KEY,
    -- Siempre texto: el valor se valida en la API, que es donde está la regla de cada ajuste.
    -- Guardar '' es distinto de no tener fila: '' significa "puesto y vacío a propósito".
    value      text        NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    -- Quién lo cambió por última vez. La traza completa está en audit_log; esto es el atajo
    -- para no tener que buscarla. SET NULL: borrar a un admin no debe borrar el ajuste.
    updated_by uuid        NULL REFERENCES app_user (id) ON DELETE SET NULL
);

-- Valores de partida: exactamente la marca que hoy está escrita a mano en la cabecera y el
-- pie de la SPA. Así la pantalla de administración abre mostrando lo que se ve en la web y
-- no un formulario vacío que invita a inventarse un nombre.
--
-- ON CONFLICT DO NOTHING para que reaplicar el script no pise lo que ya haya cambiado el
-- negocio.
INSERT INTO academy_setting (key, value)
VALUES
    ('academyName',    'Inkoova Academy'),
    ('academyTagline', 'Formación práctica en IA aplicada para equipos que ya tienen trabajo.'),
    ('logoUrl',        ''),
    -- Vacío a propósito: el dominio real depende del despliegue y no debe inventarse aquí
    -- (CLAUDE.md, convención 6). La API cae en el host de Academy:PublicBaseUrl mientras esté
    -- vacío.
    ('publicDomain',   ''),
    ('supportEmail',   '')
ON CONFLICT (key) DO NOTHING;
