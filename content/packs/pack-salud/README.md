# C-16 · Pack sectorial · Salud

> Sector: Salud · slug `pack-salud` · versión `1.0.0`
> **Verificación normativa: 2026-09-01**

## Qué encaja de este sector en el Anexo III

- **Anexo III.5.a** · Uso por autoridades públicas, o en su nombre, para evaluar la admisibilidad de personas físicas a servicios y prestaciones esenciales de asistencia pública —incluidos los de asistencia sanitaria— y para concederlos, reducirlos, retirarlos o reclamar su devolución.
- **Anexo III.5.d** · Evaluación y clasificación de llamadas de emergencia, envío o priorización de servicios de primera intervención, y sistemas de triaje de pacientes en urgencias.

**El matiz que importa:** en salud la vía que más pesa NO suele ser el Anexo III, sino el art. 6.1: si el software es un producto sanitario y necesita evaluación de la conformidad por un tercero, ya es de alto riesgo por esa puerta. El MDR define «producto sanitario» incluyendo expresamente el «programa informático» destinado a diagnóstico, prevención, seguimiento, predicción, pronóstico, tratamiento o alivio de una enfermedad. Un modelo que predice una complicación clínica cae ahí antes de que nadie mire el Anexo III.

## Ficheros

- `checklist-clasificacion-riesgo.docx`
- `plantilla-evaluacion-riesgos.xlsx`
- `registro-logs-exigible.xlsx`
- `clausulas-modelo-proveedor-deployer.docx`
- `caso-resuelto-salud.pdf`

Se generan, no se editan a mano:

```bash
python content/tools/build_packs.py pack-salud
```

El contenido vive en `content/tools/packs/`: lo normativo común en `fuentes.py` —citado por
artículo— y lo de cada sector en `sectores.py`. Cambiar una cita ahí la cambia en los seis packs
a la vez, en lugar de en treinta ficheros.

## Fuentes

Abiertas y citadas por artículo. Ninguna afirmación normativa de estos documentos se escribió de
memoria.

- Reglamento (UE) 2024/1689 (Reglamento de Inteligencia Artificial), DOUE L de 12.7.2024. Texto consultado en EUR-Lex.
- Reglamento (UE) 2017/745 (MDR), art. 2.1 «producto sanitario». Texto consultado en EUR-Lex.
- Reglamento (UE) 2017/745 (MDR), Anexo VIII, Regla 11 «Reglas de clasificación». Texto consultado en EUR-Lex.
- Reglamento (UE) 2017/745 (MDR), conservación de la documentación técnica. Texto consultado en EUR-Lex.

## Aviso

Material formativo. No constituye asesoramiento jurídico. Las referencias normativas se verificaron en la fecha indicada en portada contra el texto oficial publicado en EUR-Lex; revísese con la asesoría jurídica antes de aplicarlo a un expediente real.

## Publicación

Los ficheros salen a `content/dist/packs/pack-salud/`, que es el volumen que sirve la API. La
versión se publica desde el panel de administración, que avisa por correo a quien ya descargó
una versión anterior.

El generador comprueba al terminar que no queden marcadores de verificación pendientes. Un pack
con marcadores dentro no se publica: quien lo compra lo aplica a un expediente real.
