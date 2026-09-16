-- V010 · La página «Sobre mí», editable desde el panel.
--
-- Va en `academy_setting` y no en una tabla propia porque es lo mismo que el nombre y el lema:
-- identidad de la academia que el negocio cambia sin desplegar. Una tabla nueva para cuatro
-- cadenas sin relaciones entre ellas solo añadiría un JOIN.
--
-- El cuerpo se guarda como TEXTO PLANO con un marcado mínimo (`## título`, `- punto`), no como
-- HTML. Guardar HTML editable por el panel y pintarlo tal cual sería un XSS almacenado servido
-- a todo visitante anónimo: el panel es de confianza, pero una sesión de administración robada
-- no debería poder inyectar scripts en la portada.

INSERT INTO academy_setting (key, value)
VALUES
    -- Quién firma el programa. Vacío = se usa el nombre de la academia.
    ('aboutName',     ''),
    ('aboutHeadline', ''),
    ('aboutBody',     ''),
    ('aboutPhotoUrl', '')
ON CONFLICT (key) DO NOTHING;
