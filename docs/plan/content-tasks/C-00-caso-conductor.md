# C-00 · Caso conductor "Aseguradora Meridiana"

## Alcance
- Documento `caso/meridiana.md`: aseguradora mediana española, agente de gestión de siniestros de auto (FNOL → triaje → petición de documentación → propuesta de resolución con aprobación humana).
- Datos sintéticos: 30 siniestros de ejemplo (JSON), 5 pólizas, catálogo de coberturas simplificado. Sin datos reales.
- Mapa de en qué bloque de cada curso aparece el caso y qué se construye sobre él.
- Repo `meridiana-agent` de partida (C#/.NET, misma estructura que los labs del curso 2) con el agente del curso 2 ya funcionando: es el punto de partida del curso 3.

## Criterios de aceptación
- Los 4 cursos referencian el mismo caso sin contradicciones (revisar glosario).
- El repo arranca con `dotnet run` y resuelve un siniestro sintético end-to-end.
