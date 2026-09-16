# C-19 · Pack sectorial · Retail y comercio electrónico

> Sector: Retail y comercio electrónico · slug `pack-retail-ecommerce` · versión `1.0.0`
> **Verificación normativa: 2026-09-01**

## Qué encaja de este sector en el Anexo III

- **Anexo III.5.b** · Evaluar la solvencia de personas físicas o establecer su calificación crediticia, SALVO los sistemas utilizados al objeto de detectar fraudes financieros.

**El matiz que importa:** este es el sector donde lo más probable es que NO haya alto riesgo, y saberlo con fundamento vale dinero. Recomendar productos, personalizar una portada, prever demanda o fijar precios de artículos no está en el Anexo III. La excepción real llega por el 5.b: en cuanto se evalúa la solvencia de una persona —pago aplazado, financiación en el punto de venta— se entra en la lista, aunque el negocio sea vender zapatillas.

## Ficheros

- `checklist-clasificacion-riesgo.docx`
- `plantilla-evaluacion-riesgos.xlsx`
- `registro-logs-exigible.xlsx`
- `clausulas-modelo-proveedor-deployer.docx`
- `caso-resuelto-retail.pdf`

Se generan, no se editan a mano:

```bash
python content/tools/build_packs.py pack-retail-ecommerce
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

Los ficheros salen a `content/dist/packs/pack-retail-ecommerce/`, que es el volumen que sirve la API. La
versión se publica desde el panel de administración, que avisa por correo a quien ya descargó
una versión anterior.

El generador comprueba al terminar que no queden marcadores de verificación pendientes. Un pack
con marcadores dentro no se publica: quien lo compra lo aplica a un expediente real.
