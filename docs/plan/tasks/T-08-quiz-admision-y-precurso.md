# T-08 · Quiz de admisión y pre-curso

## Contexto
Existen `nivel.html` (12 preguntas, 4 dimensiones, veredictos APTO / CASI / VEN MÁS TARDE) y `pre-curso/` (P0–P3, mini-quiz de 3 preguntas cada uno).

## Alcance
- Modelo `Quiz` + `Question` en BD; importar las preguntas desde `content.py` (mismo importador de T-07).
- Componente `Quiz` React genérico (single choice, corrección, veredicto) que reemplaza la lógica embebida en HTML cuando se ejecuta dentro de la plataforma.
- Flujo: ficha de curso → "Comprueba tu nivel" → resultado guardado en `QuizAttempt` → recomendación (empezar B0 / hacer pre-curso / recursos externos).
- Pre-curso como curso gratuito publicado (`isFreePreview` en todas sus lecciones): sirve de lead magnet.
- Panel de resultados en `/cuenta`: intentos y evolución.

## Fuera de alcance
Quizzes de evaluación al final de cada bloque (backlog, mismo componente).

## Criterios de aceptación
- Los 3 veredictos se reproducen con los umbrales originales (≥9, 5–8, <5).
- Un usuario anónimo puede hacer el quiz; al registrarse se asocia el intento (cookie temporal).
- Pre-curso completo accesible sin suscripción.

## Dependencias
T-07.
