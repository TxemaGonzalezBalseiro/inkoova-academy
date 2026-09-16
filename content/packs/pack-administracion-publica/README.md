# C-18 · Pack sectorial · Administración pública

> Sector: Administración pública · slug `pack-administracion-publica` · versión `1.0.0`
> **Verificación normativa: 2026-09-01**

## Qué encaja de este sector en el Anexo III

- **Anexo III.5.a** · Uso por autoridades públicas, o en su nombre, para evaluar la admisibilidad de personas físicas a servicios y prestaciones esenciales de asistencia pública —incluidos los de asistencia sanitaria— y para concederlos, reducirlos, retirarlos o reclamar su devolución.
- **Anexo III.6** · Garantía del cumplimiento del Derecho (letras a a e: evaluación del riesgo de victimización, polígrafos, fiabilidad de las pruebas, riesgo de delinquir o reincidir, y elaboración de perfiles durante la investigación).
- **Anexo III.7** · Migración, asilo y gestión del control fronterizo (letras a a d).
- **Anexo III.8** · Administración de justicia y procesos democráticos.

**El matiz que importa:** el sector público arrastra una obligación que casi ningún privado tiene: la evaluación de impacto relativa a los derechos fundamentales del art. 27, obligatoria ANTES de desplegar para los organismos de Derecho público y para las entidades privadas que prestan servicios públicos. Se exceptúan los sistemas del Anexo III.2, infraestructuras críticas.

## Ficheros

- `checklist-clasificacion-riesgo.docx`
- `plantilla-evaluacion-riesgos.xlsx`
- `registro-logs-exigible.xlsx`
- `clausulas-modelo-proveedor-deployer.docx`
- `caso-resuelto-administracion.pdf`

Se generan, no se editan a mano:

```bash
python content/tools/build_packs.py pack-administracion-publica
```

El contenido vive en `content/tools/packs/`: lo normativo común en `fuentes.py` —citado por
artículo— y lo de cada sector en `sectores.py`. Cambiar una cita ahí la cambia en los seis packs
a la vez, en lugar de en treinta ficheros.

## Fuentes

Abiertas y citadas por artículo. Ninguna afirmación normativa de estos documentos se escribió de
memoria.

- Reglamento (UE) 2024/1689 (Reglamento de Inteligencia Artificial), DOUE L de 12.7.2024. Texto consultado en EUR-Lex.
- Ley 40/2015, de Régimen Jurídico del Sector Público, art. 41 «Actuación administrativa automatizada». Texto consolidado consultado en el BOE.

## Aviso

Material formativo. No constituye asesoramiento jurídico. Las referencias normativas se verificaron en la fecha indicada en portada contra el texto oficial publicado en EUR-Lex; revísese con la asesoría jurídica antes de aplicarlo a un expediente real.

## Publicación

Los ficheros salen a `content/dist/packs/pack-administracion-publica/`, que es el volumen que sirve la API. La
versión se publica desde el panel de administración, que avisa por correo a quien ya descargó
una versión anterior.

El generador comprueba al terminar que no queden marcadores de verificación pendientes. Un pack
con marcadores dentro no se publica: quien lo compra lo aplica a un expediente real.
