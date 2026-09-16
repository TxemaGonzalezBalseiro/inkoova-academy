# T-11 · Panel de administración

## Alcance
- Rutas `/admin/*` solo rol `admin`.
- CRUD de cursos, secciones y lecciones con reordenación drag-and-drop; publicar/despublicar; marcar `featured`/`isNew`/banner "nuevo curso".
- Importador desde manifest (T-07) con vista previa de diff antes de aplicar.
- Gestión de usuarios: buscar, ver suscripción, otorgar acceso manual (becas/empresas), revocar.
- Métricas básicas: altas, MRR, churn, % completado por curso, lecciones donde más se abandona.
- Log de auditoría de acciones admin.

## Fuera de alcance
Editor WYSIWYG de slides (el contenido se sigue generando desde `content.py`).

## Criterios de aceptación
- Cambios de catálogo invalidan cache y aparecen en landing sin redeploy.
- Toda acción admin queda en auditoría con usuario y timestamp.

## Dependencias
T-02, T-03, T-04, T-07.

## Añadido (packs y programa)
- CRUD de packs: subir ficheros, publicar versión nueva, changelog; notificación por email a compradores de "nueva versión disponible".
- Configuración de programa: orden de cursos, prerequisitos, qué planes incluyen packs.
