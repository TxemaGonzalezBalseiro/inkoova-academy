# C-17 · Pack sectorial · Recursos humanos

> Sector: Recursos humanos · slug `pack-rrhh` · versión `1.0.0`
> **Verificación normativa: 2026-09-01**

## Qué encaja de este sector en el Anexo III

- **Anexo III.4.a** · Contratación o selección de personas físicas, en particular para publicar anuncios de empleo específicos, analizar y filtrar las solicitudes y evaluar a los candidatos.
- **Anexo III.4.b** · Decisiones que afecten a las condiciones de las relaciones laborales, a la promoción o rescisión de relaciones contractuales, asignación de tareas a partir de comportamientos individuales o rasgos personales, y supervisión y evaluación del rendimiento y el comportamiento.

**El matiz que importa:** el punto 4 es de los más amplios del Anexo III y de los que menos escapatoria dejan. Cubre no solo seleccionar, sino «publicar anuncios de empleo específicos» y «analizar y filtrar las solicitudes». Y el 4.b llega a la asignación de tareas y a la supervisión del rendimiento. Casi cualquier herramienta de RR. HH. con IA que toque a una persona concreta entra por alguna de las dos letras.

## Ficheros

- `checklist-clasificacion-riesgo.docx`
- `plantilla-evaluacion-riesgos.xlsx`
- `registro-logs-exigible.xlsx`
- `clausulas-modelo-proveedor-deployer.docx`
- `caso-resuelto-rrhh.pdf`

Se generan, no se editan a mano:

```bash
python content/tools/build_packs.py pack-rrhh
```

El contenido vive en `content/tools/packs/`: lo normativo común en `fuentes.py` —citado por
artículo— y lo de cada sector en `sectores.py`. Cambiar una cita ahí la cambia en los seis packs
a la vez, en lugar de en treinta ficheros.

## Fuentes

Abiertas y citadas por artículo. Ninguna afirmación normativa de estos documentos se escribió de
memoria.

- Reglamento (UE) 2024/1689 (Reglamento de Inteligencia Artificial), DOUE L de 12.7.2024. Texto consultado en EUR-Lex.
- Real Decreto Legislativo 2/2015, Estatuto de los Trabajadores, art. 64.4.d. Texto consolidado consultado en el BOE.

## Aviso

Material formativo. No constituye asesoramiento jurídico. Las referencias normativas se verificaron en la fecha indicada en portada contra el texto oficial publicado en EUR-Lex; revísese con la asesoría jurídica antes de aplicarlo a un expediente real.

## Publicación

Los ficheros salen a `content/dist/packs/pack-rrhh/`, que es el volumen que sirve la API. La
versión se publica desde el panel de administración, que avisa por correo a quien ya descargó
una versión anterior.

El generador comprueba al terminar que no queden marcadores de verificación pendientes. Un pack
con marcadores dentro no se publica: quien lo compra lo aplica a un expediente real.
