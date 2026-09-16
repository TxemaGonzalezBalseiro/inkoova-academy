#!/usr/bin/env python3
"""Genera el esqueleto de los bloques de contenido (C-01 → C-21).

Qué genera y qué no, y por qué:

**Sí genera** la estructura pedagógica completa de cada bloque: objetivo, guion de slides con
una idea por slide, los tres puntos de «qué te llevas», los temas del mini-quiz y el
enunciado del lab. Esa parte se puede decidir de antemano y es la que da forma al curso.

**No genera** la prosa de cada slide ni las afirmaciones normativas. En los cursos 3 y 4 cada
referencia a un artículo lleva su fecha de verificación, y eso exige tener la fuente primaria
delante. El README del plan lo dice sin rodeos: nunca inventar plazos. Un generador que
escupiera «según el artículo 6.2, desde agosto de 2026…» produciría material que parece
verificado y no lo está, que es peor que no tener el bloque.

Uso:
    python content/tools/scaffold.py                    # todos los bloques
    python content/tools/scaffold.py --task C-02        # solo uno
    python content/tools/scaffold.py --force            # sobrescribe lo ya escrito
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from programa import BLOCKS, PACKS, Block, Pack  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]

MIN_SLIDES = 25
MAX_SLIDES = 40


def render_block(block: Block) -> str:
    lines: list[str] = []

    lines.append(f"# {block.task} · {block.block_id} · {block.title}")
    lines.append("")
    lines.append(f"> Curso: `{block.course}` · bloque `{block.block_id}`")
    lines.append("")
    lines.append("## Objetivo")
    lines.append("")
    lines.append(block.objective)
    lines.append("")

    if block.needs_verification:
        lines.append("## ⚠ Bloque normativo: verificación obligatoria antes de publicar")
        lines.append("")
        lines.append(
            "Cada afirmación de este bloque debe citar artículo y fecha de verificación, y esa "
            "fecha debe salir de haber abierto la fuente primaria. No se redacta de memoria ni "
            "a partir de resúmenes de terceros."
        )
        lines.append("")
        lines.append("**Fuentes primarias a consultar:**")
        lines.append("")
        for source in block.sources:
            lines.append(f"- [ ] {source}")
        lines.append("")
        lines.append("**Fecha de verificación:** `TODO(YYYY-MM-DD)`")
        lines.append("")
        lines.append(
            "> Aviso al alumno que debe aparecer en el bloque: este material es formativo y no "
            "constituye asesoramiento jurídico."
        )
        lines.append("")

    lines.append("## Guion de slides")
    lines.append("")
    lines.append(
        f"{len(block.slides)} slides de contenido. Una idea por slide, con un ejemplo real o "
        "del caso Meridiana. Nada de relleno."
    )
    lines.append("")

    if not MIN_SLIDES <= len(block.slides) <= MAX_SLIDES:
        lines.append(
            f"> **Aviso de estructura:** este guion tiene {len(block.slides)} slides y la "
            f"convención del programa son {MIN_SLIDES}–{MAX_SLIDES}. "
            "Amplía o divide antes de escribir la prosa."
        )
        lines.append("")

    for index, slide in enumerate(block.slides, start=1):
        lines.append(f"### {index}. {slide}")
        lines.append("")
        lines.append("`TODO(prosa)` · idea única, ejemplo del caso, sin relleno.")
        lines.append("")

    lines.append("## Qué te llevas")
    lines.append("")
    for takeaway in block.takeaways:
        lines.append(f"- {takeaway}")
    lines.append("")

    lines.append("## Mini-quiz (3 preguntas)")
    lines.append("")
    lines.append(
        "Una sola opción correcta por pregunta, con explicación. Se generan con el mismo "
        "formato que `assets/js/quizzes-data.js` para que el importador las recoja."
    )
    lines.append("")
    for index, topic in enumerate(block.quiz_topics, start=1):
        lines.append(f"{index}. **Tema:** {topic}")
        lines.append("   - `TODO(enunciado)`")
        lines.append("   - `TODO(4 opciones, 1 correcta)`")
        lines.append("   - `TODO(explicación de por qué la correcta lo es)`")
        lines.append("")

    lines.append("## Lab")
    lines.append("")
    if block.lab:
        lines.append(block.lab)
        lines.append("")
        lines.append("- Reproducible desde el repositorio `meridiana-agent` en menos de 60 minutos.")
        lines.append("- Sin servicios de pago: todo en local o en free tier.")
        lines.append("- `TODO(enunciado detallado, criterios de aceptación y solución de referencia)`")
    else:
        lines.append(
            "Este curso no lleva labs de código. En su lugar, ejercicio de plantilla "
            "(DOCX/XLSX) sobre el caso Meridiana."
        )
        lines.append("")
        lines.append("- `TODO(plantilla del ejercicio)`")
        lines.append("- `TODO(solución de referencia)`")
    lines.append("")

    lines.append("## Cierre")
    lines.append("")
    lines.append("- Recapitulación en los tres puntos de arriba.")
    lines.append("- Mini-quiz.")
    lines.append("- Enlace al siguiente bloque.")
    lines.append("")

    return "\n".join(lines)


def render_pack(pack: Pack) -> str:
    lines: list[str] = []

    lines.append(f"# {pack.task} · {pack.title}")
    lines.append("")
    lines.append(f"> Sector: {pack.sector} · slug `{pack.slug}` · versión inicial `1.0.0`")
    lines.append("")
    lines.append("## Qué incluye")
    lines.append("")
    lines.append(pack.summary)
    lines.append("")

    lines.append("## Ficheros")
    lines.append("")
    lines.append("Cinco documentos editables más el PDF resumen. Sin logos ni textos de terceros.")
    lines.append("")
    for filename in pack.files:
        lines.append(f"- [ ] `{filename}` · `TODO(contenido)`")
    lines.append("")

    lines.append("## ⚠ Verificación normativa obligatoria")
    lines.append("")
    lines.append(
        "Cada fichero lleva en portada su versión y la **fecha de verificación normativa**. "
        "Esa fecha sale de haber abierto las fuentes de abajo, no de estimarla."
    )
    lines.append("")
    for source in pack.sources:
        lines.append(f"- [ ] {source}")
    lines.append("")
    lines.append("**Fecha de verificación:** `TODO(YYYY-MM-DD)`")
    lines.append("")
    lines.append(
        "> Aviso en portada de cada fichero: material formativo, no constituye asesoramiento "
        "jurídico. Revísese con la asesoría antes de aplicarlo a un expediente real."
    )
    lines.append("")

    lines.append("## Ficha del pack en la plataforma")
    lines.append("")
    lines.append("- **Qué incluye:** `TODO`")
    lines.append("- **A quién va dirigido:** `TODO`")
    lines.append("- **Changelog de la versión 1.0.0:** `TODO`")
    lines.append("")

    lines.append("## Publicación")
    lines.append("")
    lines.append("```bash")
    lines.append("# 1. Subir los ficheros al volumen de contenido")
    lines.append(f"#    content/dist/packs/{pack.slug}/")
    lines.append("# 2. Publicar la versión desde el panel de administración,")
    lines.append("#    que avisa por email a quien ya descargó una versión anterior.")
    lines.append("```")
    lines.append("")

    return "\n".join(lines)


def render_final_project() -> str:
    return """# C-21 · Proyecto final y certificado de programa

