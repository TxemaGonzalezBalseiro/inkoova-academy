#!/usr/bin/env python3
"""Importador de contenido de la academia (T-07).

Lee la carpeta del curso tal y como la genera `content.py` y produce un
`course.manifest.json` que la plataforma consume.

El origen NO se modifica nunca: sigue abriéndose con doble clic fuera de la plataforma, que es
un criterio de aceptación de T-07. Las transformaciones ocurren sobre la copia que se sirve
(`--copy-to`), y son tres, todas por la misma razón: el original no puede saber que va dentro
de esta plataforma.

- `split_quiz_bundles`: el banco de quizzes, un fichero por documento. Compartido, el token de
  una lección gratuita alcanzaba las preguntas de los bloques de pago.
- `split_single_file_course`: el curso que vive en un solo HTML, un documento por lección.
  Entero, el token de una lección gratuita servía el curso de pago completo.
- `normalise_theme`: el tema se hereda del player en vez de elegirse dos veces.

Uso:
    python content/tools/importer.py --source "<carpeta del curso>" --out content/manifests
    python content/tools/importer.py --source ... --out ... --copy-to /srv/content

El manifest sustituye al array `BLOQUES` hardcodeado del index.html original: la plataforma
y el índice autodidacta leen la misma fuente y dejan de poder divergir.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import unicodedata
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).parent))
from jsobj import load_assignment  # noqa: E402
from html_course import PANE, group_lessons, parse_block_slides, parse_panes  # noqa: E402

MANIFEST_VERSION = 1

# Ritmo de lectura estimado para prosa técnica densa en castellano. Se usa solo cuando el
# contenido no declara duración; es una estimación explícita, no un dato inventado que
# después se muestre como si fuera medido.
WORDS_PER_MINUTE = 130

# Una slide de portada o de cierre no aporta tiempo real de estudio.
MINIMUM_LESSON_MINUTES = 3


@dataclass
class LessonManifest:
    slug: str
    title: str
    type: str
    durationMinutes: int
    contentRef: str
    isFreePreview: bool
    isRequired: bool = True
    sha256: str | None = None


@dataclass
class SectionManifest:
    title: str
    order: int
    lessons: list[LessonManifest] = field(default_factory=list)


@dataclass
class QuestionManifest:
    category: str
    text: str
    explanation: str
    options: list[dict[str, Any]]


@dataclass
class QuizManifest:
    slug: str
    title: str
    kind: str
    questions: list[QuestionManifest] = field(default_factory=list)


@dataclass
class CourseManifest:
    manifestVersion: int
    courseSlug: str
    title: str
    shortDescription: str
    longDescription: str
    level: str
    sections: list[SectionManifest] = field(default_factory=list)
    quizzes: list[QuizManifest] = field(default_factory=list)
    assets: list[str] = field(default_factory=list)


def slugify(value: str) -> str:
    """Mismo criterio que `Slug.FromTitle` en el dominio, para que los slugs coincidan."""
    normalized = unicodedata.normalize("NFD", value)
    stripped = "".join(c for c in normalized if unicodedata.category(c) != "Mn")
    lowered = re.sub(r"[^a-zA-Z0-9]+", "-", stripped).strip("-").lower()
    return re.sub(r"-{2,}", "-", lowered)[:120].strip("-")


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def estimate_minutes(html: str) -> int:
    """Estima duración a partir del texto visible del bloque.

    Se descartan `<script>` y `<style>`: en estos ficheros el JS del visor pesa más que la
    prosa y contarlo dispararía la estimación.
    """
    without_code = re.sub(r"<(script|style)\b.*?</\1>", " ", html, flags=re.DOTALL | re.IGNORECASE)
    text = re.sub(r"<[^>]+>", " ", without_code)
    words = len(text.split())
    return max(MINIMUM_LESSON_MINUTES, round(words / WORDS_PER_MINUTE))


def read_index(source: Path) -> list[dict[str, Any]]:
    """Índice de bloques. Prefiere `parser/indice.json`; si no está, escanea `bloques/`."""
    index_path = source / "parser" / "indice.json"

    if index_path.exists():
        return json.loads(index_path.read_text(encoding="utf-8"))

    blocks = []
    for path in sorted((source / "bloques").glob("*.html")):
        html = path.read_text(encoding="utf-8", errors="replace")

        # El deck ya declara quién es: la etiqueta del bloque en la primera slide y el nombre
        # que le pasa al visor. Se lee de ahí y no del nombre del fichero, porque partirlo por
        # el primer guion rompe con identificadores que llevan uno dentro: «PE-A-prompts…»
        # daba el bloque «PE» y la sección «A prompts como codigo».
        tag = re.search(r'class="bloque-tag">([^<]+)<', html)
        name = re.search(r'setBloqueName\("([^"]+)"\)', html)

        if tag:
            block_id = tag.group(1).strip()
            block_name = name.group(1).strip() if name else path.stem
        else:
            block_id = path.stem.split("-")[0]
            block_name = path.stem.split("-", 1)[1].replace("-", " ").capitalize()

        blocks.append({"id": block_id, "nombre": block_name, "filename": path.name})

    return blocks


def import_admission_quiz(source: Path, course_slug: str) -> QuizManifest | None:
    """Importa `assets/js/admission-quiz-data.js` (12 preguntas, 4 dimensiones)."""
    path = source / "assets" / "js" / "admission-quiz-data.js"
    if not path.exists():
        return None

    raw = load_assignment(path.read_text(encoding="utf-8"), "ADMISSION_QUIZ")

    questions = [
        QuestionManifest(
            category=item.get("cat", "General"),
            text=item["q"],
            explanation=item.get("explanation", ""),
            options=[
                {"index": i, "text": option["text"], "isCorrect": bool(option.get("correct", False))}
                for i, option in enumerate(item["options"])
            ],
        )
        for item in raw
    ]

    # Convención de la plataforma: el quiz de admisión de un curso vive en {slug}-nivel.
    return QuizManifest(
        slug=f"{course_slug}-nivel",
        title="Test de nivel",
        kind="admission",
        questions=questions,
    )


def import_block_quizzes(source: Path, course_slug: str, files: list[Path]) -> list[QuizManifest]:
    """Importa mini-quiz de bloque desde los ficheros indicados.

    Los ficheros se pasan explícitamente porque el curso y el pre-curso tienen cada uno los
    suyos: leerlos todos para ambos les asignaría los quizzes del otro.
    """
    quizzes: list[QuizManifest] = []

    for path in files:
        if not path.exists():
            continue

        raw = load_assignment(path.read_text(encoding="utf-8"), "QUIZZES")

        for key, items in raw.items():
            quizzes.append(
                QuizManifest(
                    slug=slugify(f"{course_slug}-{key}"),
                    title=f"Comprobación {key}",
                    kind="blockcheck",
                    questions=[
                        QuestionManifest(
                            category=item.get("cat", key),
                            text=item["q"],
                            explanation=item.get("explanation", ""),
                            options=[
                                {
                                    "index": i,
                                    "text": option["text"],
                                    "isCorrect": bool(option.get("correct", False)),
                                }
                                for i, option in enumerate(item["options"])
                            ],
                        )
                        for item in items
                    ],
                )
            )

    return quizzes


def import_course(
    source: Path,
    course_slug: str,
    free_blocks: set[str],
    title: str | None = None,
    short_description: str | None = None,
    long_description: str | None = None,
    level: str = "advanced",
) -> CourseManifest:
    """
    Importa un curso de bloques.

    El título y las descripciones llegan por parámetro. Estaban fijos al primer curso, así que
    cualquier curso nuevo se importaba llamándose «Agent Engineering v3.0»: dos cursos con el
    mismo nombre en el catálogo. Se editan después desde el panel; esto es solo el valor
    inicial (T-11).
    """
    blocks = read_index(source)
    manifest = CourseManifest(
        manifestVersion=MANIFEST_VERSION,
        courseSlug=course_slug,
        title=title or "Agent Engineering v3.0",
        shortDescription=short_description or "Construye agentes de IA que sobreviven en producción.",
        longDescription=long_description or (
            "Programa de capacitación en sistemas LLM para builders senior. Agnóstico de "
            "framework: se aprenden los invariantes, no la librería de moda."
        ),
        level=level,
    )

    for order, block in enumerate(blocks):
        block_id = block["id"]
        filename = block["filename"]
        html_path = source / "bloques" / filename

        if not html_path.exists():
            print(f"  aviso: {filename} no existe, se omite", file=sys.stderr)
            continue

        source_html = html_path.read_text(encoding="utf-8", errors="replace")

        # La lección es la slide, no el bloque. Un bloque entero como una sola lección haría
        # que marcarla valiese el 12 % del curso y que el temario mostrase ocho entradas.
        slides = parse_block_slides(source_html, block_id, block["nombre"])

        if not slides:
            raise ValueError(
                f"No se ha encontrado ninguna slide en {filename}. "
                "¿Ha cambiado el marcado que genera content.py?"
            )

        section = SectionManifest(title=f"{block_id} · {block['nombre']}", order=order)
        free = block_id in free_blocks

        for slide in slides:
            section.lessons.append(
                LessonManifest(
                    slug=slugify(f"{block_id}-{slide.anchor}-{slide.title}"),
                    title=slide.title,
                    type=slide.lesson_type,
                    durationMinutes=slide.duration_minutes,
                    # El ancla viaja en el contentRef; el player la aplica al iframe.
                    contentRef=f"{course_slug}/bloques/{filename}#{slide.anchor}",
                    # Los primeros bloques son el lead magnet (C-01).
                    isFreePreview=free,
                    isRequired=slide.is_required,
                    sha256=sha256_of(html_path),
                )
            )

        # Labs en Markdown junto al bloque, si el generador los produjo.
        for lab_path in sorted((source / "labs").glob(f"{block_id}-*.md")) if (source / "labs").exists() else []:
            section.lessons.append(
                LessonManifest(
                    slug=slugify(lab_path.stem),
                    title=lab_path.stem.split("-", 1)[-1].replace("-", " ").capitalize(),
                    type="lab",
                    durationMinutes=45,
                    contentRef=f"{course_slug}/labs/{lab_path.name}",
                    isFreePreview=free,
                    sha256=sha256_of(lab_path),
                )
            )

        manifest.sections.append(section)

    admission = import_admission_quiz(source, course_slug)
    if admission:
        manifest.quizzes.append(admission)

    # Dos sitios posibles para los quizzes de bloque:
    #
    # - El banco compartido del curso original, `assets/js/quizzes-data.js`.
    # - Un fichero por documento, `bloques/<bloque>.data/quizzes.js`, que es lo que produce
    #   `build_course.py` y lo que deja el importador tras repartir el banco compartido.
    #
    # Se miran los dos: si solo se mirara el primero, un curso generado se importaría sin
    # ninguna de sus preguntas y los quizzes del temario quedarían vacíos.
    quiz_files = [source / "assets" / "js" / "quizzes-data.js"]
    quiz_files.extend(sorted((source / "bloques").glob("*.data/quizzes.js")))

    manifest.quizzes.extend(import_block_quizzes(source, course_slug, quiz_files))

    # Los assets compartidos se listan para que el copiado sepa qué llevarse.
    assets_root = source / "assets"
    if assets_root.exists():
        manifest.assets = sorted(
            f"{course_slug}/assets/{path.relative_to(assets_root).as_posix()}"
            for path in assets_root.rglob("*")
            if path.is_file()
        )

    return manifest


def import_precourse(
    source: Path,
    parent_slug: str,
    free_blocks: set[str] | None = None,
) -> CourseManifest | None:
    """El pre-curso, con su primer bloque de muestra y el resto de pago.

    `free_blocks` son los identificadores de bloque (`P0`, `P1`…) que quedan accesibles sin
    plan. Por defecto solo `P0`: el lead magnet es el primer bloque, no el curso entero.

    **Esto solo es seguro porque cada bloque es un fichero.** Un token ampara «su propio
    documento», así que si P0 y P1 vivieran en el mismo HTML, marcar P0 como muestra serviría
    P1 con él. `ensure_access_is_per_file` lo comprueba en vez de confiar en ello.

    Es un producto aparte en el catálogo, pero NO un árbol de ficheros aparte: su HTML pide
    `../assets/css/curso.css`, que en el origen es la carpeta de assets del curso padre.
    Copiarlo a una carpeta propia en la raíz dejaba ese `..` apuntando fuera de todo y las
    lecciones se servían sin estilos y sin JS. Por eso conserva su posición relativa,
    `<curso padre>/pre-curso/`, y solo el catálogo lo trata como curso independiente.
    """
    free_blocks = {"P0"} if free_blocks is None else free_blocks
    precourse = source / "pre-curso"
    if not precourse.exists():
        return None

    # El slug NO cambia aunque cambie el título: es la URL pública del curso, la referencia
    # que llevan los derechos de acceso ya concedidos y la clave con la que el importador
    # reconoce el producto existente en vez de crear uno nuevo. Renombrarlo dejaría la ficha
    # anterior en 404 y duplicaría el producto en el catálogo.
    slug = "pre-curso-agent-engineering"
    # Ruta dentro del volumen de contenido, distinta del slug del producto.
    content_dir = f"{parent_slug}/pre-curso"
    manifest = CourseManifest(
        manifestVersion=MANIFEST_VERSION,
        courseSlug=slug,
        title="Fundamentos de IA Engineer",
        shortDescription="Cuatro mini-bloques con las bases para construir con LLM. El primero, gratis.",
        longDescription=(
            "P0 a P3: fundamentos de LLM, primera llamada a API, tool use y tu primer agent "
            "loop. Pensado para quien no supera el test de nivel."
        ),
        level="intro",
    )

    for order, path in enumerate(sorted(precourse.glob("P*.html"))):
        block_id = path.stem.split("-")[0]
        name = path.stem.split("-", 1)[1].replace("-", " ").capitalize()
        source_html = path.read_text(encoding="utf-8", errors="replace")

        # Mismo marcado que los bloques del curso principal: una lección por slide.
        slides = parse_block_slides(source_html, block_id, name)

        if not slides:
            raise ValueError(f"No se ha encontrado ninguna slide en {path.name}.")

        section = SectionManifest(title=f"{block_id} · {name}", order=order)

        for slide in slides:
            section.lessons.append(
                LessonManifest(
                    slug=slugify(f"{block_id}-{slide.anchor}-{slide.title}"),
                    title=slide.title,
                    type=slide.lesson_type,
                    durationMinutes=slide.duration_minutes,
                    contentRef=f"{content_dir}/{path.name}#{slide.anchor}",
                    isFreePreview=block_id in free_blocks,
                    isRequired=slide.is_required,
                    sha256=sha256_of(path),
                )
            )

        manifest.sections.append(section)

    manifest.quizzes.extend(
        import_block_quizzes(source, slug, [precourse / "quizzes-pre.js"])
    )
    return manifest


def import_single_file_course(
    source: Path,
    course_slug: str,
    title: str,
    free_modules: int,
) -> CourseManifest:
    """Importa un curso que vive en un único HTML (ver html_course.py).

    `free_modules` marca cuántos módulos iniciales quedan como muestra gratuita: son el
    lead magnet que pide C-01.
    """
    source_html = source.read_text(encoding="utf-8", errors="replace")
    panes = parse_panes(source_html)

    if not panes:
        raise ValueError(
            f"No se ha encontrado ninguna lección en {source.name}. "
            "¿Ha cambiado la estructura del HTML?"
        )

    manifest = CourseManifest(
        manifestVersion=MANIFEST_VERSION,
        courseSlug=course_slug,
        title=title,
        shortDescription=(
            "Prompt engineering profesional: de cómo procesa un LLM tu prompt a evals, "
            "guardarraíles y coste."
        ),
        longDescription=(
            "Curso de prompt engineering para quien construye software con LLMs. Cubre "
            "fundamentos, técnicas, reglas antialucinación, guardarraíles, evaluación y "
            "control de coste."
        ),
        level="intermediate",
    )

    # Un documento por lección, no un ancla dentro del fichero entero.
    #
    # Con `index.html#pane-m0-l0`, el documento de CADA lección era el curso completo, y el
    # token de una lección gratuita ampara «su propio documento»: bastaba con abrir la primera
    # lección para recibir las 54, de pago incluidas. Ver `split_single_file_course`, que es
    # quien escribe estos ficheros sobre la copia servida.
    def content_ref(anchor: str) -> str:
        return f"{course_slug}/panes/{anchor}.html"

    for order, (module, module_panes) in enumerate(group_lessons(panes)):
        section = SectionManifest(title=module, order=order)

        for pane in module_panes:
            section.lessons.append(
                LessonManifest(
                    slug=slugify(f"{pane.subtitle}-{pane.title}") or slugify(pane.anchor),
                    title=pane.title,
                    type=pane.lesson_type,
                    durationMinutes=pane.duration_minutes,
                    contentRef=content_ref(pane.anchor),
                    isFreePreview=order < free_modules,
                    isRequired=pane.is_required,
                )
            )

        manifest.sections.append(section)

    manifest.assets = [content_ref(pane.anchor) for pane in panes]
    return manifest


# ── curso de un solo fichero: una lección, un documento ──────────────────────────────────
#
# El curso de prompt engineering vive en UN html con sus 54 lecciones dentro y su propio
# índice lateral. Copiarlo tal cual al volumen servido tenía dos consecuencias, y la segunda
# es la grave:
#
#   1. Dentro del player se veían DOS índices de curso, uno encima del otro: el «Temario» de
#      la plataforma y el `<nav id="rail">` del propio documento.
#   2. **El muro de pago no existía.** El token de cualquiera de las 15 lecciones gratuitas
#      ampara «su propio documento», y su documento era el fichero entero: un alumno sin plan
#      recibía las 54 lecciones del curso de pago en una sola respuesta, y el índice del
#      documento navegaba a ellas sin volver a pedir nada.
#
# Es la misma forma de fallo que el banco de quizzes compartido, y se arregla igual: sobre la
# COPIA SERVIDA, dejando el origen intacto para que siga abriéndose con doble clic.
#
# Cada lección se escribe como documento propio con la cabecera original —los estilos son los
# del curso y no se tocan— y solo su pane. Sin índice: el temario ya lo pone la plataforma, que
# es la que sabe a qué tiene acceso esta persona.

PANE_ID = re.compile(r'<article class="pane" id="(?P<anchor>pane-m\d+-l\d+)"')
HEAD_BLOCK = re.compile(r"<head>(?P<body>.*?)</head>", re.DOTALL)

# El realce de bloques de código del propio documento: botón de copiar y coloreado. Se recorta
# del original entre sus dos marcadores en vez de reescribirse aquí, para que siga siendo el
# del curso y no una copia nuestra que se quede vieja.
CODE_BLOCK_SCRIPT = re.compile(
    r"/\* -+ bloques de código -+ \*/(?P<body>.*?)(?=/\* -+ )",
    re.DOTALL,
)


# El documento original reserva a la izquierda el ancho de su índice (`#main{margin-left:
# var(--rail)}`). Sin índice, ese hueco queda como una franja vacía de 300 px justo donde el
# player ya pinta su temario. Se anula la variable en vez de tocar la regla: así el ajuste
# desaparece solo el día que el original deje de reservar ese espacio.
EMBEDDED_STYLE = (
    "<style>/* Añadido por el importador: sin índice propio, no hay hueco que reservar. */\n"
    ":root{--rail:0px}</style>"
)

# ── unificación visual del curso de fichero único ────────────────────────────────────────
#
# El documento original trae su identidad completa en un <style> propio: otras fuentes
# (Syne, DM Sans), paleta neón y un único tema oscuro. Dentro de la academia eso rompe el
# look & feel: los demás cursos comparten curso.css (Inter, marino/dorado, claro y oscuro).
#
# En vez de reescribir su CSS —mantener ese fork a mano no escala—, el importador deja el
# <style> original y carga DESPUÉS una hoja compat que redefine sus variables con los tokens
# de curso.css y añade el tema claro. La hoja vive versionada en tools/assets, junto al
# curso.css maestro, y se copia a assets/ del curso porque el token de contenido solo
# alcanza los ficheros de SU curso.

LEGACY_FONTS_LINK = re.compile(r'<link href="https://fonts\.googleapis\.com/css2\?[^"]*"[^>]*>')

UNIFIED_FONTS_LINK = (
    '<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800'
    '&family=JetBrains+Mono:wght@400;500;700&display=swap" rel="stylesheet">'
)

COMPAT_STYLESHEET_LINK = '\n<link rel="stylesheet" href="../assets/css/curso-compat.css">'

# Cada módulo del original coloreaba su acento en línea (style="--acc:#00e5ff"). El estilo en
# línea gana a cualquier hoja, así que con él puesto la compat no puede unificar el acento.
PANE_INLINE_ACCENT = re.compile(r'(<article\b[^>]*?)\s+style="--acc:[^"]*"')


def split_single_file_course(source_html: str, target: Path) -> list[str]:
    """Escribe un documento por lección en `panes/` y devuelve sus nombres."""
    head = HEAD_BLOCK.search(source_html)
    if not head:
        raise ValueError("El curso de un solo fichero no tiene <head>. ¿Ha cambiado el formato?")

    enhancement = CODE_BLOCK_SCRIPT.search(source_html)
    if not enhancement:
        raise ValueError(
            "No se encuentra el bloque de realce de código del documento. Se aborta en vez de "
            "servir las lecciones sin botón de copiar: perderlo en silencio da un curso que "
            "parece correcto y no lo es."
        )

    panes = list(PANE.finditer(source_html))
    if not panes:
        raise ValueError("No se ha encontrado ninguna lección que separar.")

    directory = target / "panes"
    directory.mkdir(parents=True, exist_ok=True)

    # Mismas fuentes que curso.css: las del original (Syne, DM Sans) ya no las usa nadie en
    # cuanto la hoja compat pisa las font-family, y dejarlas sería descargarlas para nada.
    head_body = LEGACY_FONTS_LINK.sub(UNIFIED_FONTS_LINK, head.group("body"), count=1)

    written: list[str] = []

    for match in panes:
        anchor = PANE_ID.search(match.group(0)).group("anchor")

        # `show` va puesto en el marcado: sin el router del documento, `.pane` está en
        # `display:none` y la lección saldría en blanco.
        pane_html = match.group(0).replace(
            f'<article class="pane" id="{anchor}"',
            f'<article class="pane show" id="{anchor}"',
            1,
        )

        # Un solo acento para todo el curso: el de la academia, que pone la hoja compat.
        pane_html = PANE_INLINE_ACCENT.sub(r"\1", pane_html, count=1)

        document = (
            "<!DOCTYPE html>\n"
            '<html lang="es">\n'
            # Orden deliberado: estilo original → compat (lo pisa) → script de tema, que
            # pone data-theme antes de pintar para no dar un fogonazo del tema contrario.
            f"<head>{head_body}{EMBEDDED_STYLE}{COMPAT_STYLESHEET_LINK}{THEME_HEAD_SCRIPT}</head>\n"
            "<body>\n"
            '<main id="main">\n'
            f"{pane_html}\n"
            "</main>\n"
            f"<script>{enhancement.group('body')}</script>\n"
            # El puente del player: estos panes no cargan viewer.js, así que sin él un
            # cambio de tema con la lección abierta no llegaría hasta el documento.
            f"<script>{THEME_BRIDGE}</script>\n"
            "</body>\n"
            "</html>\n"
        )

        name = f"{anchor}.html"
        (directory / name).write_text(document, encoding="utf-8")
        written.append(name)

    return written


def ensure_access_is_per_file(manifest: CourseManifest) -> None:
    """Aborta si un mismo fichero sirve lecciones gratuitas y de pago.

    Un token de contenido ampara «su propio documento». Si ese documento contiene además
    lecciones de pago, el muro de pago no existe: basta con abrir la lección de muestra para
    recibirlas todas. Pasó con el curso de prompt engineering, que vivía entero en un HTML.

    Se comprueba al importar y no al servir porque al servir ya es tarde: la respuesta salió
    con un 200 y nadie se entera. Marcar un bloque como muestra es una decisión de negocio de
    una línea, y esta es la que dice si el contenido soporta esa decisión.
    """
    por_fichero: dict[str, dict[str, list[str]]] = {}

    for section in manifest.sections:
        for lesson in section.lessons:
            if not lesson.contentRef:
                continue

            fichero = lesson.contentRef.split("#", 1)[0]
            destino = por_fichero.setdefault(fichero, {"gratis": [], "pago": []})
            destino["gratis" if lesson.isFreePreview else "pago"].append(lesson.title)

    mezclados = {f: v for f, v in por_fichero.items() if v["gratis"] and v["pago"]}

    if mezclados:
        detalle = "; ".join(
            f"{f} ({len(v['gratis'])} de muestra y {len(v['pago'])} de pago, p. ej. «{v['pago'][0]}»)"
            for f, v in mezclados.items()
        )
        raise ValueError(
            "Hay ficheros que mezclan lecciones de muestra y de pago, así que el muro de pago "
            f"no se sostiene: {detalle}. Separa esas lecciones en ficheros distintos antes de "
            "marcar unas como muestra."
        )


def ensure_not_self_copy(source: Path, target: Path) -> None:
    """Aborta si el origen es la carpeta a la que se va a copiar, o está dentro.

    `shutil.copytree(x, x, dirs_exist_ok=True)` abre cada fichero para escritura y lo copia
    sobre sí mismo: los deja a CERO bytes. Y no falla. El curso se sigue importando, el manifest
    sale con sus 213 lecciones y sus duraciones —porque se leyeron antes de copiar— y la
    plataforma sirve documentos vacíos con un 200. Se ve al abrir una lección, no al importar.

    Pasa con `--source content/dist/<slug> --copy-to content/dist`, que es lo que uno escribe
    sin pensar cuando el curso ya está generado en el volumen servido.
    """
    origen = source.resolve()
    destino = target.resolve()

    if origen == destino or destino.is_relative_to(origen):
        raise ValueError(
            f"El origen y el destino son la misma carpeta ({origen}). Copiar sobre sí misma "
            "dejaría todos los ficheros a cero bytes sin dar error. Importa desde la carpeta "
            "generada (content/generado/<slug>), no desde el volumen que ya se sirve."
        )


def copy_single_file(source: Path, destination: Path, course_slug: str) -> int:
    target = destination / course_slug
    ensure_not_self_copy(source.parent, target)
    target.mkdir(parents=True, exist_ok=True)

    # El fichero completo NO se copia: es justo lo que hacía que un token de lección gratuita
    # sirviera el curso de pago entero.
    written = split_single_file_course(
        source.read_text(encoding="utf-8", errors="replace"), target
    )

    # La hoja compat que referencian los panes. Del directorio versionado del repo, no de
    # otra copia servida: si no está, mejor fallar aquí que servir lecciones sin re-skin.
    compat = Path(__file__).resolve().parent / "assets" / "css" / "curso-compat.css"
    css_dir = target / "assets" / "css"
    css_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(compat, css_dir / compat.name)

    # Y si quedaba de una importación anterior, se borra: seguiría siendo servible.
    stale = target / "index.html"
    if stale.exists():
        stale.unlink()

    return len(written)


def copy_content(source: Path, destination: Path, course_slug: str) -> int:
    """Copia el contenido al volumen que sirve la API, conservando rutas relativas."""
    target = destination / course_slug
    ensure_not_self_copy(source, target)
    target.mkdir(parents=True, exist_ok=True)
    copied = 0

    for relative in ("bloques", "assets", "labs", "pre-curso"):
        origin = source / relative
        if not origin.exists():
            continue

        # Los .py del origen son las herramientas que generaron el HTML, no contenido del
        # curso. Publicarlos los pondría al alcance de cualquiera con un token válido.
        shutil.copytree(
            origin,
            target / relative,
            dirs_exist_ok=True,
            ignore=shutil.ignore_patterns("*.py", "__pycache__"),
        )
        copied += sum(1 for path in origin.rglob("*") if path.is_file() and path.suffix != ".py")

    for name in ("index.html", "nivel.html"):
        candidate = source / name
        if candidate.exists():
            shutil.copy2(candidate, target / name)
            copied += 1

    # Sobre la copia, nunca sobre el origen.
    copied += split_quiz_bundles(target)
    normalise_theme(target)

    return copied


# ── tema del visor ────────────────────────────────────────────────────────────────────────
#
# El HTML original trae su propio selector de tema con los nombres de otra marca ("Occident
# (claro)", "Occident (oscuro)"). Dentro del player eso es doblemente malo: enseña una marca
# ajena a un alumno de esta academia, y ofrece un tema propio que no tiene por qué coincidir
# con el que la persona ya eligió en la plataforma.
#
# La transformación sobre la copia servida hace tres cosas:
#   1. Renombra los temas a `light` y `dark`, que es lo que dice el player.
#   2. Quita el selector: el tema se hereda, no se elige dos veces.
#   3. Deja el documento escuchando al player, y leyendo `?theme=` para el primer pintado.
#
# Es la segunda excepción a "el contenido se importa, no se reescribe" (ADR-008), y por la
# misma razón que la primera: el original no puede saber que va dentro de esta plataforma.

# Marca que dice si el puente ya está puesto. Es una cadena del propio puente, no un comentario
# aparte: así no puede quedarse el comentario sin el código ni al revés.
THEME_BRIDGE_MARKER = 'data.type === "inkoova:theme"'

THEME_BRIDGE = """
/* ── tema heredado de la academia ──────────────────────────────────────
   Añadido por el importador. El documento NO elige tema: lo recibe del
   player, que es quien sabe lo que ha elegido la persona. Sin esto, el
   visor tendría un selector propio con los nombres de otra marca. */
