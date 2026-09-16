import { useMemo, useState } from 'react';
import { CourseCard, EmptyState, ErrorMessage, Spinner } from '../components/common';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import type { CourseCard as CourseCardType } from '../lib/types';

const LEVELS = [
  { value: 'all', label: 'Todos los niveles' },
  { value: 'intro', label: 'Introducción' },
  { value: 'intermediate', label: 'Intermedio' },
  { value: 'advanced', label: 'Avanzado' },
] as const;

const STATUSES = [
  { value: 'all', label: 'Todos' },
  { value: 'published', label: 'Disponibles' },
  { value: 'comingsoon', label: 'Próximamente' },
] as const;

export function CoursesPage() {
  useDocumentTitle('Catálogo de cursos');

  const { data, error, loading } = useApi<CourseCardType[]>('/courses');
  const [level, setLevel] = useState<string>('all');
  const [status, setStatus] = useState<string>('all');

  const filtered = useMemo(
    () =>
      (data ?? []).filter(
        (course) =>
          (level === 'all' || course.level === level) &&
          (status === 'all' || course.status === status),
      ),
    [data, level, status],
  );

  return (
    <div className="container section">
      <header className="section-header">
        <h1>Cursos</h1>
        <p className="muted">
          Cuatro cursos que encajan como un programa: prompts, agentes, producción y gobernanza.
        </p>
      </header>

      <div className="row" style={{ marginBottom: 'var(--space-8)' }}>
        <div className="field" style={{ marginBottom: 0 }}>
          <label htmlFor="filtro-nivel">Nivel</label>
          <select id="filtro-nivel" value={level} onChange={(event) => setLevel(event.target.value)}>
            {LEVELS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="field" style={{ marginBottom: 0 }}>
          <label htmlFor="filtro-estado">Estado</label>
          <select id="filtro-estado" value={status} onChange={(event) => setStatus(event.target.value)}>
            {STATUSES.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {loading && <Spinner />}
      {error && <ErrorMessage>No hemos podido cargar el catálogo.</ErrorMessage>}

      {/* El recuento se anuncia para que un lector de pantalla sepa que el filtro hizo algo. */}
      {!loading && !error && (
        <p className="sr-only" role="status" aria-live="polite">
          {filtered.length} cursos coinciden con el filtro.
        </p>
      )}

      {!loading && filtered.length === 0 && (
        <EmptyState title="Ningún curso coincide">
          <p>Prueba a quitar algún filtro.</p>
        </EmptyState>
      )}

      <div className="grid grid--cards">
        {filtered.map((course) => (
          <CourseCard key={course.slug} course={course} />
        ))}
      </div>
    </div>
  );
}
