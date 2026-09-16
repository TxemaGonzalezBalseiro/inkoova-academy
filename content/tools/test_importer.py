#!/usr/bin/env python3
"""Pruebas del importador. Se ejecutan en CI sin necesidad del contenido real.

Lo que se comprueba es lo que puede romperse en silencio: el normalizador de literales JS
(que toca cadenas llenas de comas y comillas) y la estimación de duración.
"""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from html_course import group_lessons, parse_block_slides, parse_panes  # noqa: E402
from importer import (  # noqa: E402
    CourseManifest,
    LessonManifest,
    SectionManifest,
    ensure_access_is_per_file,
    ensure_not_self_copy,
    estimate_minutes,
    import_admission_quiz,
    import_single_file_course,
    slugify,
    split_quiz_bundles,
    split_single_file_course,
)
from jsobj import load_assignment, normalize  # noqa: E402


BLOCK_HTML = """
<main>
<section id="slide-0" data-slide-idx="0">
  <div class="slide-meta">
    <span class="bloque-tag">B0</span>
    <span class="slide-num">Slide 1 de 4</span>
    <span class="time-est">⏱ ~4 min</span>
  </div>
  <h2 class="slide-title">Bienvenido</h2>
  <div class="slide-subtitle">La disciplina</div>
  <p>Prosa.</p>
</section>
<section id="slide-1" data-slide-idx="1" data-checkpoint="true">
  <div class="slide-meta"><span class="time-est">🎯 Checkpoint</span></div>
  <h2 class="slide-title">Pausa de comprensión</h2>
</section>
<section id="slide-2" data-slide-idx="2" data-exercise-slide="true">
  <div class="slide-meta"><span class="time-est">🧪 Ejercicio 1 de 2</span></div>
  <h2 class="slide-title">Ejercicio práctico 1</h2>
</section>
<section id="slide-3" data-slide-idx="3" data-quiz-slide="true">
  <div class="slide-meta"><span class="time-est">✅ Quiz</span></div>
  <h2 class="slide-title">Quiz de comprensión</h2>
</section>
</main>
"""


class BlockSlideTests(unittest.TestCase):
    """La lección es la slide, no el fichero del bloque."""

    def setUp(self):
        self.slides = parse_block_slides(BLOCK_HTML, "B0", "Fundamentos")

    def test_every_slide_becomes_a_lesson(self):
        self.assertEqual(len(self.slides), 4)

    def test_the_anchor_locates_the_slide_inside_the_file(self):
        self.assertEqual([s.anchor for s in self.slides], ["slide-0", "slide-1", "slide-2", "slide-3"])

    def test_the_declared_duration_wins_over_any_estimate(self):
        self.assertEqual(self.slides[0].duration_minutes, 4)

    def test_slide_type_comes_from_the_section_attributes(self):
        self.assertEqual([s.lesson_type for s in self.slides], ["slides", "slides", "lab", "quiz"])

    def test_a_checkpoint_does_not_block_the_certificate(self):
        # Un checkpoint es una pausa para pensar: no produce evidencia.
        self.assertFalse(self.slides[1].is_required)
        self.assertTrue(all(s.is_required for s in (self.slides[0], self.slides[2], self.slides[3])))

    def test_slides_without_declared_minutes_get_a_value_by_type(self):
        checkpoint, exercise, quiz = self.slides[1], self.slides[2], self.slides[3]
        self.assertEqual((checkpoint.duration_minutes, exercise.duration_minutes, quiz.duration_minutes), (2, 10, 5))

    def test_all_slides_share_the_block_as_group(self):
        self.assertEqual({s.group for s in self.slides}, {"B0 · Fundamentos"})

    def test_a_section_without_title_is_not_a_lesson(self):
        html = '<section id="slide-0" data-slide-idx="0"><p>pie del visor</p></section>'
        self.assertEqual(parse_block_slides(html, "B0", "Fundamentos"), [])


