#!/usr/bin/env python3
"""Convierte un curso escrito en Markdown en el mismo HTML que ya sirve la academia.

Los cursos nuevos (C-01 a C-20) se escriben en `content/cursos/<curso>/<bloque>.md`. Ese
Markdown no llega a ninguna parte por sí solo: el importador lee decks HTML con
`<section id="slide-N">`, que es lo que produce `content.py` para el curso original. Sin este
paso, escribir la prosa dejaba el curso fuera de la academia.

Genera exactamente el mismo contrato que el curso original —mismas clases, mismos marcadores,
mismos scripts compartidos— para que el player, el progreso y los quizzes funcionen igual sin
tocar la plataforma.

Formato de entrada:

    # C-02 · B1 · Título del bloque
    > Curso: `agentes-en-produccion` · bloque `B1`

    ## Objetivo
    Una frase.

    ## Guion de slides

    ### 1. Título de la slide
    Prosa en Markdown.

    ### 2. Otro título {checkpoint}
    Prosa.

    ### 3. Ejercicio práctico 1 {ejercicio:B1-ej1}
    Enunciado.

    ### 4. Mini-quiz {quiz:B1}
    Introducción al quiz.

Uso:
    python content/tools/build_course.py --course agentes-en-produccion --out <carpeta>
"""

from __future__ import annotations

import argparse
import json
import html
import re
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

# Ritmo de lectura para prosa técnica densa, el mismo que usa el importador para estimar.
WORDS_PER_MINUTE = 130
MINIMUM_SLIDE_MINUTES = 2

# Lo que admite la columna `quiz_question.category` de la plataforma.
MAX_CATEGORY = 80

# `### 3. Título {marcador}` — el número ordena y el marcador es opcional.
HEADING = re.compile(r"^###\s+(?P<order>\d+)\.\s+(?P<title>.+?)\s*$", re.MULTILINE)

# {checkpoint} · {ejercicio:B1-ej1} · {quiz:B1}
MARKER = re.compile(r"\{(?P<kind>checkpoint|ejercicio|quiz)(?::(?P<id>[^}]+))?\}\s*$")

# La prosa pendiente se marca así en los esqueletos. Un bloque con esto dentro no se publica.
PENDING = re.compile(r"`?TODO\(prosa\)`?")

# Elemento de lista numerada: "1. texto".
ORDERED_ITEM = re.compile(r"^\d+\.\s+")

# Bloque de código completo, con su lenguaje opcional. Se extrae entero antes de nada más.
FENCE = re.compile(r"```(?P<lang>[^\n`]*)\n(?P<code>.*?)```", re.DOTALL)
PLACEHOLDER = re.compile(r"\x00FENCE(\d+)\x00")

# Las preguntas del mini-quiz, tal y como se escriben en el Markdown del bloque.
QUESTION = re.compile(
    r"^\d+\.\s+\*\*Tema:\*\*\s*(?P<tema>.+?)\n(?P<body>.*?)(?=^\d+\.\s+\*\*Tema:\*\*|\Z)",
    re.MULTILINE | re.DOTALL,
)
STATEMENT = re.compile(r"\*\*Enunciado:\*\*\s*(?P<text>.+?)\s*$", re.MULTILINE)
OPTION = re.compile(r"^\s*-\s*[a-d]\)\s*(?P<text>.+?)\s*$", re.MULTILINE)
EXPLANATION = re.compile(r"\*\*Explicación:\*\*\s*(?P<text>.+?)\s*$", re.MULTILINE | re.DOTALL)


@dataclass
class Slide:
    order: int
    title: str
    subtitle: str
    body_md: str
    kind: str = "slides"
    marker_id: str | None = None

    @property
    def minutes(self) -> int:
        words = len(strip_markdown(self.body_md).split())
        if self.kind == "quiz":
            return 5
        if self.kind == "lab":
            return 10
        return max(MINIMUM_SLIDE_MINUTES, round(words / WORDS_PER_MINUTE))


def strip_markdown(text: str) -> str:
    """Texto plano aproximado, solo para contar palabras."""
    without_code = re.sub(r"```.*?```", " ", text, flags=re.DOTALL)
    return re.sub(r"[*_`>#\[\]()-]", " ", without_code)