> Cierra los cuatro cursos. Se entrega al terminar Gobernanza EU.

## Enunciado

Llevar el agente Meridiana —o uno propio con un caso equivalente— a **listo para
producción**. No se pide que esté desplegado: se pide que esté en condiciones de estarlo y
que puedas demostrarlo.

El entregable es un repositorio y un expediente. Nada más, y nada menos.

## Qué se entrega

1. **Repositorio** con:
   - Arquitectura por capas y las reglas críticas en código, fuera del prompt (curso 3, B1).
   - Observabilidad con trazas que permitan reconstruir una decisión concreta (B2).
   - Evals en CI con umbrales que bloquean el merge (B3).
   - Coste por caso instrumentado y presupuesto por ejecución (B4).
   - Pruebas adversariales en el conjunto de evaluación (B5).
   - Plan de despliegue progresivo escrito, con criterios de promoción y reversión (B6).
   - Runbooks e interruptor de apagado (B7).

2. **Expediente técnico** con:
   - Clasificación de riesgo bajo el AI Act, **justificada por escrito** (curso 4, B2).
   - Mapa de roles proveedor/deployer y qué se exige por contrato (B3).
   - Documentación técnica según el índice del Anexo IV (B4).
   - Tratamiento de datos personales y, si procede, evaluación de impacto (B5).
   - Gobernanza mínima: inventario, roles y vigilancia poscomercialización (B6).

