import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorMessage, Spinner } from '../components/common';
import { formatDate } from '../lib/format';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import type { MySubscription } from '../lib/types';
import './account.css';

const STATUS_LABEL: Record<string, string> = {
  active: 'Activa',
  pastdue: 'Pago pendiente',
  canceled: 'Cancelada',
  unpaid: 'Impagada',
  none: 'Sin suscripción',
};

export function SubscriptionPage() {
  useDocumentTitle('Mi suscripción');

  const { data, error, loading } = useApi<MySubscription>('/me/subscription');
  const [portalError, setPortalError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function openPortal() {
    setBusy(true);
    setPortalError(null);

    try {
      const { url } = await api.get<{ url: string }>('/billing/portal');
      window.location.assign(url);
    } catch (caught) {
      setPortalError(
        caught instanceof ApiError ? caught.message : 'No hemos podido abrir el portal de pagos.',
      );
      setBusy(false);
    }
  }

  if (loading) {
    return <Spinner />;
  }

  if (error || !data) {
    return (
      <div className="container section">
        <ErrorMessage>No hemos podido cargar tu suscripción.</ErrorMessage>
      </div>
    );
  }

  return (
    <div className="container section account">
      <header className="section-header">
        <h1>Mi suscripción</h1>
        <p className="muted">
          <Link to="/cuenta">← Volver a mi cuenta</Link>
        </p>
      </header>

      {portalError && <ErrorMessage>{portalError}</ErrorMessage>}

      {data.status === 'pastdue' && (
        <div className="alert alert--error" role="alert">
          Tu último pago no se ha completado. Mantienes el acceso unos días mientras lo
          solucionas; actualiza el método de pago desde el portal.
        </div>
      )}

      {data.cancelAtPeriodEnd && (
        <div className="alert alert--info">
          Tu suscripción está cancelada y no se renovará. Conservas el acceso hasta el{' '}
          {formatDate(data.currentPeriodEnd)}.
        </div>
      )}

      <dl className="subscription__summary">
        <div>
          <dt>Plan</dt>
          <dd>{data.planName ?? 'Ninguno'}</dd>
        </div>
        <div>
          <dt>Estado</dt>
          <dd>
            <span className={`status-pill status-pill--${data.status}`}>
              {STATUS_LABEL[data.status] ?? data.status}
            </span>
          </dd>
        </div>
        <div>
          <dt>{data.cancelAtPeriodEnd ? 'Acceso hasta' : 'Próxima renovación'}</dt>
          <dd>{formatDate(data.currentPeriodEnd)}</dd>
        </div>
        <div>
          <dt>Packs sectoriales</dt>
          <dd>{data.includesPacks ? 'Incluidos' : 'No incluidos'}</dd>
        </div>
      </dl>

      <div className="row">
        {data.planCode ? (
          <button type="button" className="btn btn--primary" onClick={() => void openPortal()} disabled={busy}>
            {busy ? 'Abriendo…' : 'Gestionar pago y facturación'}
          </button>
        ) : (
          <Link to="/precios" className="btn btn--accent">
            Elegir un plan
          </Link>
        )}

        <Link to="/cuenta/facturas" className="btn btn--ghost">
          Ver facturas
        </Link>
      </div>

      <section>
        <h2>En propiedad</h2>
        <p className="muted">
          Cursos y packs que has comprado por separado. No dependen de la suscripción: siguen
          siendo tuyos aunque la canceles.
        </p>

        {data.ownedProductSlugs.length === 0 ? (
          <p className="muted">Ninguno todavía.</p>
        ) : (
          <ul>
            {data.ownedProductSlugs.map((slug) => (
              <li key={slug}>
                <Link to={`/curso/${slug}`}>{slug}</Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