class NormalizerTests(unittest.TestCase):
    def test_unquoted_keys_become_json(self):
        self.assertEqual(json.loads(normalize('{ cat: "A", q: "B" }')), {"cat": "A", "q": "B"})

    def test_trailing_commas_are_removed(self):
        self.assertEqual(json.loads(normalize('[1, 2, 3,]')), [1, 2, 3])
        self.assertEqual(json.loads(normalize('{ a: 1, }')), {"a": 1})

    def test_commas_and_braces_inside_strings_survive(self):
        source = '{ explanation: "Cuesta, y mucho: {a, b} sigue siendo texto." }'
        self.assertEqual(
            json.loads(normalize(source))["explanation"],
            "Cuesta, y mucho: {a, b} sigue siendo texto.",
        )

    def test_single_quotes_inside_a_double_quoted_string_are_not_touched(self):
        source = '{ q: "temperature alta para que sea mas \'creativo\' eligiendo" }'
        self.assertIn("'creativo'", json.loads(normalize(source))["q"])

    def test_booleans_are_preserved(self):
        self.assertEqual(json.loads(normalize('{ correct: true }')), {"correct": True})

    def test_line_comments_are_dropped(self):
        source = """
        [
          // esto es un comentario
          { a: 1 }
        ]
        """
        self.assertEqual(json.loads(normalize(source)), [{"a": 1}])

    def test_escaped_quotes_survive(self):
        source = '{ q: "dice \\"hola\\" y se va" }'
        self.assertEqual(json.loads(normalize(source))["q"], 'dice "hola" y se va')


class AssignmentTests(unittest.TestCase):
    def test_window_assignment(self):
        source = 'window.ADMISSION_QUIZ = [ { cat: "A", q: "B", options: [] }, ];'
        value = load_assignment(source, "ADMISSION_QUIZ")
        self.assertEqual(value[0]["cat"], "A")

    def test_object_assign_skips_the_empty_default(self):
        # Este es exactamente el patrón de quizzes-pre.js: el primer literal es el `{}` del
        # operador `||` y el dato está en el segundo argumento.
        source = 'window.QUIZZES = Object.assign(window.QUIZZES || {}, { "PRE-P0": [ { q: "x" } ] });'
        value = load_assignment(source, "QUIZZES")
        self.assertIn("PRE-P0", value)
        self.assertEqual(value["PRE-P0"][0]["q"], "x")

    def test_missing_variable_fails_loudly(self):
        with self.assertRaises(ValueError):
            load_assignment("window.OTRA = [];", "ADMISSION_QUIZ")


class DurationTests(unittest.TestCase):
    def test_script_and_style_do_not_count_towards_duration(self):
        prose = "<p>" + ("palabra " * 1300) + "</p>"
        noise = "<script>" + ("var x = 1; " * 5000) + "</script>"

        self.assertEqual(estimate_minutes(prose), estimate_minutes(prose + noise))

    def test_a_short_page_still_gets_a_minimum(self):
        self.assertGreaterEqual(estimate_minutes("<p>Portada</p>"), 3)


class SlugTests(unittest.TestCase):
    def test_accents_and_symbols_are_normalised(self):
        self.assertEqual(slugify("B6 · Seguridad y gobernanza"), "b6-seguridad-y-gobernanza")

    def test_repeated_separators_collapse(self):
        self.assertEqual(slugify("a --  b"), "a-b")


