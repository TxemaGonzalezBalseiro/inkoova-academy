# C-15 · Pack sectorial · Seguros y financiero

> Sector: Seguros y financiero · slug `pack-seguros-financiero` · versión `1.0.0`
> **Verificación normativa: 2026-09-01**

## Qué encaja de este sector en el Anexo III

- **Anexo III.5.b** · Evaluar la solvencia de personas físicas o establecer su calificación crediticia, SALVO los sistemas utilizados al objeto de detectar fraudes financieros.
- **Anexo III.5.c** · Evaluación de riesgos y fijación de precios en relación con personas físicas en el caso de los seguros DE VIDA Y DE SALUD.

**El matiz que importa:** el Anexo III.5.c cubre la evaluación de riesgos y la fijación de precios ÚNICAMENTE en los seguros de vida y de salud. Un modelo que tarifica un seguro de hogar o de automóvil no entra por esa vía. Y el 5.b excluye expresamente los sistemas destinados a detectar fraudes financieros: un motor antifraude no es alto riesgo por ser antifraude, aunque puntúe a personas. Las dos son delimitaciones que se pasan por alto a menudo, en las dos direcciones.

## Ficheros

- `checklist-clasificacion-riesgo.docx`
- `plantilla-evaluacion-riesgos.xlsx`
- `registro-logs-exigible.xlsx`
- `clausulas-modelo-proveedor-deployer.docx`
- `caso-resuelto-meridiana.pdf`

Se generan, no se editan a mano:

```bash
python content/tools/build_packs.py pack-seguros-financiero
```

El contenido vive en `content/tools/packs/`: lo normativo común en `fuentes.py` —citado por
artículo— y lo de cada sector en `sectores.py`. Cambiar una cita ahí la cambia en los seis packs
a la vez, en lugar de en treinta ficheros.

## Fuentes

Abiertas y citadas por artículo. Ninguna afirmación normativa de estos documentos se escribió de
memoria.

- Reglamento (UE) 2024/1689 (Reglamento de Inteligencia Artificial), DOUE L de 12.7.2024. Texto consultado en EUR-Lex.
- Reglamento (UE) 2022/2554 (DORA), art. 30 «Cláusulas contractuales fundamentales». Texto consultado en EUR-Lex.

## Aviso

Material formativo. No constituye asesoramiento jurídico. Las referencias normativas se verificaron en la fecha indicada en portada contra el texto oficial publicado en EUR-Lex; revísese con la asesoría jurídica antes de aplicarlo a un expediente real.

## Publicación

Los ficheros salen a `content/dist/packs/pack-seguros-financiero/`, que es el volumen que sirve la API. La
versión se publica desde el panel de administración, que avisa por correo a quien ya descargó
una versión anterior.

El generador comprueba al terminar que no queden marcadores de verificación pendientes. Un pack
con marcadores dentro no se publica: quien lo compra lo aplica a un expediente real.
