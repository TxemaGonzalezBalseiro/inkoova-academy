-- V014 · El certificado deja de guardar un PDF.
--
-- El PDF se escribía en el volumen al emitir y ya no lo sirve nadie: la descarga lo regenera,
-- que es lo que hace que un rediseño o un cambio de marca lleguen también a quien emitió su
-- certificado hace meses.
--
-- Guardarlo no solo sobraba, era una fuente de contradicciones: el fichero se quedaba con el
-- estilo del día de la emisión, así que el volumen y lo que descargaba el alumno podían
-- enseñar dos documentos distintos del mismo certificado.
--
-- Lo que se verifica NO cambia. El hash se calcula sobre alumno, asunto y fecha —nunca sobre
-- los bytes del PDF—, así que ningún certificado emitido deja de poder comprobarse por esto.

ALTER TABLE certificate DROP COLUMN IF EXISTS pdf_content_ref;

-- Los ficheros que quedaron escritos siguen en el volumen, bajo `certificates/`. No se borran
-- desde aquí: una migración que borra ficheros del disco es una migración que puede borrar
-- algo que alguien todavía quería. Se limpian a mano cuando se decida.
