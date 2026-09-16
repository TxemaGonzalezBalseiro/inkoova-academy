import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { EmptyState, ErrorMessage, Spinner } from '../components/common';
import { RichText } from '../components/RichText';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import type { Quiz, QuizResult } from '../lib/types';
import './quiz.css';

/**
 * Test de nivel (T-08). La corrección la hace el servidor: el cliente no recibe cuál es la
 * respuesta correcta hasta que envía la suya, al contrario que el nivel.html original, que
 * traía las respuestas en el propio JavaScript.
 */
export function QuizPage() {
  const { slug = '' } = useParams();
  const { user } = useAuth();
  const { data: quiz, error, loading } = useApi<Quiz>(`/quizzes/${slug}`, [slug]);

  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [current, setCurrent] = useState(0);
  const [result, setResult] = useState<QuizResult | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  useDocumentTitle(quiz?.title ?? 'Test de nivel');

  if (loading) {
    return <Spinner />;
  }

  if (error?.status === 404) {
    return (
      <div className="container section">
        <EmptyState title="Ese cuestionario no existe">
          <Link to="/cursos">Ver cursos</Link>
        </EmptyState>
      </div>
    );
  }

  if (error || !quiz) {
    return (
      <div className="container section">
        <ErrorMessage>No hemos podido cargar el cuestionario.</ErrorMessage>
      </div>
    );
  }

  if (result) {
    return <QuizResultView quiz={quiz} result={result} isLoggedIn={Boolean(user)} />;
  }

  const question = quiz.questions[current];
  const answered = answers[question.id] !== undefined;
  const isLast = current === quiz.questions.length - 1;

  async function submit() {
    setSubmitting(true);
    setSubmitError(null);

    try {
      const payload = Object.entries(answers).map(([questionId, selectedIndex]) => ({
        questionId,
        selectedIndex,
      }));

      setResult(await api.post<QuizResult>(`/quizzes/${slug}/submit`, { answers: payload }));
    } catch (caught) {
      setSubmitError(
        caught instanceof ApiError ? caught.message : 'No hemos podido corregir el test.',
      );
      setSubmitting(false);
    }
  }

  return (
    <div className="container section quiz">
      <header className="quiz__header">
        <h1>{quiz.title}</h1>
        <p className="muted">
          {quiz.questions.length} preguntas · unos {Math.ceil(quiz.questions.length * 0.6)} minutos ·
          resultado inmediato
        </p>
      </header>

      {submitError && <ErrorMessage>{submitError}</ErrorMessage>}

      <div className="quiz__progress">
        <div className="progress__track" aria-hidden="true">
          <div
            className="progress__fill"
            style={{ width: `${(current / quiz.questions.length) * 100}%` }}
          />
        </div>
        <p className="muted" role="status" aria-live="polite">
          Pregunta {current + 1} de {quiz.questions.length}
        </p>
      </div>

      <fieldset className="quiz__card">
        <legend className="quiz__category">{question.category}</legend>

        {/*
          El enunciado trae marcado del contenido original: <em>, <strong> y <code>. Se
          reconocen esas tres y ninguna más, y lo que no encaje sale como texto. Ver RichText:
          antes esto era dangerouslySetInnerHTML sobre un campo de la base de datos.
        */}
        <h2 className="quiz__question">
          <RichText text={question.text} />
        </h2>

        <div className="quiz__options" role="radiogroup" aria-label="Opciones de respuesta">
          {question.options.map((option) => (
            <label
              key={option.index}
              className={`quiz__option ${answers[question.id] === option.index ? 'is-selected' : ''}`}
            >
              <input
                type="radio"
                name={question.id}
                value={option.index}
                checked={answers[question.id] === option.index}
                onChange={() => setAnswers((previous) => ({ ...previous, [question.id]: option.index }))}
              />
              <span className="quiz__letter" aria-hidden="true">
                {String.fromCharCode(65 + option.index)}
              </span>
              <span>{option.text}</span>
            </label>
          ))}
        </div>

        <div className="quiz__actions">
          <button
            type="button"
            className="btn btn--ghost"
            disabled={current === 0}
            onClick={() => setCurrent((value) => value - 1)}
          >
            ← Anterior
          </button>

          {isLast ? (
            <button
              type="button"
              className="btn btn--accent"
              disabled={!answered || submitting}
              onClick={() => void submit()}
            >
              {submitting ? 'Corrigiendo…' : 'Ver resultado'}
            </button>
          ) : (
            <button
              type="button"
              className="btn btn--primary"
              disabled={!answered}
              onClick={() => setCurrent((value) => value + 1)}
            >
              Siguiente →
            </button>
          )}
        </div>
      </fieldset>
    </div>
  );
}

function QuizResultView({
  quiz,
  result,
  isLoggedIn,
}: {
  quiz: Quiz;
  result: QuizResult;
  isLoggedIn: boolean;
}) {
  return (
    <div className="container section quiz">
      <div className={`quiz__verdict quiz__verdict--${result.verdict}`}>
        <p className="quiz__score">
          {result.score}/{result.total}
        </p>
        <h1>{result.headline}</h1>
        <p>{result.recommendation}</p>

        <div className="row">
          {result.verdict === 'ready' && (
            <Link to="/cursos" className="btn btn--accent">
              Ir al curso
            </Link>
          )}
          {/* El slug sigue siendo el de siempre aunque el curso se llame ya de otra forma: es
              la URL pública y la referencia de los accesos ya concedidos. */}
          {result.verdict === 'almost' && (
            <Link to="/curso/pre-curso-agent-engineering" className="btn btn--accent">
              Empezar Fundamentos de IA Engineer
            </Link>
          )}
          <Link to="/roadmap" className="btn btn--ghost">
            Ver el roadmap
          </Link>
        </div>

        {!isLoggedIn && (
          <p className="muted quiz__claim">
            Crea una cuenta y este resultado queda guardado en tu perfil.{' '}
            <Link to="/registro">Crear cuenta</Link>
          </p>
        )}
      </div>

      <section>
        <h2>Por dimensión</h2>
        <ul className="quiz__breakdown">
          {result.byCategory.map((category) => {
            const ratio = category.correct / category.total;
            const tone = ratio === 1 ? 'full' : ratio >= 0.5 ? 'partial' : 'weak';

            return (
              <li key={category.category}>
                <span>{category.category}</span>
                <span className={`quiz__cat-score quiz__cat-score--${tone}`}>
                  {category.correct}/{category.total}
                </span>
              </li>
            );
          })}
        </ul>
      </section>

      <section>
        <h2>Respuestas</h2>
        <ol className="quiz__feedback">
          {quiz.questions.map((question) => {
            const feedback = result.feedback.find((item) => item.questionId === question.id);

            return (
              <li key={question.id} className={feedback?.wasCorrect ? 'is-correct' : 'is-wrong'}>
                <p className="quiz__feedback-question">
                  <RichText text={question.text} />
                </p>
                <p className="muted quiz__feedback-answer">
                  <strong>
                    {(feedback?.correctIndexes.length ?? 0) > 1 ? 'Válidas:' : 'Correcta:'}
                  </strong>{' '}
                  {question.options
                    .filter((option) => feedback?.correctIndexes.includes(option.index))
                    .map((option) => option.text)
                    .join(' · ')}
                </p>
                <p className="quiz__feedback-explanation">{feedback?.explanation}</p>
              </li>
            );
          })}
        </ol>
      </section>
    </div>
  );
}
