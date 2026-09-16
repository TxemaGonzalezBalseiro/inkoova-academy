"""Lector tolerante de literales de objeto JavaScript.

El contenido del curso guarda sus quizzes como asignaciones JS (`window.ADMISSION_QUIZ = [...]`)
con claves sin comillas y comas finales. Eso no es JSON, así que `json.loads` falla.

En vez de arrastrar una dependencia (json5, pyjsparser) para leer dos ficheros, aquí hay un
normalizador que recorre el texto carácter a carácter. Recorrerlo importa: un reemplazo con
expresiones regulares rompería cualquier `{` o `,` que aparezca *dentro* de una cadena, y las
explicaciones del quiz están llenas de comas, comillas simples y llaves.
"""

from __future__ import annotations

import json
import re
from typing import Any

_IDENTIFIER = re.compile(r"[A-Za-z_$][A-Za-z0-9_$]*")


def normalize(source: str) -> str:
    """Convierte un literal de objeto JS en JSON válido.

    Cubre lo que usa el contenido real: claves sin comillas, comas finales y comentarios de
    línea. No pretende ser un parser de JavaScript: si algún día el contenido usa template
    literals o expresiones, esto debe fallar de forma visible, no adivinar.
    """
    out: list[str] = []
    i = 0
    length = len(source)

    while i < length:
        char = source[i]

        # Cadenas: se copian tal cual, escapes incluidos.
        if char == '"':
            j = i + 1
            while j < length:
                if source[j] == "\\":
                    j += 2
                    continue
                if source[j] == '"':
                    break
                j += 1
            out.append(source[i : j + 1])
            i = j + 1
            continue

        if char == "'":
            raise ValueError(
                "El contenido usa cadenas con comillas simples; el normalizador solo admite "
                "comillas dobles. Revisa el fichero antes de importarlo."
            )

        if char == "`":
            raise ValueError(
                "El contenido usa template literals; el normalizador no los soporta. "
                "Conviértelos a cadenas normales en el generador de contenido."
            )

        # Comentarios.
        if char == "/" and i + 1 < length:
            if source[i + 1] == "/":
                i = source.find("\n", i)
                if i == -1:
                    break
                continue
            if source[i + 1] == "*":
                end = source.find("*/", i + 2)
                i = length if end == -1 else end + 2
                continue

        # Clave sin comillas: identificador seguido de dos puntos.
        if char.isalpha() or char in "_$":
            match = _IDENTIFIER.match(source, i)
            if match:
                after = match.end()
                while after < length and source[after] in " \t\r\n":
                    after += 1
                if after < length and source[after] == ":":
                    out.append(json.dumps(match.group(0)))
                    i = match.end()
                    continue
                # Literales JS que sí son JSON válidos.
                if match.group(0) in ("true", "false", "null"):
                    out.append(match.group(0))
                    i = match.end()
                    continue
                raise ValueError(
                    f"Identificador inesperado '{match.group(0)}' en la posición {i}: "
                    "el normalizador solo admite datos, no expresiones."
                )

        # Coma final antes de un cierre.
        if char == ",":
            j = i + 1
            while j < length and source[j] in " \t\r\n":
                j += 1
            if j < length and source[j] in "]}":
                i = j
                continue

        out.append(char)
        i += 1

    return "".join(out)


def load_assignment(source: str, variable: str) -> Any:
    """Extrae el valor asignado a `variable` en un fichero JS y lo devuelve como dato Python.

    Acepta tanto `window.X = ...` como `const X = ...`; el contenido usa ambas formas según
    el fichero.
    """
    pattern = re.compile(
        rf"(?:window\.)?(?:const\s+|let\s+|var\s+)?{re.escape(variable)}\s*=\s*",
    )
    match = pattern.search(source)
    if not match:
        raise ValueError(f"No se encuentra la asignación de '{variable}' en el fichero.")

    start = match.end()
    if source[start] not in "[{":
        # `Object.assign(window.QUIZZES || {}, { ... })`: el primer literal es el `{}` del
        # operador `||`, que está vacío. El dato es el primer literal con contenido.
        start = _find_non_empty_literal(source, start, variable)

    opening = source[start]
    closing = "]" if opening == "[" else "}"
    depth = 0
    i = start
    in_string = False

    while i < len(source):
        char = source[i]

        if in_string:
            if char == "\\":
                i += 2
                continue
            if char == '"':
                in_string = False
        elif char == '"':
            in_string = True
        elif char == opening:
            depth += 1
        elif char == closing:
            depth -= 1
            if depth == 0:
                return json.loads(normalize(source[start : i + 1]))

        i += 1

    raise ValueError(f"El literal de '{variable}' no está balanceado.")


def _find_non_empty_literal(source: str, start: int, variable: str) -> int:
    """Primera posición de un `{` o `[` que no abra un literal vacío."""
    i = start

    while i < len(source):
        if source[i] in "[{":
            j = i + 1
            while j < len(source) and source[j] in " \t\r\n":
                j += 1
            if j < len(source) and source[j] not in "]}":
                return i
            i = j + 1
            continue
        i += 1

    raise ValueError(f"La asignación de '{variable}' no contiene un literal con datos.")
