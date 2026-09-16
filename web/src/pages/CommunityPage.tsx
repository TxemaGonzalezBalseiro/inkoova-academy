import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { EmptyState, ErrorMessage, Spinner } from '../components/common';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';

type Session = {
  id: string;
  title: string;
  description: string;
  startsAt: string;
  durationMinutes: number;
  joinUrl: string;
};

export function CommunityPage() {
  useDocumentTitle('Comunidad');

  const { user } = useAuth();
  const [params] = useSearchParams();
  const { data: sessions, error, loading, reload } = useApi<Session[]>(user ? '/community/sessions' : null);

  const [linkError, setLinkError] = useState<string | null>(null);
  const [linked, setLinked] = useState(false);

  // Vuelta del OAuth de Discord: el código llega por query string (T-12).
  const code = params.get('code');
  const state = params.get('state');

  useEffect(() => {
    if (!code || !state) {
      return;
    }

    api
      .post('/community/discord/callback', { code, state })
      .then(() => {
        setLinked(true);
        reload();
      })
      .catch((caught: unknown) => {
        setLinkError(caught instanceof ApiError ? caught.message : 'No hemos podido vincular Discord.');
      });
  }, [code, state, reload]);

  async function startDiscordLink() {
    setLinkError(null);

    try {
      const { url } = await api.get<{ url: string }>('/community/discord/link');
      window.location.assign(url);
    } catch (caught) {
      setLinkError(caught instanceof ApiError ? caught.message : 'Discord no está disponible ahora mismo.');
    }
  }

  if (!user) {
    return (
      <div className="container section">
        <header className="section-header">
          <h1>Comunidad</h1>
          <p className="muted">
            Canal de Discord por curso, sesiones grupales y, en el plan superior, sesiones uno a
            uno. Todo eso vive detrás de la cuenta.
          </p>
        </header>

        <div className="row">
          <Link to="/registro" className="btn btn--accent">
            Crear cuenta
          </Link>
          <Link to="/precios" className="btn btn--ghost">
            Ver planes
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="container section">
      <header className="section-header">
        <h1>Comunidad</h1>
        <p className="muted">Lo que incluye tu plan, en un sitio.</p>
      </header>

      {linkError && <ErrorMessage>{linkError}</ErrorMessage>}
      {linked && (
        <div className="alert alert--success" role="status">
          Cuenta de Discord vinculada. El rol de tu plan se aplica en unos segundos.
        </div>
      )}

      <section style={{ marginBottom: 'var(--space-12)' }}>
        <h2>Discord</h2>
        <p className="muted">
          Vincula tu cuenta y el bot te da acceso a los canales de tu plan. Si la suscripción
          termina, el rol se retira automáticamente.
        </p>
        <button type="button" className="btn btn--primary" onClick={() => void startDiscordLink()}>
          Vincular mi Discord
        </button>
      </section>

      <section>
        <h2>Sesiones grupales</h2>

        {loading && <Spinner />}
        {error && <ErrorMessage>No hemos podido cargar el calendario.</ErrorMessage>}

        {!loading && (sessions?.length ?? 0) === 0 && (
          <EmptyState title="No hay sesiones programadas">
            <p className="muted">
              Las sesiones grupales son un beneficio de los planes trimestral en adelante. Si el
              tuyo las incluye, aparecerán aquí en cuanto se convoque la siguiente.
            </p>
          </EmptyState>
        )}

        <div className="grid grid--cards">
          {sessions?.map((session) => (
            <article key={session.id} className="card" style={{ padding: 'var(--space-6)' }}>
              <p className="muted" style={{ fontSize: 'var(--text-xs)' }}>
                {new Intl.DateTimeFormat('es-ES', { dateStyle: 'full', timeStyle: 'short' }).format(
                  new Date(session.startsAt),
                )}{' '}
                · {session.durationMinutes} min
              </p>
              <h3>{session.title}</h3>
              <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
                {session.description}
              </p>
              <a
                className="btn btn--ghost btn--sm"
                href={session.joinUrl}
                target="_blank"
                rel="noreferrer noopener"
              >
                Entrar a la sesión
              </a>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}