def parse_block(path: Path) -> tuple[str, str, list[Slide]]:
    """Devuelve (id del bloque, nombre del bloque, slides)."""
    source = path.read_text(encoding="utf-8")

    title_line = source.splitlines()[0] if source else ""
    parts = [p.strip() for p in title_line.lstrip("# ").split("·")]

    if len(parts) < 3:
        raise ValueError(
            f"{path.name}: la primera línea debe ser '# C-xx · B1 · Título del bloque'."
        )

    block_id, block_name = parts[1], " · ".join(parts[2:])

    guion = source.split("## Guion de slides", 1)
    if len(guion) != 2:
        raise ValueError(f"{path.name}: falta la sección '## Guion de slides'.")

    body = guion[1]

    # El guion termina donde empieza la siguiente sección de nivel 2. Sin este corte, la última
    # slide se tragaba todo lo que venía detrás —«Qué te llevas», el mini-quiz y el lab— y el
    # deck acababa mostrando las respuestas correctas con su marca justo encima del quiz.
    next_section = re.search(r"^## ", body, re.MULTILINE)
    if next_section:
        body = body[: next_section.start()]

    headings = list(HEADING.finditer(body))

    if not headings:
        raise ValueError(f"{path.name}: el guion no tiene ninguna slide '### N. Título'.")

    slides: list[Slide] = []

    for index, heading in enumerate(headings):
        end = headings[index + 1].start() if index + 1 < len(headings) else len(body)
        raw_title = heading.group("title")
        prose = body[heading.end():end].strip()

        marker = MARKER.search(raw_title)
        kind, marker_id = "slides", None

        if marker:
            raw_title = raw_title[: marker.start()].strip()
            kind = {"checkpoint": "slides", "ejercicio": "lab", "quiz": "quiz"}[marker.group("kind")]
            marker_id = marker.group("id")

            # El checkpoint se marca aparte: es una slide normal que además no es obligatoria.
            if marker.group("kind") == "checkpoint":
                marker_id = "checkpoint"

        # Un subtítulo opcional tras el título, separado por ": ".
        title, _, subtitle = raw_title.partition(": ")

        slides.append(Slide(int(heading.group("order")), title, subtitle, prose, kind, marker_id))

    pending = [s.title for s in slides if PENDING.search(s.body_md) or not s.body_md]

    if pending:
        # Publicar un bloque a medias es peor que no publicarlo: el alumno paga por slides que
        # dicen "TODO". Se falla aquí, con la lista de lo que falta.
        raise ValueError(
            f"{path.name}: {len(pending)} slide(s) sin prosa. La primera es «{pending[0]}»."
        )

    return block_id, block_name, sorted(slides, key=lambda s: s.order)


def shorten(text: str, limit: int) -> str:
    """Recorta por palabra entera. Una etiqueta cortada a mitad de palabra se lee peor que corta."""
    if len(text) <= limit:
        return text

    cut = text[:limit].rsplit(" ", 1)[0]
    return (cut or text[:limit]).rstrip(" ,;:")


def parse_quiz(source: str, block_id: str) -> list[dict]:
    """
    Extrae el mini-quiz del Markdown del bloque.

    Sin esto, la slide de quiz se genera con su contenedor y el contenedor se queda vacío: es
    exactamente el fallo que tenía el pre-curso, donde el HTML pedía un quiz que nadie servía.

    Devuelve lista vacía si el bloque no trae quiz o si el formato no encaja. No revienta la
    generación: un bloque sin quiz es un bloque incompleto, no un bloque roto, y quien lo
    escribe se entera por el aviso.
    """
    section = source.split("## Mini-quiz", 1)
    if len(section) != 2:
        return []

    body = section[1].split("\n## ", 1)[0]
    questions: list[dict] = []

    for match in QUESTION.finditer(body):
        block = match.group("body")

        statement = STATEMENT.search(block)
        explanation = EXPLANATION.search(block)
        options = OPTION.findall(block)

        if not statement or len(options) < 2:
            continue

        parsed = []
        for raw in options:
            correct = "✅" in raw
            # Se limpian la marca y las negritas con las que se señala la correcta.
            text = raw.replace("✅", "").strip()
            text = re.sub(r"^\*\*(.+?)\*\*$", r"\1", text).strip()
            parsed.append({"text": text, "correct": correct})

        if not any(option["correct"] for option in parsed):
            # Una pregunta sin respuesta correcta puntuaría mal a todo el mundo.
            continue

        # La categoría es una etiqueta corta, y la columna que la guarda mide 80. El «Tema:»
        # del guion suele ser una frase entera, así que se recorta por palabra: pasarse de
        # largo hacía que el curso se importara con sus clases y sin ninguno de sus quizzes.
        questions.append(
            {
                "cat": shorten(match.group("tema").strip(), MAX_CATEGORY),
                "q": statement.group("text").strip(),
                "options": parsed,
                "explanation": (explanation.group("text").strip() if explanation else ""),
            }
        )

    return questions


