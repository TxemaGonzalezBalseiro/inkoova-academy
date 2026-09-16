import { Link } from 'react-router-dom';
import { EmptyState, ErrorMessage, Spinner } from '../components/common';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { useAuth } from '../lib/useAuth';
import type { RoadmapNode } from '../lib/types';
import './roadmap.css';

const STATE_LABEL: Record<RoadmapNode['state'], string> = {
  locked: 'Bloqueado',
  available: 'Disponible',
  in_progress: 'En curso',
  completed: 'Completado',
};

/**
 * Roadmap (T-09). El itinerario se dibuja como una lista ordenada con conectores CSS en vez
 * de un SVG: una lista ya es la alternativa accesible que pedía la tarea, y así no hay dos
 * representaciones que mantener sincronizadas.
 */
export function RoadmapPage() {
  useDocumentTitle('Roadmap de aprendizaje');

  const { user } = useAuth();
  const { data: nodes, error, loading } = useApi<RoadmapNode[]>('/roadmap');

  return (
    <div className="container section">
      <header className="section-header">
        <h1>Roadmap de aprendizaje</h1>
        <p className="muted">
          El orden en que las piezas encajan. Puedes saltártelo, pero está pensado para que cada
          curso se apoye en el anterior.
        </p>
      </header>

      {loading && <Spinner />}
      {error && <ErrorMessage>No hemos podido cargar el roadmap.</ErrorMessage>}

      {!loading && nodes?.length === 0 && (
        <EmptyState title="El roadmap todavía no está configurado">
          <p className="muted">Se edita desde el panel de administración.</p>
        </EmptyState>
      )}

      {nodes && nodes.length > 0 && (
        <ol className="roadmap">
          {nodes.map((node) => (
            <li key={node.key} className={`roadmap__node roadmap__node--${node.state}`}>
              <div className="roadmap__marker" aria-hidden="true">
                {node.state === 'completed' ? '✓' : node.state === 'locked' ? '🔒' : '●'}
              </div>

              <div className="roadmap__body">
                <div className="row">
                  <h2>{node.title}</h2>
                  <span className={`badge roadmap__badge roadmap__badge--${node.state}`}>
                    {STATE_LABEL[node.state]}
                  </span>
                </div>

                <p className="muted">{node.description}</p>

                {node.prerequisites.length > 0 && (
                  <p className="muted roadmap__prereq">
                    Antes: {node.prerequisites.join(', ')}
                  </p>
                )}

                {node.courseSlug && (
                  <Link to={`/curso/${node.courseSlug}`} className="btn btn--ghost btn--sm">
                    {node.state === 'in_progress' ? 'Continuar' : 'Ver el curso'}
                  </Link>
                )}
              </div>
            </li>
          ))}
        </ol>
      )}

      {!user && nodes && nodes.length > 0 && (
        <div className="roadmap__cta card">
          <h2>Sigue tu progreso</h2>
          <p className="muted">
            Con una cuenta, el roadmap marca por dónde vas y qué tienes desbloqueado.
          </p>
          <Link to="/registro" className="btn btn--accent">
            Crear cuenta gratis
          </Link>
        </div>
      )}
    </div>
  );
}