(function () {
  function apply(theme) {
    document.documentElement.setAttribute("data-theme", theme === "dark" ? "dark" : "light");
  }

  // El player manda el tema al cargar y cada vez que cambia.
  window.addEventListener("message", function (event) {
    var data = event.data;
    if (data && data.type === "inkoova:theme" && typeof data.theme === "string") {
      apply(data.theme);
    }
  });

  // Y se avisa de que ya se puede recibir: si el documento tarda en cargar, el primer
  // mensaje del player se habría perdido contra una ventana que aún no escuchaba.
  try {
    parent.postMessage({ type: "inkoova:theme-ready", version: 1 }, "*");
  } catch (e) {}
})();
"""

# Script que corre ANTES de pintar. Va en el <head> porque aplicar el tema desde el bundle
# deja un fogonazo del tema contrario en cada lección.
THEME_HEAD_SCRIPT = """<script>
        // Tema, antes de pintar. Orden: lo que manda el player en la URL, y si no viene,
        // la preferencia del sistema. No se lee localStorage: el tema es de la academia,
        // no del documento, y una copia guardada aquí se quedaría vieja.
        (function () {
            var q = new URLSearchParams(location.search).get("theme");
            var dark = q ? q === "dark"
                         : window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
            document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
        })();
    </script>"""

# El script original: lee localStorage y cae en "occident". Se sustituye entero.
OLD_HEAD_SCRIPT = re.compile(
    r"<script>\s*//[^\n]*\n\s*\(function\(\)\s*\{\s*var t = localStorage\.getItem\([^)]*\)[^}]*\}\)\(\);\s*</script>",
    re.MULTILINE,
)


def normalise_theme(target: Path) -> None:
    """Deja el visor copiado heredando el tema de la academia, sin selector propio."""
    _rename_theme_tokens(target / "assets" / "css" / "curso.css")
    _strip_theme_switcher(target / "assets" / "js" / "viewer.js")

    for html in target.rglob("*.html"):
        _rewrite_head_script(html)


def _rename_theme_tokens(css: Path) -> None:
    if not css.exists():
        return

    text = css.read_text(encoding="utf-8")

    # El oscuro primero: "occident" es prefijo de "occident-dark" y sustituirlo antes
    # convertiría [data-theme="occident-dark"] en [data-theme="light-dark"].
    text = text.replace('[data-theme="occident-dark"]', '[data-theme="dark"]')
    text = text.replace('[data-theme="occident"]', '[data-theme="light"]')

    # Las muestras de color del selector. El selector ya no se pinta, así que estas reglas son
    # código muerto, pero llevan el nombre de otra marca dentro de un fichero que servimos.
    text = re.sub(r"^\.theme-swatch\.[a-z-]+\s*\{[^\n]*\n", "", text, flags=re.MULTILINE)

    # Y los comentarios de cabecera, que también la nombran.
    text = text.replace(
        "Sistema de temas: Occident (default), Occident Dark, Inkoova",
        "Sistema de temas: claro y oscuro, heredados de la academia",
    )
    text = text.replace("Tokens de tema — default = Occident", "Tokens de tema — default = claro")

    # El tercer tema desaparece: era una elección más del selector que ya no existe, y sus
    # colores no son los de esta academia.
    text = re.sub(r'\[data-theme="inkoova"\]\s*\{[^}]*\}\s*', "", text)

    css.write_text(text, encoding="utf-8")


def _strip_theme_switcher(viewer: Path) -> None:
    if not viewer.exists():
        return

    text = viewer.read_text(encoding="utf-8")

    # El ancla puede llegar sucia (#slide-0?theme=dark: una redirección vieja pegaba la query
    # detrás), y querySelector muere con ese selector. Se busca por id sobre el ancla saneada.
    # Va antes de la guarda del puente y es idempotente por sí mismo: si la cadena original ya
    # no está, no hay nada que sustituir.
    text = text.replace(
        "const target = document.querySelector(window.location.hash);",
        'const anchor = window.location.hash.slice(1).split("?")[0];\n'
        "      const target = anchor ? document.getElementById(anchor) : null;",
    )

    # Idempotente: el generador de los cursos escritos copia estos assets desde una copia YA
    # servida, así que este mismo fichero pasa por aquí más de una vez. Sin la guarda, cada
    # reimportación añadiría otro puente y el documento acabaría con cinco listeners iguales.
    if THEME_BRIDGE_MARKER in text:
        viewer.write_text(text, encoding="utf-8")
        return

    # Se desactiva la llamada en vez de borrar el método: borrarlo obligaría a acertar con los
    # límites de una función en un fichero ajeno, y basta con que no se llame.
    text = text.replace("this.setupThemeSwitcher();", "// selector propio retirado: el tema lo hereda de la academia")

    # applyStoredTheme leía localStorage y caía en "occident". El tema ya lo puso el script
    # del <head>; aquí sobra y pisaría el que mandó el player.
    text = text.replace("this.applyStoredTheme();", "// el tema ya viene puesto desde el <head>")

    # La lista de temas del selector: código muerto en cuanto el selector no se pinta, pero con
    # el nombre de otra marca dentro. Se vacía en vez de borrarse para no tener que adivinar los
    # límites del literal en un fichero ajeno.
    text = re.sub(
        r"themes:\s*\[[^\]]*\]",
        "themes: [] /* el tema lo elige la academia, no el documento */",
        text,
        count=1,
    )

    # Los dos respaldos que caían en el tema de la otra marca.
    text = text.replace('|| "occident"', '|| "light"')

    text += THEME_BRIDGE

    viewer.write_text(text, encoding="utf-8")


def _rewrite_head_script(html: Path) -> None:
    text = html.read_text(encoding="utf-8")

    if 'localStorage.getItem("agent-engineering-v3-theme")' not in text:
        return

    replaced = OLD_HEAD_SCRIPT.sub(THEME_HEAD_SCRIPT, text, count=1)

    if replaced != text:
        html.write_text(replaced, encoding="utf-8")



# Carpeta que acompaña a cada documento con los datos que solo son suyos. El nombre sale del
# propio documento (B3-multi-agente.html -> B3-multi-agente.data/) y eso es lo que permite a la
# API atar el permiso al documento sin saber nada del contenido.
SIDECAR_SUFFIX = ".data"
SIDECAR_FILE = "quizzes.js"

# Bancos compartidos de la copia servida, con la carpeta de los documentos que los cargan.
SHARED_QUIZ_BUNDLES = (
    ("assets/js/quizzes-data.js", "bloques"),
    ("pre-curso/quizzes-pre.js", "pre-curso"),
)

# Lo que cada documento declara que necesita: <div class="quiz-container" data-quiz="B3">.
DATA_REQUEST = re.compile(r'data-(quiz|exercise)="([^"]+)"')

# Los dos bancos que consume el HTML del curso. EXERCISES no existe en todos.
QUIZ_BANKS = ("QUIZZES", "EXERCISES")


def split_quiz_bundles(target: Path) -> int:
    """Reparte los bancos de quizzes compartidos, uno por documento.

    El banco original trae los bloques juntos —quizzes y ejercicios con su `model_answer`— y
    cada bloque lo carga entero. Al ser un fichero compartido del curso, el token de una
    lección gratuita lo alcanzaba: las preguntas y las respuestas de los bloques de pago
    quedaban a la vista en la pestaña de red.

    Cada trozo va a `<documento>.data/quizzes.js`, una carpeta atada por nombre a su
    documento. La API concede acceso a esa carpeta y a nada más, así que un token de B0 no
    alcanza los datos de B3 aunque sean del mismo curso.

    Es la única vez que se reescribe contenido, y solo sobre la copia servida: el origen sigue
    abriéndose con doble clic. Se hace aquí y no en la API porque el corte es estático, depende
    del bloque y no de quién mire.
    """
    return sum(_split_bundle(target, bundle, documents) for bundle, documents in SHARED_QUIZ_BUNDLES)


def _split_bundle(target: Path, bundle_ref: str, documents_ref: str) -> int:
    bundle = target / bundle_ref
    documents_dir = target / documents_ref

    if not bundle.exists() or not documents_dir.is_dir():
        return 0

    documents = sorted(documents_dir.glob("*.html"))

    # Cómo lo referencia el HTML: relativo desde la carpeta del documento.
    reference = os.path.relpath(bundle, documents_dir).replace("\\", "/")

    # Solo reciben datos los documentos que de verdad cargan el banco. En la misma carpeta hay
    # HTML que no lo hacen —el índice del pre-curso, por ejemplo— y darles una carpeta de datos
    # dejaría basura sin dueño.
    loaders = [path for path in documents if reference in path.read_text(encoding="utf-8")]

    if not loaders:
        raise ValueError(f"{bundle_ref} no lo carga ningún documento de {documents_ref}.")

    source = bundle.read_text(encoding="utf-8")
    banks: dict[str, dict[str, Any]] = {}

    for bank in QUIZ_BANKS:
        try:
            banks[bank] = load_assignment(source, bank)
        except ValueError:
            continue

    written = 0
    claimed: set[tuple[str, str]] = set()

    for document in loaders:
        html = document.read_text(encoding="utf-8")
        share: dict[str, dict[str, Any]] = {}

        # El reparto lo manda el documento: se le da lo que sus `data-quiz` y `data-exercise`
        # piden, y nada más. Deducirlo del nombre de la clave sería adivinar, y adivinar mal
        # aquí no se nota: el quiz simplemente no aparece.
        for kind, name in DATA_REQUEST.findall(html):
            bank = "QUIZZES" if kind == "quiz" else "EXERCISES"
            key = _match_key(banks.get(bank, {}), name)

            if key is None:
                raise ValueError(
                    f"{document.name} pide {kind} {name!r} y {bundle_ref} no lo tiene."
                )

            # Se guarda con el nombre que pide el documento, no con el del banco. El pre-curso
            # los tiene desincronizados en origen (pide "P0", el banco dice "PRE-P0") y por eso
            # sus mini-quiz nunca llegaban a renderizarse.
            share.setdefault(bank, {})[name] = banks[bank][key]
            claimed.add((bank, key))

        sidecar = f"{document.stem}{SIDECAR_SUFFIX}"
        (documents_dir / sidecar).mkdir(exist_ok=True)

        chunks = [f"// Datos de {document.name}, separados del banco común por el importador."]

        for bank, entries in share.items():
            # Object.assign y no una asignación directa: cada documento carga solo su trozo y
            # ninguno debe pisar lo que haya dejado otro. Es el patrón que ya usaba el
            # pre-curso, así que el código de la página no cambia.
            body = json.dumps(entries, ensure_ascii=False, indent=2)
            chunks.append(f"window.{bank} = Object.assign(window.{bank} || {{}}, {body});")

        (documents_dir / sidecar / SIDECAR_FILE).write_text(
            "\n\n".join(chunks) + "\n", encoding="utf-8"
        )
        written += 1

        document.write_text(
            html.replace(reference, f"{sidecar}/{SIDECAR_FILE}"), encoding="utf-8"
        )

    orphans = sorted(
        f"{bank}.{key}" for bank, entries in banks.items()
        for key in entries if (bank, key) not in claimed
    )

    if orphans:
        # Material que nadie carga. Antes se perdía en silencio y el curso salía incompleto sin
        # que nada fallara.
        raise ValueError(f"{bundle_ref} tiene datos que ningún documento pide: {', '.join(orphans)}.")

    bundle.unlink()

    remaining = [p.name for p in documents if reference in p.read_text(encoding="utf-8")]
    if remaining:
        raise ValueError(f"Estos documentos siguen apuntando al banco entero: {', '.join(remaining)}.")

    return written


def _match_key(entries: dict[str, Any], name: str) -> str | None:
    """Busca en el banco la clave que corresponde a lo que pide el documento.

    Exacta primero. Si no la hay, se acepta una única clave que termine en `-<nombre>`: así
    `PRE-P0` responde a un `data-quiz="P0"`. Si hubiera más de una candidata no se elige: dos
    quizzes distintos con el mismo final es ambigüedad real, no algo que deba resolverse solo.
    """
    if name in entries:
        return name

    candidates = [key for key in entries if key.endswith(f"-{name}")]

    return candidates[0] if len(candidates) == 1 else None


def write_manifest(manifest: CourseManifest, out: Path) -> Path:
    # Antes de escribir nada: un manifest que mezcla acceso dentro de un fichero describe un
    # curso sin muro de pago, y una vez escrito se aplica sin que nadie lo mire.
    ensure_access_is_per_file(manifest)

    path = out / f"{manifest.courseSlug}.manifest.json"
    path.write_text(
        json.dumps(asdict(manifest), ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    lessons = sum(len(section.lessons) for section in manifest.sections)
    minutes = sum(lesson.durationMinutes for s in manifest.sections for lesson in s.lessons)
    questions = sum(len(quiz.questions) for quiz in manifest.quizzes)
    free = sum(1 for s in manifest.sections for lesson in s.lessons if lesson.isFreePreview)

    print(
        f"{manifest.courseSlug}: {len(manifest.sections)} secciones, {lessons} lecciones "
        f"({free} gratis), {minutes} min (~{round(minutes / 60)} h), "
        f"{len(manifest.quizzes)} quizzes ({questions} preguntas) -> {path}"
    )

    return path


def main() -> int:
    parser = argparse.ArgumentParser(description="Importa el contenido del curso a un manifest.")
    parser.add_argument("--source", required=True, type=Path, help="Carpeta del curso")
    parser.add_argument("--out", required=True, type=Path, help="Carpeta donde escribir el manifest")
    parser.add_argument("--slug", default="agent-engineering-v3", help="Slug del curso")
    parser.add_argument(
        "--free-blocks",
        default="B0",
        help="Bloques accesibles sin suscripción, separados por coma (lead magnet)",
    )
    parser.add_argument(
        "--free-precourse-blocks",
        default="P0",
        help=(
            "Bloques del pre-curso accesibles sin suscripción, separados por coma. "
            "Aparte de --free-blocks porque son productos distintos y se venden distinto."
        ),
    )
    parser.add_argument("--copy-to", type=Path, help="Copia también el contenido a este volumen")
    parser.add_argument("--short-description", help="Descripción corta del curso")
    parser.add_argument("--long-description", help="Descripción larga del curso")
    parser.add_argument(
        "--level",
        default="advanced",
        choices=["intro", "intermediate", "advanced"],
        help="Nivel del curso",
    )
    parser.add_argument(
        "--no-precourse",
        action="store_true",
        help="No busca pre-curso dentro del origen. Los cursos generados no lo tienen.",
    )
    parser.add_argument(
        "--title",
        help="Título del curso. Solo se usa al importar un curso de fichero único.",
    )
    parser.add_argument(
        "--free-modules",
        type=int,
        default=2,
        help="Módulos iniciales gratuitos en un curso de fichero único (lead magnet)",
    )
    args = parser.parse_args()

    if not args.source.exists():
        print(f"error: no existe {args.source}", file=sys.stderr)
        return 2

    args.out.mkdir(parents=True, exist_ok=True)

    # Un curso puede venir como carpeta (curso-autodidacta) o como un único HTML
    # (curso-prompt-engineering.html). Se distinguen por lo que hay en la ruta, no por una
    # bandera: equivocarse de bandera daría un manifest vacío en silencio.
    if args.source.is_file():
        manifest = import_single_file_course(
            args.source,
            args.slug,
            args.title or args.source.stem.replace("-", " ").capitalize(),
            args.free_modules,
        )

        write_manifest(manifest, args.out)

        if args.copy_to:
            copied = copy_single_file(args.source, args.copy_to, args.slug)
            print(f"copiados {copied} ficheros a {args.copy_to}")

        return 0

    free_blocks = {b.strip() for b in args.free_blocks.split(",") if b.strip()}

    manifests = [
        import_course(
            args.source,
            args.slug,
            free_blocks,
            title=args.title,
            short_description=args.short_description,
            long_description=args.long_description,
            level=args.level,
        )
    ]

    free_precourse = {b.strip() for b in args.free_precourse_blocks.split(",") if b.strip()}

    precourse = (
        None
        if args.no_precourse
        else import_precourse(args.source, args.slug, free_precourse)
    )
    if precourse:
        manifests.append(precourse)

    for manifest in manifests:
        write_manifest(manifest, args.out)

    if args.copy_to:
        # El pre-curso viaja dentro del árbol del curso padre, así que copy_content ya lo
        # trae: no hay una segunda copia que hacer.
        copied = copy_content(args.source, args.copy_to, args.slug)
        print(f"copiados {copied} ficheros a {args.copy_to}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