## Rúbrica de autoevaluación

Marca solo lo que puedas demostrar señalando un fichero o una traza. «Lo tengo pensado» no
cuenta.

### Arquitectura y código (25 %)

- [ ] Las reglas con consecuencias reguladas están en código, no en el prompt.
- [ ] El sistema funciona (degradado) con el proveedor del modelo caído.
- [ ] Reprocesar el mismo caso dos veces no duplica efectos.
- [ ] El modelo es sustituible sin tocar el loop.

### Observabilidad (15 %)

- [ ] Puedo reconstruir por qué el sistema decidió X en un caso concreto de hace un mes.
- [ ] Las trazas no contienen datos personales sin tratar.
- [ ] Los casos que fallan se guardan siempre, aunque haya muestreo.

### Evals (20 %)

- [ ] Hay un conjunto de evaluación con casos difíciles a propósito.
- [ ] Los evals corren en CI y bloquean el merge por debajo de un umbral.
- [ ] Las métricas están segmentadas, no solo agregadas.
- [ ] Al menos un eval cubre una regla de seguridad concreta.

### Coste y operación (15 %)

- [ ] Sé el coste por caso y el coste mensual estimado.
- [ ] Hay presupuesto por ejecución que corta un bucle caro.
- [ ] Existen runbooks por modo de fallo, no uno genérico.
- [ ] Se puede apagar el agente y seguir operando.

### Gobernanza (25 %)

- [ ] La clasificación de riesgo está escrita y argumentada, con sus fuentes.
- [ ] El mapa de roles está claro y lo que exijo al proveedor está por contrato.
- [ ] El expediente técnico sigue el índice del Anexo IV y está completo.
- [ ] Cada afirmación normativa lleva artículo y fecha de verificación.
- [ ] La gobernanza dice quién mantiene esto cuando yo no esté.

## Tiempo estimado

Entre 8 y 12 horas si has hecho los labs de los cuatro cursos. Si partes de cero con un caso
propio, cuenta el doble.

## Entrega y revisión

- **Autoevaluación:** cualquier alumno, con la rúbrica de arriba.
- **Revisión con devolución escrita:** incluida en el plan Elite.

## Certificado de programa

Se emite al completar los cuatro cursos y marcar el proyecto como entregado. Lleva código
público verificable, igual que los de curso, y el ámbito «programa» en lugar de un curso
concreto.