def render_markdown(text: str) -> str:
    """
    Markdown mínimo a HTML: párrafos, listas, código, negrita, cursiva y citas.

    Deliberadamente pequeño y sin dependencias. El contenido lo escribimos nosotros, así que no
    hay HTML arbitrario que sanear, y una librería completa traería su propio criterio de
    formato que divergiría del curso original.
    """
    # Los bloques de código se apartan ANTES de partir por líneas en blanco. Si no, un código
    # con una línea en blanco dentro —lo normal— se partía en dos y salía media etiqueta `pre`
    # seguida de párrafos con el resto del código escapado.
    fences: list[str] = []

    def keep(match: re.Match[str]) -> str:
        language = (match.group("lang") or "").strip()
        body = match.group("code")
        fences.append(
            f'<pre><code class="language-{html.escape(language)}">'
            f"{html.escape(body.strip(chr(10)).rstrip())}</code></pre>"
        )
        return f"\n\n\x00FENCE{len(fences) - 1}\x00\n\n"

    text = FENCE.sub(keep, text)

    blocks = re.split(r"\n\s*\n", text.strip())
    out: list[str] = []

    for block in blocks:
        block = block.strip()
        if not block:
            continue

        placeholder = PLACEHOLDER.fullmatch(block)
        if placeholder:
            out.append(fences[int(placeholder.group(1))])
            continue

        if block.startswith("> "):
            quote = " ".join(line.lstrip("> ") for line in block.splitlines())
            out.append(f'<div class="profesor-quote">{inline(quote)}</div>')
            continue

        if all(line.lstrip().startswith(("- ", "* ")) for line in block.splitlines()):
            items = "".join(
                f"<li>{inline(line.lstrip()[2:])}</li>" for line in block.splitlines()
            )
            out.append(f"<ul>{items}</ul>")
            continue

        if all(ORDERED_ITEM.match(line.lstrip()) for line in block.splitlines()):
            items = "".join(
                "<li>" + inline(ORDERED_ITEM.sub("", line.lstrip())) + "</li>"
                for line in block.splitlines()
            )
            out.append(f"<ol>{items}</ol>")
            continue

        if block.startswith("#### "):
            out.append(f"<h3>{inline(block[5:])}</h3>")
            continue

        out.append(f"<p>{inline(block)}</p>")

    return "\n".join(out)


def inline(text: str) -> str:
    """Negrita, cursiva y código dentro de un párrafo. Se escapa primero."""
    escaped = html.escape(text)
    escaped = re.sub(r"`([^`]+)`", r"<code>\1</code>", escaped)
    escaped = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", escaped)
    escaped = re.sub(r"(?<!\*)\*([^*]+)\*(?!\*)", r"<em>\1</em>", escaped)
    return escaped


def render_slide(slide: Slide, index: int, total: int, block_id: str) -> str:
    attributes = f'id="slide-{index}" data-slide-idx="{index}"'

    if slide.marker_id == "checkpoint":
        attributes += ' data-checkpoint="true"'
    elif slide.kind == "lab":
        attributes += ' data-exercise-slide="true"'
    elif slide.kind == "quiz":
        attributes += ' data-quiz-slide="true"'

    meta = {
        "lab": "🧪 Ejercicio",
        "quiz": "✅ Quiz",
    }.get(slide.kind, f"⏱ ~{slide.minutes} min")

    if slide.marker_id == "checkpoint":
        meta = "🎯 Checkpoint"

    subtitle = (
        f'    <div class="slide-subtitle">{html.escape(slide.subtitle)}</div>\n'
        if slide.subtitle
        else ""
    )

    # El contenedor lo rellena quiz.js con los datos del sidecar del bloque.
    #
    # Los ejercicios NO llevan contenedor: su enunciado ya está escrito en la propia slide, y
    # el widget del curso original necesita datos (criterios, pista, respuesta modelo) que este
    # formato no tiene. Un contenedor sin datos es un hueco en blanco debajo del enunciado.
    container = ""
    if slide.kind == "quiz":
        container = f'\n    <div class="quiz-container" data-quiz="{slide.marker_id or block_id}"></div>'

    return f"""        <section {attributes}>
    <div class="slide-meta">
        <span class="bloque-tag">{html.escape(block_id)}</span>
        <span class="slide-num">Slide {index + 1} de {total}</span>
        <span class="time-est">{meta}</span>
    </div>
    <h2 class="slide-title">{html.escape(slide.title)}</h2>
{subtitle}{render_markdown(slide.body_md)}{container}
        </section>"""


