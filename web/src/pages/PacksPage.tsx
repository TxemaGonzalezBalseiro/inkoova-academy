import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { EmptyState, ErrorMessage, Spinner } from '../components/common';
import { formatBytes, formatDate, formatMoney } from '../lib/format';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import type { Pack } from '../lib/types';

export function PacksPage() {
  useDocumentTitle('Packs sectoriales');

  const { data, error, loading } = useApi<Pack[]>('/packs');

  return (
    <div className="container section">
      <header className="section-header">
        <h1>Packs sectoriales</h1>
        <p className="muted">
          Plantillas y checklists listos para usar en tu sector. Se actualizan cuando cambia la
          normativa, y las actualizaciones no se cobran aparte.
        </p>
      </header>

      {loading && <Spinner />}
      {error && <ErrorMessage>No hemos podido cargar los packs.</ErrorMessage>}

      {!loading && (data?.length ?? 0) === 0 && (
        <EmptyState title="Todavía no hay packs publicados">
          <p className="muted">El primero es el de seguros y financiero.</p>
        </EmptyState>
      )}

      <div className="grid grid--cards">
        {data?.map((pack) => (
          <article key={pack.slug} className="card" style={{ padding: 'var(--space-6)' }}>
            <p className="muted" style={{ fontSize: 'var(--text-xs)', textTransform: 'uppercase' }}>
              {pack.sector}
            </p>
            <h2 style={{ fontSize: 'var(--text-lg)' }}>
              <Link to={`/pack/${pack.slug}`}>{pack.title}</Link>
            </h2>
            <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
              Versión {pack.version}
              {pack.regulatoryCheckDate && ` · verificado el ${formatDate(pack.regulatoryCheckDate)}`}
            </p>
            <p>
              {pack.hasAccess ? (
                <span className="badge badge--featured">Incluido en tu plan</span>
              ) : pack.price !== null && pack.currency ? (
                <strong>{formatMoney(pack.price, pack.currency)}</strong>
              ) : (
                <span className="muted">Solo con plan</span>
              )}
            </p>
          </article>
        ))}
      </div>
    </div>
  );
}

export function PackDetailPage() {
  const { slug = '' } = useParams();
  const { user } = useAuth();
  const { data: pack, error, loading } = useApi<Pack>(`/packs/${slug}`, [slug]);

  useDocumentTitle(pack?.title ?? 'Pack');

  const [busy, setBusy] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  async function buy() {
    if (!user) {
      window.location.assign('/registro');
      return;
    }

    setBusy('checkout');
    setActionError(null);

    try {
      const { url } = await api.post<{ url: string }>(`/checkout/products/${slug}`);
      window.location.assign(url);
    } catch (caught) {
      setActionError(caught instanceof ApiError ? caught.message : 'No hemos podido iniciar el pago.');
      setBusy(null);
    }
  }

  /**
   * La descarga es en dos pasos: la API emite un token de 60 s tras comprobar el acceso, y
   * el navegador va después a por el fichero. Así el enlace no es compartible.
   */
  async function download(fileId: string, fileName: string) {
    setBusy(fileId);
    setActionError(null);

    try {
      const { token } = await api.post<{ token: string; fileName: string }>(
        `/packs/files/${fileId}/download`,
      );

      const anchor = document.createElement('a');
      anchor.href = `${api.baseUrl}/content/${token}`;
      anchor.download = fileName;
      anchor.click();
    } catch (caught) {
      setActionError(caught instanceof ApiError ? caught.message : 'No hemos podido preparar la descarga.');
    } finally {
      setBusy(null);
    }
  }

  if (loading) {
    return <Spinner />;
  }

  if (error || !pack) {
    return (
      <div className="container section">
        <EmptyState title="Ese pack no existe">
          <Link to="/packs">Ver todos los packs</Link>
        </EmptyState>
      </div>
    );
  }

  return (
    <div className="container section" style={{ maxWidth: 760 }}>
      <p className="muted" style={{ fontSize: 'var(--text-xs)', textTransform: 'uppercase' }}>
        {pack.sector}
      </p>
      <h1>{pack.title}</h1>

      <p className="muted">
        Versión <strong>{pack.version}</strong>
        {pack.regulatoryCheckDate && (
          <> · normativa verificada el {formatDate(pack.regulatoryCheckDate)}</>
        )}
      </p>

      {actionError && <ErrorMessage>{actionError}</ErrorMessage>}

      {pack.hasAccess ? (
        <>
          <div className="alert alert--success">
            Tienes acceso a este pack. Las versiones nuevas también, sin coste adicional.
          </div>

          <h2>Ficheros</h2>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Fichero</th>
                  <th>Tamaño</th>
                  <th>Descargar</th>
                </tr>
              </thead>
              <tbody>
                {pack.files.map((file) => (
                  <tr key={file.id}>
                    <td>{file.fileName}</td>
                    <td className="muted">{formatBytes(file.sizeInBytes)}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn--ghost btn--sm"
                        disabled={busy === file.id}
                        onClick={() => void download(file.id, file.fileName)}
                      >
                        {busy === file.id ? 'Preparando…' : 'Descargar'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <div className="card" style={{ padding: 'var(--space-6)', marginTop: 'var(--space-6)' }}>
          <h2 style={{ fontSize: 'var(--text-lg)' }}>Conseguir este pack</h2>
          <p className="muted">
            Incluido en los planes anual y vitalicio. También se puede comprar suelto y se queda
            tuyo para siempre, con sus actualizaciones.
          </p>

          <div className="row">
            {pack.price !== null && pack.currency && (
              <button type="button" className="btn btn--accent" disabled={busy !== null} onClick={() => void buy()}>
                {busy === 'checkout' ? 'Abriendo pago…' : `Comprar por ${formatMoney(pack.price, pack.currency)}`}
              </button>
            )}
            <Link to="/precios" className="btn btn--ghost">
              Ver planes que lo incluyen
            </Link>
          </div>
        </div>
      )}

      {pack.changelog && (
        <section style={{ marginTop: 'var(--space-12)' }}>
          <h2>Cambios en la versión {pack.version}</h2>
          <p className="muted" style={{ whiteSpace: 'pre-wrap' }}>
            {pack.changelog}
          </p>
        </section>
      )}

      <p className="muted" style={{ marginTop: 'var(--space-12)', fontSize: 'var(--text-sm)' }}>
        Estas plantillas son material formativo y no constituyen asesoramiento jurídico. Revísalas
        con tu asesoría antes de usarlas en un expediente real.
      </p>
    </div>
  );
}