class AdmissionQuizTests(unittest.TestCase):
    def test_the_quiz_is_imported_marking_the_valid_options(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            js_dir = root / "assets" / "js"
            js_dir.mkdir(parents=True)

            (js_dir / "admission-quiz-data.js").write_text(
                """
                window.ADMISSION_QUIZ = [
                  {
                    cat: "Fundamentos LLM",
                    q: "¿Cuál es correcta?",
                    options: [
                      { text: "Una" },
                      { text: "Dos", correct: true },
                    ],
                    explanation: "Porque sí."
                  },
                ];
                """,
                encoding="utf-8",
            )

            quiz = import_admission_quiz(root, "curso-x")

            self.assertIsNotNone(quiz)
            self.assertEqual(quiz.slug, "curso-x-nivel")
            self.assertEqual(quiz.kind, "admission")
            self.assertEqual(len(quiz.questions), 1)
            # Una o varias: el test de nivel original marca como válidas todas las opciones
            # que acreditan el nivel, no solo una.
            correct = [o for o in quiz.questions[0].options if o["isCorrect"]]
            self.assertGreaterEqual(len(correct), 1)
            self.assertEqual(correct[0]["text"], "Dos")

    def test_a_missing_file_is_not_an_error(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertIsNone(import_admission_quiz(Path(tmp), "curso-x"))


PANE_HTML = """
<main>
<article class="pane" id="pane-m0-l0">
  <p class="kicker">Módulo 1 · Fundamentos</p>
  <h1 class="l-title"><span class="l-num">1.1</span>Qué es un prompt</h1>
  <div class="l-meta"><span class="chip chip-lectura">lectura</span><span class="chip chip-dur">15 min</span></div>
</article>
<article class="pane" id="pane-m0-l1">
  <p class="kicker">Módulo 1 · Fundamentos</p>
  <h1 class="l-title"><span class="l-num">1.2</span>Anatomía</h1>
  <div class="l-meta"><span class="chip chip-práctica">práctica</span><span class="chip chip-dur">20 min</span></div>
</article>
<article class="pane" id="pane-m1-l0">
  <p class="kicker">Módulo 2 · Técnicas</p>
  <h1 class="l-title"><span class="l-num">2.1</span>Few-shot</h1>
  <div class="l-meta"><span class="chip chip-lectura">lectura</span><span class="chip chip-dur">12 min</span></div>
</article>
</main>
"""


class PaneTests(unittest.TestCase):
    """El curso de prompt engineering vive en un solo HTML, con un pane por lección."""

    def setUp(self):
        self.panes = parse_panes(PANE_HTML)

    def test_each_pane_is_a_lesson(self):
        self.assertEqual(len(self.panes), 3)

    def test_the_lesson_number_is_removed_from_the_title(self):
        # El número va dentro del h1; dejarlo lo mostraría dos veces en el temario.
        self.assertEqual(self.panes[0].title, "Qué es un prompt")
        self.assertEqual(self.panes[0].subtitle, "1.1")

    def test_the_chip_decides_the_lesson_type(self):
        self.assertEqual([p.lesson_type for p in self.panes], ["slides", "lab", "slides"])

    def test_panes_group_by_module_in_order(self):
        groups = group_lessons(self.panes)
        self.assertEqual([name for name, _ in groups], ["Módulo 1 · Fundamentos", "Módulo 2 · Técnicas"])
        self.assertEqual([len(items) for _, items in groups], [2, 1])


class GroupingTests(unittest.TestCase):
    def test_grouping_preserves_pedagogical_order(self):
        slides = parse_block_slides(BLOCK_HTML, "B0", "Fundamentos")
        groups = group_lessons(slides)

        self.assertEqual(len(groups), 1)
        self.assertEqual(groups[0][0], "B0 · Fundamentos")
        self.assertEqual(len(groups[0][1]), 4)


BUNDLE_JS = """
window.QUIZZES = {
  "B0": [ { q: "Gratis 1", options: [ { text: "a", correct: true } ] } ],
  "B3": [ { q: "De pago 1", options: [ { text: "b", correct: true } ] } ],
};

window.EXERCISES = {
  "B0-ej1": { prompt: "Practica", model_answer: "Respuesta de B0" },
  "B3-ej1": { prompt: "Practica", model_answer: "Respuesta de B3" },
};
"""

PRE_BUNDLE_JS = (
    'window.QUIZZES = Object.assign(window.QUIZZES || {}, '
    '{ "PRE-P0": [ { q: "De P0" } ], "PRE-P1": [ { q: "De P1" } ] });'
)


def _course_with_bundle(root: Path) -> Path:
    """Curso mínimo con dos bloques y el banco de quizzes compartido."""
    (root / "assets" / "js").mkdir(parents=True)
    (root / "assets" / "js" / "quizzes-data.js").write_text(BUNDLE_JS, encoding="utf-8")

    (root / "bloques").mkdir()
    for name, block in (("B0-fundamentos.html", "B0"), ("B3-multi-agente.html", "B3")):
        (root / "bloques" / name).write_text(
            f'<div class="quiz-container" data-quiz="{block}"></div>'
            f'<div data-exercise="{block}-ej1"></div>'
            '<script src="../assets/js/quizzes-data.js"></script>',
            encoding="utf-8",
        )

    # Un HTML de la misma carpeta que no carga el banco: no debe recibir carpeta de datos.
    (root / "bloques" / "index.html").write_text("<p>Índice</p>", encoding="utf-8")

    return root


def _precourse_with_bundle(root: Path) -> Path:
    """Pre-curso: el banco vive junto a los documentos y sus claves llevan prefijo."""
    (root / "pre-curso").mkdir(parents=True)
    (root / "pre-curso" / "quizzes-pre.js").write_text(PRE_BUNDLE_JS, encoding="utf-8")

    for name, block in (("P0-fundamentos-llm.html", "P0"), ("P1-primera-llamada.html", "P1")):
        (root / "pre-curso" / name).write_text(
            f'<div class="quiz-container" data-quiz="{block}"></div>'
            '<script src="quizzes-pre.js"></script>',
            encoding="utf-8",
        )

    return root


class QuizBundleSplitTests(unittest.TestCase):
    """El banco compartido traía B0 a B7 juntos y cada bloque lo cargaba entero.

    Como es un asset del curso, el token de una lección gratuita lo alcanzaba: las preguntas y
    los `model_answer` de los bloques de pago quedaban a la vista. Con un fichero por bloque,
    cada token solo alcanza el suyo.
    """

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = _course_with_bundle(Path(self._tmp.name))
        self.written = split_quiz_bundles(self.root)
        self.blocks = self.root / "bloques"

    def tearDown(self):
        self._tmp.cleanup()

    def _sidecar(self, stem: str) -> Path:
        return self.blocks / f"{stem}.data" / "quizzes.js"

    def test_one_data_folder_per_document(self):
        # Junto al documento y con su nombre: así la API ata el permiso al documento sin
        # necesitar saber nada del contenido.
        self.assertEqual(self.written, 2)
        self.assertTrue(self._sidecar("B0-fundamentos").exists())
        self.assertTrue(self._sidecar("B3-multi-agente").exists())

    def test_the_shared_bundle_is_gone(self):
        # Si se quedara seguiría siendo alcanzable y el reparto no serviría de nada.
        self.assertFalse((self.root / "assets" / "js" / "quizzes-data.js").exists())

    def test_a_document_only_gets_its_own_block(self):
        free = self._sidecar("B0-fundamentos").read_text(encoding="utf-8")

        self.assertIn("Gratis 1", free)
        self.assertNotIn("De pago 1", free)
        self.assertNotIn("Respuesta de B3", free)

    def test_exercises_travel_with_their_block(self):
        free = self._sidecar("B0-fundamentos").read_text(encoding="utf-8")
        self.assertIn("Respuesta de B0", free)

    def test_each_document_now_loads_its_own_data(self):
        html = (self.blocks / "B3-multi-agente.html").read_text(encoding="utf-8")

        self.assertIn("B3-multi-agente.data/quizzes.js", html)
        self.assertNotIn("quizzes-data.js", html)

    def test_the_pieces_do_not_overwrite_each_other(self):
        # Object.assign y no asignación directa: cada fichero se carga por separado y el
        # segundo no puede borrar lo que dejó el primero.
        for stem in ("B0-fundamentos", "B3-multi-agente"):
            body = self._sidecar(stem).read_text(encoding="utf-8")
            self.assertIn("window.QUIZZES = Object.assign(window.QUIZZES || {}", body)

    def test_a_course_without_the_bundle_is_left_alone(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(split_quiz_bundles(Path(tmp)), 0)

    def test_a_document_that_does_not_load_the_bundle_gets_no_data_folder(self):
        self.assertFalse((self.blocks / "index.data").exists())

    def test_the_precourse_bundle_is_split_the_same_way(self):
        # El pre-curso trae su banco al lado de sus documentos y con claves "PRE-P0", no en
        # assets. Es gratis entero, pero se reparte igual: una sola regla en la API.
        with tempfile.TemporaryDirectory() as tmp:
            root = _precourse_with_bundle(Path(tmp))

            self.assertEqual(split_quiz_bundles(root), 2)

            data = (root / "pre-curso" / "P0-fundamentos-llm.data" / "quizzes.js").read_text(
                encoding="utf-8"
            )
            self.assertIn("De P0", data)
            self.assertNotIn("De P1", data)

            html = (root / "pre-curso" / "P0-fundamentos-llm.html").read_text(encoding="utf-8")
            self.assertIn("P0-fundamentos-llm.data/quizzes.js", html)

    def test_the_data_is_rekeyed_to_what_the_document_asks_for(self):
        # El pre-curso pide `data-quiz="P0"` y su banco dice "PRE-P0". Por eso sus mini-quiz
        # nunca se renderizaban: `viewer.js` busca la clave tal cual. El reparto la reescribe.
        with tempfile.TemporaryDirectory() as tmp:
            root = _precourse_with_bundle(Path(tmp))
            split_quiz_bundles(root)

            data = (root / "pre-curso" / "P0-fundamentos-llm.data" / "quizzes.js").read_text(
                encoding="utf-8"
            )

            self.assertIn('"P0"', data)
            self.assertNotIn("PRE-P0", data)

    def test_data_that_no_document_asks_for_stops_the_import(self):
        # Antes se perdía en silencio y el curso salía incompleto sin que nada fallara.
        with tempfile.TemporaryDirectory() as tmp:
            root = _precourse_with_bundle(Path(tmp))
            (root / "pre-curso" / "P1-primera-llamada.html").unlink()

            with self.assertRaises(ValueError):
                split_quiz_bundles(root)

    def test_a_document_asking_for_data_that_is_not_there_stops_the_import(self):
        # Un quiz que se pide y no existe se vería como un hueco en blanco en la lección.
        with tempfile.TemporaryDirectory() as tmp:
            root = _course_with_bundle(Path(tmp))
            bundle = root / "assets" / "js" / "quizzes-data.js"
            bundle.write_text(BUNDLE_JS.replace('"B3": [', '"B9": ['), encoding="utf-8")

            with self.assertRaises(ValueError):
                split_quiz_bundles(root)


SINGLE_FILE_HTML = """<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>Curso</title>
<style>:root{--rail:300px}#main{margin-left:var(--rail)}.pane{display:none}.pane.show{display:block}</style>
</head>
<body>
<nav id="rail" aria-label="Índice del curso"><a href="#pane-m1-l0">Lección de pago</a></nav>
<main id="main">
<article class="pane" id="pane-m0-l0">
  <p class="kicker">Módulo 1 · Fundamentos</p>
  <h1 class="l-title"><span class="l-num">1.1</span>Qué es un prompt</h1>
  <div class="l-meta"><span class="chip chip-lectura">lectura</span><span class="chip chip-dur">15 min</span></div>
  <div class="prose"><p>Lección gratuita.</p></div>
</article>
<article class="pane" id="pane-m1-l0">
  <p class="kicker">Módulo 2 · Técnicas</p>
  <h1 class="l-title"><span class="l-num">2.1</span>Few-shot</h1>
  <div class="l-meta"><span class="chip chip-lectura">lectura</span><span class="chip chip-dur">12 min</span></div>
  <div class="prose"><p>SECRETO DE PAGO.</p></div>
</article>
</main>
<script>
/* ---------- bloques de código ---------- */
document.querySelectorAll('.prose pre').forEach(function (pre) { pre.dataset.listo = '1'; });
/* ---------- navegación ---------- */
var rail = document.getElementById('rail');
</script>
</body>
</html>
"""


class SingleFileSplitTests(unittest.TestCase):
    """El curso de prompt engineering vivía en UN html con sus 54 lecciones dentro.

    Copiarlo tal cual al volumen servido dejaba el muro de pago en nada: el token de una
    lección gratuita ampara «su propio documento», y su documento era el curso entero. Con un
    documento por lección, cada token alcanza solo el suyo.
    """

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.target = Path(self._tmp.name)
        self.written = split_single_file_course(SINGLE_FILE_HTML, self.target)
        self.gratis = (self.target / "panes" / "pane-m0-l0.html").read_text(encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def test_one_document_per_lesson(self):
        self.assertEqual(sorted(self.written), ["pane-m0-l0.html", "pane-m1-l0.html"])

    def test_the_paid_lesson_is_not_inside_the_free_one(self):
        # Esto es el muro de pago. Si vuelve a fallar, un alumno sin plan recibe el curso
        # entero por abrir la lección de muestra.
        self.assertNotIn("SECRETO DE PAGO", self.gratis)
        self.assertIn("Lección gratuita", self.gratis)

    def test_the_documents_own_index_is_gone(self):
        # Dentro del player se veían dos temarios, y el del documento navegaba a lecciones
        # que la persona no ha pagado.
        self.assertNotIn('id="rail"', self.gratis)

    def test_the_lesson_is_visible_without_the_router(self):
        # `.pane` está en display:none y lo enciende el router del documento, que ya no viaja.
        self.assertIn('class="pane show"', self.gratis)

    def test_the_gutter_reserved_for_the_index_is_reclaimed(self):
        # Sin esto queda una franja vacía de 300 px justo donde el player pinta su temario.
        self.assertIn("--rail:0px", self.gratis.replace(" ", ""))

    def test_the_styles_of_the_course_travel_untouched(self):
        self.assertIn("--rail:300px", self.gratis)

    def test_the_code_block_enhancement_travels(self):
        # Se recorta del original, no se reescribe: el botón de copiar es del curso.
        self.assertIn("pre.dataset.listo", self.gratis)

    def test_the_navigation_script_does_not_travel(self):
        # El router y el índice sobran, y buscarían elementos que ya no existen.
        self.assertNotIn("getElementById('rail')", self.gratis)

    def test_a_document_without_panes_aborts(self):
        # Perderlo en silencio daría un curso sin lecciones que parece importado.
        with self.assertRaises(ValueError):
            split_single_file_course("<html><head></head><body></body></html>", self.target)

    def test_a_document_without_the_code_marker_aborts(self):
        with self.assertRaises(ValueError):
            split_single_file_course(
                SINGLE_FILE_HTML.replace("/* ---------- bloques de código ---------- */", ""),
                self.target,
            )


class SingleFileManifestTests(unittest.TestCase):
    """El manifest tiene que apuntar a los documentos que escribe el separador, no al fichero."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        source = Path(self._tmp.name) / "curso.html"
        source.write_text(SINGLE_FILE_HTML, encoding="utf-8")
        self.manifest = import_single_file_course(source, "prompt-engineering", "Curso", 1)

    def tearDown(self):
        self._tmp.cleanup()

    def test_each_lesson_points_at_its_own_document(self):
        refs = [l.contentRef for s in self.manifest.sections for l in s.lessons]
        self.assertEqual(
            refs,
            [
                "prompt-engineering/panes/pane-m0-l0.html",
                "prompt-engineering/panes/pane-m1-l0.html",
            ],
        )

    def test_no_lesson_points_at_a_fragment_of_the_whole_file(self):
        # `index.html#pane-m0-l0` era la forma del fallo: el documento de CADA lección era el
        # curso completo.
        for section in self.manifest.sections:
            for lesson in section.lessons:
                self.assertNotIn("#", lesson.contentRef)
                self.assertNotIn("index.html", lesson.contentRef)

    def test_the_free_modules_stay_free(self):
        libres = [l.isFreePreview for s in self.manifest.sections for l in s.lessons]
        self.assertEqual(libres, [True, False])


class SelfCopyTests(unittest.TestCase):
    """Copiar una carpeta sobre sí misma deja sus ficheros a CERO bytes, y no falla.

    Pasó de verdad: `--source content/dist/<slug> --copy-to content/dist` dejó dos cursos
    enteros sirviendo documentos vacíos con un 200. El manifest salía correcto —se lee antes de
    copiar— así que la importación decía que todo había ido bien.
    """

    def test_the_same_folder_aborts(self):
        with tempfile.TemporaryDirectory() as tmp:
            carpeta = Path(tmp) / "curso"
            carpeta.mkdir()

            with self.assertRaises(ValueError):
                ensure_not_self_copy(carpeta, carpeta)

    def test_copying_into_a_subfolder_of_the_source_aborts(self):
        # `--source content/dist --copy-to content/dist` con slug: el destino cae dentro.
        with tempfile.TemporaryDirectory() as tmp:
            origen = Path(tmp) / "dist"
            origen.mkdir()

            with self.assertRaises(ValueError):
                ensure_not_self_copy(origen, origen / "curso")

    def test_two_different_folders_are_fine(self):
        with tempfile.TemporaryDirectory() as tmp:
            origen = Path(tmp) / "generado"
            destino = Path(tmp) / "dist" / "curso"
            origen.mkdir()

            ensure_not_self_copy(origen, destino)


class PerFileAccessTests(unittest.TestCase):
    """Un fichero no puede servir lecciones de muestra y de pago a la vez.

    El token ampara «su propio documento». Si ese documento lleva dentro lecciones de pago, el
    muro no existe: basta con abrir la de muestra. Es lo que pasó con el curso de prompt
    engineering, que vivía entero en un HTML, y lo que podría volver a pasar cada vez que
    alguien marca un bloque como gratuito.
    """

    @staticmethod
    def _manifest(*lecciones: tuple[str, bool]) -> CourseManifest:
        manifest = CourseManifest(
            manifestVersion=1,
            courseSlug="curso",
            title="Curso",
            shortDescription="",
            longDescription="",
            level="intro",
        )
        seccion = SectionManifest(title="Bloque", order=0)

        for ref, gratis in lecciones:
            seccion.lessons.append(
                LessonManifest(
                    slug=slugify(ref + str(gratis)),
                    title=f"Lección {ref}",
                    type="slides",
                    durationMinutes=10,
                    contentRef=ref,
                    isFreePreview=gratis,
                )
            )

        manifest.sections.append(seccion)
        return manifest

    def test_a_file_with_free_and_paid_lessons_aborts(self):
        manifest = self._manifest(("curso/todo.html#slide-0", True), ("curso/todo.html#slide-1", False))

        with self.assertRaises(ValueError) as caught:
            ensure_access_is_per_file(manifest)

        self.assertIn("todo.html", str(caught.exception))

    def test_one_file_per_access_level_is_fine(self):
        # Es lo que hace posible marcar P0 del pre-curso como muestra: un fichero por bloque.
        ensure_access_is_per_file(
            self._manifest(("curso/P0.html#slide-0", True), ("curso/P1.html#slide-0", False))
        )

    def test_a_fully_free_file_is_fine(self):
        ensure_access_is_per_file(
            self._manifest(("curso/P0.html#slide-0", True), ("curso/P0.html#slide-1", True))
        )

    def test_a_fully_paid_file_is_fine(self):
        ensure_access_is_per_file(
            self._manifest(("curso/P1.html#slide-0", False), ("curso/P1.html#slide-1", False))
        )


if __name__ == "__main__":
    unittest.main(verbosity=2)