def render_deck(block_id: str, block_name: str, slides: list[Slide], course_title: str, stem: str) -> str:
    body = "\n\n".join(
        render_slide(slide, index, len(slides), block_id) for index, slide in enumerate(slides)
    )

    return f"""<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{html.escape(block_id)} · {html.escape(block_name)} — {html.escape(course_title)}</title>
    <link rel="stylesheet" href="../assets/css/curso.css">
    <script>
        // El tema lo hereda de la academia, antes de pintar para evitar el parpadeo. Orden:
        // lo que manda el player en la URL y, si no viene, la preferencia del sistema.
        // No se lee localStorage: el tema es de la academia, no del documento, y una copia
        // guardada aquí se quedaría vieja en cuanto la persona lo cambiara fuera.
        (function() {{
            var q = new URLSearchParams(location.search).get("theme");
            var dark = q ? q === "dark"
                         : window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
            document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
        }})();
    </script>
</head>
<body>
    <main class="curso-main">
{body}
    </main>

    <script src="../assets/js/progreso.js"></script>
    <script src="../assets/js/quiz.js"></script>
    <script src="{html.escape(stem)}.data/quizzes.js"></script>
    <script src="../assets/js/viewer.js"></script>
    <script>
        CursoViewer.init("{html.escape(block_id)}", {len(slides)});
        CursoViewer.setBloqueName("{html.escape(block_name)}");
    </script>
</body>
</html>
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--course", required=True, help="Carpeta bajo content/cursos")
    parser.add_argument("--title", default=None, help="Título del curso para el <title>")
    parser.add_argument("--out", required=True, type=Path, help="Carpeta destino del deck")
    parser.add_argument(
        "--assets",
        type=Path,
        default=None,
        help="Carpeta de assets compartidos. Por defecto, los del curso original.",
    )
    args = parser.parse_args()

    source = Path(__file__).resolve().parents[1] / "cursos" / args.course
    if not source.is_dir():
        print(f"No existe {source}", file=sys.stderr)
        return 1

    blocks_dir = args.out / "bloques"
    blocks_dir.mkdir(parents=True, exist_ok=True)

    # Los decks piden `../assets/css/curso.css` y los tres scripts del visor. Sin copiarlos, el
    # curso se sirve sin estilos y sin quiz: el token solo alcanza los assets de SU curso, así
    # que apuntar a los de otro no es una opción.
    #
    # La fuente es el directorio versionado en tools/assets, no la copia servida de un curso:
    # copiar desde content/dist hacía que el "maestro" fuera un fichero generado, y una edición
    # en cualquiera de las copias divergía en silencio.
    shared = args.assets or (Path(__file__).resolve().parent / "assets")

    if not shared.is_dir():
        # Respaldo para árboles antiguos donde tools/assets aún no existe.
        shared = Path(__file__).resolve().parents[1] / "dist" / "agent-engineering-v3" / "assets"

    if shared.is_dir():
        # El test de nivel NO se copia: es contenido del curso que lo escribió, no chrome
        # compartido. Copiándolo, el curso nuevo se importaba con el test de nivel del otro y
        # preguntaba por temas que no explica.
        shutil.copytree(
            shared,
            args.out / "assets",
            dirs_exist_ok=True,
            ignore=shutil.ignore_patterns("admission-quiz-data.js", "quizzes-data.js"),
        )
        print(f"assets compartidos copiados desde {shared}")
    else:
        print(f"aviso: no se encuentran los assets en {shared}; el curso se verá sin estilos")

    built = 0
    skipped: list[str] = []

    for path in sorted(source.glob("*.md")):
        # En la carpeta del curso también hay notas de trabajo —VERIFICADO.md, por ejemplo—.
        # No son bloques y no deben aparecer como «pendientes de prosa».
        first = path.read_text(encoding="utf-8").splitlines()[:1]
        if not first or "·" not in first[0]:
            continue

        try:
            block_id, block_name, slides = parse_block(path)
        except ValueError as problem:
            # Se salta y se dice, en vez de abortar: así se pueden ir publicando los bloques
            # terminados mientras el resto se escribe.
            skipped.append(str(problem))
            continue

        deck = render_deck(block_id, block_name, slides, args.title or args.course, path.stem)
        (blocks_dir / f"{path.stem}.html").write_text(deck, encoding="utf-8")

        # Los datos del quiz van en la carpeta del documento, no en un banco compartido: el
        # contenido se sirve por fichero y un banco común entregaría los quizzes de todos los
        # bloques a quien abra uno solo.
        questions = parse_quiz(path.read_text(encoding="utf-8"), block_id)
        sidecar = blocks_dir / f"{path.stem}.data"
        sidecar.mkdir(exist_ok=True)

        (sidecar / "quizzes.js").write_text(
            "// Generado por build_course.py desde el Markdown del bloque.\n\n"
            f"window.QUIZZES = Object.assign(window.QUIZZES || {{}}, "
            f"{json.dumps({block_id: questions}, ensure_ascii=False, indent=2)});\n",
            encoding="utf-8",
        )

        aviso = "" if questions else "  ⚠ sin mini-quiz legible"
        print(f"{block_id}: {len(slides)} slides, {len(questions)} preguntas -> {path.stem}.html{aviso}")
        built += 1

    for problem in skipped:
        print(f"  pendiente · {problem}")

    print(f"{built} bloque(s) generados, {len(skipped)} pendiente(s) de prosa.")

    return 0 if built else 1


if __name__ == "__main__":
    raise SystemExit(main())
