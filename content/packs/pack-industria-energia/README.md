# C-20 · Pack sectorial · Industria y energía

> Sector: Industria y energía · slug `pack-industria-energia` · versión `1.0.0`
> **Verificación normativa: 2026-09-01**

## Qué encaja de este sector en el Anexo III

- **Anexo III.2** · Infraestructuras críticas: componentes de seguridad en la gestión y el funcionamiento de las infraestructuras digitales críticas, del tráfico rodado o del suministro de agua, gas, calefacción o electricidad.

**El matiz que importa:** el Anexo III.2 es estrecho a propósito: cubre los sistemas usados como COMPONENTES DE SEGURIDAD en la gestión y el funcionamiento de infraestructuras digitales críticas, tráfico rodado o suministro de agua, gas, calefacción o electricidad. Optimizar el consumo o predecir una avería no es un componente de seguridad; lo es aquello cuyo fallo compromete la seguridad del suministro. Además, este es el único ámbito del Anexo III exceptuado de la evaluación de impacto del art. 27.

## Ficheros

- `checklist-clasificacion-riesgo.docx`
- `plantilla-evaluacion-riesgos.xlsx`
- `registro-logs-exigible.xlsx`
- `clausulas-modelo-proveedor-deployer.docx`
- `caso-resuelto-industria.pdf`

Se generan, no se editan a mano:

```bash
python content/tools/build_packs.py pack-industria-energia
```

El contenido vive en `content/tools/packs/`: lo normativo común en `fuentes.py` —citado por
artículo— y lo de cada sector en `sectores.py`. Cambiar una cita ahí la cambia en los seis packs
a la vez, en lugar de en treinta ficheros.

## Fuentes

Abiertas y citadas por artículo. Ninguna afirmación normativa de estos documentos se escribió de
memoria.

- Reglamento (UE) 2024/1689 (Reglamento de Inteligencia Artificial), DOUE L de 12.7.2024. Texto consultado en EUR-Lex.

## Aviso

Material formativo. No constituye asesoramiento jurídico. Las referencias normativas se verificaron en la fecha indicada en portada contra el texto oficial publicado en EUR-Lex; revísese con la asesoría jurídica antes de aplicarlo a un expediente real.

## Publicación

Los ficheros salen a `content/dist/packs/pack-industria-energia/`, que es el volumen que sirve la API. La
versión se publica desde el panel de administración, que avisa por correo a quien ya descargó
una versión anterior.

El generador comprueba al terminar que no queden marcadores de verificación pendientes. Un pack
con marcadores dentro no se publica: quien lo compra lo aplica a un expediente real.
