# T-07 · Player de curso e importación del contenido existente

## Contexto
El curso ya existe como HTML plano (secciones con `IntersectionObserver`, hash routing, theme switcher) generado desde `content.py`. No se reescribe: se **importa** como lecciones y se **sirve** dentro del player.

## Alcance
- Herramienta CLI `Inkoova.Academy.Importer` (o script Python en `/content/tools`) que lee la carpeta del curso y genera un `course.manifest.json`: bloques → secciones, slides/labs → lecciones, duración estimada, assets.
- Reemplazar el array `BLOQUES` hardcodeado por lectura del manifest (elimina el mantenimiento manual).
- Subida de assets a Azure Blob (contenedor privado) con versionado por hash; la API emite URLs SAS de corta vida solo si `IAccessPolicy` lo permite.
- Player `/aprender/{courseSlug}/{lessonSlug}`: sidebar de temario con estado, iframe sandbox para lecciones `slides` (postMessage para progreso y navegación), vista Markdown para `lab`, botón "Marcar completada", atajos de teclado, siguiente/anterior.
- Progreso automático al llegar a la última slide (evento desde el iframe).
- Persistencia de última posición.
- Tema del player alineado con el theme switcher del contenido (Inkoova por defecto).

## Fuera de alcance
Edición de contenido desde el admin (T-11). Vídeo (backlog: si se graban clases, añadir tipo `video` con Mux/Bunny).

## Criterios de aceptación
- Importar el curso completo de Agent Engineering (B0–B7 + labs) sin edición manual.
- Una URL SAS caducada devuelve 403 y el player reintenta obtención.
- Progreso sobrevive a recarga y a cambio de dispositivo.
- El HTML original sigue funcionando por doble clic fuera de la plataforma (no se rompe el flujo autodidacta).

## Dependencias
T-02, T-03, T-06.

## Añadido (descargas)
- Tipo de lección `download`: lista de ficheros (DOCX/XLSX/PDF) con tamaño, versión y URL firmada de 60 s emitida por la API tras `IAccessPolicy`.
- Registro de descargas (`DownloadLog`) para métricas y para avisar de versiones nuevas.
