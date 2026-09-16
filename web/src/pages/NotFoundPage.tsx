import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../hooks/useApi';

export function NotFoundPage() {
  useDocumentTitle('Página no encontrada');

  return (
    <div className="container section" style={{ textAlign: 'center', paddingBlock: 'var(--space-24)' }}>
      <p className="muted" style={{ fontSize: 'var(--text-4xl)', fontWeight: 800, margin: 0 }}>
        404
      </p>
      <h1>Esta página no existe</h1>
      <p className="muted">Puede que el enlace esté roto o que el contenido haya cambiado de sitio.</p>

      <div className="row" style={{ justifyContent: 'center', marginTop: 'var(--space-6)' }}>
        <Link to="/" className="btn btn--primary">
          Ir al inicio
        </Link>
        <Link to="/cursos" className="btn btn--ghost">
          Ver los cursos
        </Link>
      </div>
    </div>
  );
}
