"""Extrae lecciones de cursos que viven dentro de ficheros HTML.

Los dos cursos existentes guardan sus lecciones dentro de un HTML con el visor incrustado,
pero con marcado distinto:

* **curso-autodidacta**: un fichero por bloque, y dentro
  `<section id="slide-N" data-slide-idx="N">` por cada slide, con su título, su duración
  declarada y, cuando toca, `data-checkpoint`, `data-exercise-slide` o `data-quiz-slide`.
* **curso-prompt-engineering**: un único fichero con
  `<article class="pane" id="pane-mM-lL">` por lección, agrupadas por módulo.

En ambos casos **la lección es la slide, no el fichero**. Importar un bloque entero como una
sola lección haría que marcar una lección valiese el 12 % del curso y que el temario mostrase
ocho entradas en vez de los ocho bloques con sus slides dentro.

El HTML no se trocea: su CSS y su JS son inline y partirlo los rompería. Cada lección apunta
al mismo fichero con un ancla, y el player la aplica al `src` del iframe.
"""

from __future__ import annotations

import html
import re
from dataclasses import dataclass

TAGS = re.compile(r"<[^>]+>")


@dataclass
class HtmlLesson:
    """Una lección extraída de un HTML, con el ancla que la localiza dentro del fichero."""

    anchor: str
    group: str
    title: str
    subtitle: str
    duration_minutes: int
    lesson_type: str
    is_required: bool


def strip_tags(fragment: str) -> str:
    return re.sub(r"\s+", " ", html.unescape(TAGS.sub(" ", fragment))).strip()


# ── curso-autodidacta: un fichero por bloque, secciones por slide ────────────────────────

SLIDE = re.compile(
    r'<section id="(?P<anchor>slide-\d+)"(?P<attrs>[^>]*)>'
    r'(?P<body>.*?)(?=<section id="slide-\d+"|</main>|</body>|\Z)',
    re.DOTALL,
)

SLIDE_TITLE = re.compile(r'<h2 class="slide-title">(?P<text>.*?)</h2>', re.DOTALL)
SLIDE_SUBTITLE = re.compile(r'<div class="slide-subtitle">(?P<text>.*?)</div>', re.DOTALL)
SLIDE_TIME = re.compile(r'class="time-est">[^<]*?~\s*(?P<minutes>\d+)\s*min')

# Una slide sin minutos declarados: checkpoint, ejercicio o quiz. Duran lo que el alumno
# tarde en pensar, así que se les asigna un valor conservador en lugar de estimarlo.
DEFAULT_MINUTES_BY_TYPE = {
    "quiz": 5,
    "lab": 10,
    "slides": 2,
}


def parse_block_slides(source_html: str, block_id: str, block_name: str) -> list[HtmlLesson]:
    lessons: list[HtmlLesson] = []

    for match in SLIDE.finditer(source_html):
        body = match.group("body")
        attrs = match.group("attrs")

        title_match = SLIDE_TITLE.search(body)
        if not title_match:
            # Una sección sin título no es una lección: portada, separador o pie del visor.
            continue

        subtitle_match = SLIDE_SUBTITLE.search(body)
        time_match = SLIDE_TIME.search(body)

        # El tipo va en atributos del <section>, no en el texto: es lo que el visor usa.
        if "data-quiz-slide" in attrs:
            lesson_type = "quiz"
            required = True
        elif "data-exercise-slide" in attrs:
            lesson_type = "lab"
            required = True
        elif "data-checkpoint" in attrs:
            lesson_type = "slides"
            # Un checkpoint es una pausa para pensar: no produce evidencia y no debe
            # bloquear la emisión del certificado.
            required = False
        else:
            lesson_type = "slides"
            required = True

        duration = (
            int(time_match.group("minutes"))
            if time_match
            else DEFAULT_MINUTES_BY_TYPE[lesson_type]
        )

        lessons.append(
            HtmlLesson(
                anchor=match.group("anchor"),
                group=f"{block_id} · {block_name}",
                title=strip_tags(title_match.group("text")),
                subtitle=strip_tags(subtitle_match.group("text")) if subtitle_match else "",
                duration_minutes=duration,
                lesson_type=lesson_type,
                is_required=required,
            )
        )

    return lessons


# ── curso-prompt-engineering: un fichero, panes por lección ──────────────────────────────

PANE = re.compile(
    r'<article class="pane" id="(?P<anchor>pane-m\d+-l\d+)".*?'
    r'(?=<article class="pane"|</main>|\Z)',
    re.DOTALL,
)

KICKER = re.compile(r'<p class="kicker">(?P<text>.*?)</p>', re.DOTALL)
PANE_TITLE = re.compile(r'<h1 class="l-title">(?P<text>.*?)</h1>', re.DOTALL)
LESSON_NUMBER = re.compile(r'<span class="l-num">(?P<text>.*?)</span>', re.DOTALL)
PANE_DURATION = re.compile(r'<span class="chip chip-dur">\s*(?P<minutes>\d+)\s*min', re.IGNORECASE)
PANE_KIND = re.compile(r'<span class="chip chip-(?P<kind>[a-záéíóúñ]+)"', re.IGNORECASE)

# Los chips describen el tipo en el HTML original; lo que no encaja se queda en slides, que
# es el visor genérico.
KIND_TO_LESSON_TYPE = {
    "lectura": "slides",
    "práctica": "lab",
    "practica": "lab",
    "proyecto": "lab",
    "referencia": "slides",
    "recurso": "slides",
    "cierre": "slides",
}


def parse_panes(source_html: str) -> list[HtmlLesson]:
    lessons: list[HtmlLesson] = []

    for match in PANE.finditer(source_html):
        block = match.group(0)

        title_match = PANE_TITLE.search(block)
        if not title_match:
            continue

        title_html = title_match.group("text")
        number_match = LESSON_NUMBER.search(title_html)
        number = strip_tags(number_match.group("text")) if number_match else ""

        # El número va dentro del h1: se quita para que no salga dos veces en el título.
        title = strip_tags(LESSON_NUMBER.sub("", title_html))

        kicker_match = KICKER.search(block)
        duration_match = PANE_DURATION.search(block)
        kind_match = PANE_KIND.search(block)

        kind = kind_match.group("kind").lower() if kind_match else "lectura"
        lesson_type = KIND_TO_LESSON_TYPE.get(kind, "slides")

        lessons.append(
            HtmlLesson(
                anchor=match.group("anchor"),
                group=strip_tags(kicker_match.group("text")) if kicker_match else "Curso",
                title=title,
                subtitle=number,
                duration_minutes=(
                    int(duration_match.group("minutes"))
                    if duration_match
                    else DEFAULT_MINUTES_BY_TYPE[lesson_type]
                ),
                lesson_type=lesson_type,
                is_required=True,
            )
        )

    return lessons


def group_lessons(lessons: list[HtmlLesson]) -> list[tuple[str, list[HtmlLesson]]]:
    """Agrupa conservando el orden de aparición, que es el orden pedagógico."""
    groups: list[tuple[str, list[HtmlLesson]]] = []

    for lesson in lessons:
        if groups and groups[-1][0] == lesson.group:
            groups[-1][1].append(lesson)
        else:
            groups.append((lesson.group, [lesson]))

    return groups
