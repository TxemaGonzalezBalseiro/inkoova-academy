-- V025 · El curso no pertenece a una marca.
--
-- V012 le puso `identity_id` al curso cuando se montaron las identidades. Era la pieza que
-- faltaba de un modelo distinto del que se eligió: en «marca + correo, alumnos compartidos» el
-- CATÁLOGO ES COMÚN. Los mismos cursos, los mismos alumnos y la misma facturación bajo varias
-- marcas; lo que cambia por marca es la presentación, el buzón, las plantillas de correo y el
-- estilo del certificado.
--
-- Con ese modelo, preguntar «¿de qué marca es este curso?» no tiene respuesta: es de todas. La
-- columna quedaba a NULL en todos los cursos y solo servía para que alguien la leyera algún día
-- y creyera que significa algo.
--
-- Se cae. Si algún día el catálogo deja de ser común, volver a añadirla es una migración de una
-- línea; mantener una columna que nadie escribe es una trampa que se cobra sola.

ALTER TABLE course DROP COLUMN IF EXISTS identity_id;