`TODO(C-21)`: definir el criterio exacto de «proyecto entregado» — autodeclaración frente a
revisión — antes del lanzamiento. La emisión técnica ya está implementada en
`IssueProgramCertificateHandler`.
"""


def render_index() -> str:
    lines = [
        "# Programa · mapa de contenido",
        "",
        "Generado por `content/tools/scaffold.py` a partir de `content/tools/programa.py`.",
        "No editar a mano: los cambios de estructura van en `programa.py`.",
        "",
        "| Tarea | Curso | Bloque | Título | Slides | Lab | Verificación normativa |",
        "|---|---|---|---|---:|---|---|",
    ]

    for block in BLOCKS:
        lines.append(
            f"| {block.task} | `{block.course}` | {block.block_id} | {block.title} | "
            f"{len(block.slides)} | {'sí' if block.lab else 'plantilla'} | "
            f"{'**obligatoria**' if block.needs_verification else '—'} |"
        )

    lines.append("")
    lines.append("## Packs sectoriales")
    lines.append("")
    lines.append("| Tarea | Slug | Sector | Ficheros |")
    lines.append("|---|---|---|---:|")

    for pack in PACKS:
        lines.append(f"| {pack.task} | `{pack.slug}` | {pack.sector} | {len(pack.files)} |")

    lines.append("")
    lines.append("## Orden de producción")
    lines.append("")
    lines.append(
        "C-00 → C-02..C-08 (curso 3) → C-01 → C-09..C-14 (curso 4) → C-15 (pack seguros) → "
        "resto de packs → C-21."
    )
    lines.append("")
    lines.append("## Estado")
    lines.append("")
    lines.append("- **C-00** · caso conductor: completo. Datos sintéticos y repo de partida funcionando.")
    lines.append("- **C-01 → C-21** · esqueleto generado; la prosa está pendiente.")
    lines.append(
        "- Los bloques marcados con verificación obligatoria **no se publican** sin haber "
        "abierto sus fuentes primarias y anotado la fecha."
    )
    lines.append("")

    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="Genera el esqueleto de los bloques de contenido.")
    parser.add_argument("--task", help="Genera solo esta tarea, p. ej. C-02")
    parser.add_argument("--force", action="store_true", help="Sobrescribe ficheros ya existentes")
    args = parser.parse_args()

    written = 0
    skipped = 0

    for block in BLOCKS:
        if args.task and block.task != args.task:
            continue

        directory = ROOT / "cursos" / block.course
        directory.mkdir(parents=True, exist_ok=True)
        path = directory / f"{block.block_id}-{slug(block.title)}.md"

        if path.exists() and not args.force:
            skipped += 1
            continue

        path.write_text(render_block(block) + "\n", encoding="utf-8")
        print(f"{block.task}: {path.relative_to(ROOT)}")
        written += 1

    for pack in PACKS:
        if args.task and pack.task != args.task:
            continue

        directory = ROOT / "packs" / pack.slug
        directory.mkdir(parents=True, exist_ok=True)
        path = directory / "README.md"

        if path.exists() and not args.force:
            skipped += 1
            continue

        path.write_text(render_pack(pack) + "\n", encoding="utf-8")
        print(f"{pack.task}: {path.relative_to(ROOT)}")
        written += 1

    if not args.task or args.task == "C-21":
        path = ROOT / "cursos" / "proyecto-final.md"
        path.parent.mkdir(parents=True, exist_ok=True)

        if not path.exists() or args.force:
            path.write_text(render_final_project(), encoding="utf-8")
            print(f"C-21: {path.relative_to(ROOT)}")
            written += 1
        else:
            skipped += 1

    if not args.task:
        index = ROOT / "PROGRAMA.md"
        index.write_text(render_index() + "\n", encoding="utf-8")
        print(f"índice: {index.relative_to(ROOT)}")

    print(f"\n{written} ficheros escritos, {skipped} ya existían (usa --force para sobrescribir).")
    return 0


def slug(value: str) -> str:
    import re
    import unicodedata

    normalized = unicodedata.normalize("NFD", value)
    stripped = "".join(c for c in normalized if unicodedata.category(c) != "Mn")
    return re.sub(r"-{2,}", "-", re.sub(r"[^a-zA-Z0-9]+", "-", stripped).strip("-").lower())


if __name__ == "__main__":
    raise SystemExit(main())
