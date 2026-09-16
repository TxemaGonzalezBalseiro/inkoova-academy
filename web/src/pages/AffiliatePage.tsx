import { useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState, ErrorMessage, Spinner } from '../components/common';
import { formatDate, formatMoney } from '../lib/format';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { openAuthenticatedFile } from '../lib/download';
import { api, ApiError } from '../lib/api';
import type { AffiliateDashboard } from '../lib/types';
import './account.css';

const COMMISSION_STATUS: Record<string, string> = {
  pending: 'Pendiente',
  approved: 'Aprobada',
  paid: 'Pagada',
  reversed: 'Revertida',
};

const PAYOUT_STATUS: Record<string, string> = {
  awaitinginvoice: 'Esperando factura',
  readytopay: 'Lista para pagar',
  paid: 'Pagada',
  cancelled: 'Cancelada',
};

/** Panel del afiliado (T-16). Solo ve sus propios datos: la API filtra por el token. */
export function AffiliatePage() {
  useDocumentTitle('Panel de afiliado');

  const { data, error, loading, reload } = useApi<AffiliateDashboard>('/affiliate');
  const [copied, setCopied] = useState(false);

  if (loading) {
    return <Spinner />;
  }

  if (error?.status === 404) {
    return (
      <div className="container section">
        <EmptyState title="No tienes perfil de afiliado">
          <p className="muted">
            El programa de afiliados es por invitación. Si te interesa,{' '}
            <Link to="/soporte">escríbenos</Link>.
          </p>
        </EmptyState>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="container section">
        <ErrorMessage>No hemos podido cargar tu panel.</ErrorMessage>
      </div>
    );
  }

  return (
    <div className="container section account">
      <header className="section-header">
        <h1>Panel de afiliado</h1>
        <p className="muted">
          Código <code>{data.code}</code> · {data.commissionPercent} % sobre la base neta ·
          recurrente {data.recurringMonths} meses
        </p>
      </header>

      <section>
        <h2>Tu enlace</h2>
        <div className="row">
          <input
            readOnly
            value={data.referralUrl}
            aria-label="Tu enlace de referido"
            onFocus={(event) => event.currentTarget.select()}
            style={{ flex: '1 1 320px', padding: 'var(--space-3)', border: '1px solid var(--border)', borderRadius: 'var(--radius)' }}
          />
          <button
            type="button"
            className="btn btn--ghost"
            onClick={() => {
              void navigator.clipboard.writeText(data.referralUrl);
              setCopied(true);
              window.setTimeout(() => setCopied(false), 2000);
            }}
          >
            {copied ? 'Copiado' : 'Copiar'}
          </button>
        </div>
        <p className="muted" style={{ fontSize: 'var(--text-sm)', marginTop: 'var(--space-2)' }}>
          La atribución dura 30 días desde el último clic. Un código de descuento tuyo usado en el
          pago tiene prioridad sobre la cookie.
        </p>
      </section>

      <section>
        <h2>Resumen</h2>
        <dl className="subscription__summary">
          <div>
            <dt>Clics este mes</dt>
            <dd>{data.clicksThisMonth}</dd>
          </div>
          <div>
            <dt>Conversiones</dt>
            <dd>{data.conversions}</dd>
          </div>
          <div>
            <dt>Pendiente</dt>
            <dd>{formatMoney(data.pendingAmount, data.currency)}</dd>
          </div>
          <div>
            <dt>Aprobado</dt>
            <dd>{formatMoney(data.approvedAmount, data.currency)}</dd>
          </div>
          <div>
            <dt>Pagado</dt>
            <dd>{formatMoney(data.paidAmount, data.currency)}</dd>
          </div>
        </dl>
        <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
          Una comisión pasa de pendiente a aprobada a los 14 días de la venta, cuando cierra la
          ventana de desistimiento. Un reembolso en ese plazo la revierte.
        </p>
      </section>

      <TaxDataForm onSaved={reload} />

      <section>
        <h2>Comisiones recientes</h2>

        {data.recentCommissions.length === 0 ? (
          <p className="muted">Todavía no hay ninguna.</p>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Producto</th>
                  <th>Base neta</th>
                  <th>Comisión</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {data.recentCommissions.map((commission, index) => (
                  <tr key={`${commission.date}-${index}`}>
                    <td>{formatDate(commission.date)}</td>
                    <td>{commission.product}</td>
                    <td className="muted">{formatMoney(commission.netBase, data.currency)}</td>
                    <td>
                      <strong>{formatMoney(commission.amount, data.currency)}</strong>
                    </td>
                    <td>{COMMISSION_STATUS[commission.status] ?? commission.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section>
        <h2>Liquidaciones</h2>

        {data.payouts.length === 0 ? (
          <p className="muted">
            La primera liquidación se genera el día 1 del mes siguiente al de tu primera venta
            aprobada. El mínimo de pago es de 50 €; por debajo, el saldo se arrastra.
          </p>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Periodo</th>
                  <th>Total</th>
                  <th>Estado</th>
                  <th>Documento</th>
                </tr>
              </thead>
              <tbody>
                {data.payouts.map((payout) => (
                  <tr key={payout.id}>
                    <td>
                      {formatDate(payout.periodStart)} — {formatDate(payout.periodEnd)}
                    </td>
                    <td>
                      <strong>{formatMoney(payout.total, data.currency)}</strong>
                    </td>
                    <td>{PAYOUT_STATUS[payout.status] ?? payout.status}</td>
                    <td>
                      {payout.hasStatement ? (
                        /*
                          No es un enlace: el endpoint exige sesión y una navegación normal llega
                          sin la cabecera del token, así que devolvía 401 en vez del PDF.
                        */
                        <button
                          type="button"
                          className="btn btn--ghost btn--sm"
                          onClick={() =>
                            void openAuthenticatedFile(
                              `/affiliate/payouts/${payout.id}/statement`,
                              `liquidacion-${payout.id}.pdf`,
                            ).catch(() => undefined)
                          }
                        >
                          PDF
                        </button>
                      ) : (
                        <span className="muted">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

function TaxDataForm({ onSaved }: { onSaved: () => void }) {
  const [taxId, setTaxId] = useState('');
  const [countryCode, setCountryCode] = useState('ES');
  const [iban, setIban] = useState('');
  const [state, setState] = useState<'idle' | 'saving' | 'done'>('idle');
  const [error, setError] = useState<string | null>(null);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setState('saving');
    setError(null);

    try {
      await api.put('/affiliate/tax-data', { taxId, countryCode, iban });
      setState('done');
      onSaved();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No hemos podido guardar los datos.');
      setState('idle');
    }
  }

  return (
    <section>
      <h2>Datos fiscales</h2>
      <p className="muted">
        Sin estos datos no se puede generar una liquidación. Solo se usan para pagarte y para la
        facturación.
      </p>

      {error && <ErrorMessage>{error}</ErrorMessage>}
      {state === 'done' && (
        <div className="alert alert--success" role="status">
          Datos guardados.
        </div>
      )}

      <form onSubmit={submit} style={{ maxWidth: 460 }}>
        <div className="field">
          <label htmlFor="tax-id">NIF / VAT</label>
          <input id="tax-id" type="text" required value={taxId} onChange={(e) => setTaxId(e.target.value)} />
        </div>

        <div className="field">
          <label htmlFor="country">País</label>
          <input
            id="country"
            type="text"
            required
            maxLength={2}
            value={countryCode}
            onChange={(e) => setCountryCode(e.target.value.toUpperCase())}
          />
          <span className="hint">Código ISO de dos letras, por ejemplo ES.</span>
        </div>

        <div className="field">
          <label htmlFor="iban">IBAN</label>
          <input id="iban" type="text" required value={iban} onChange={(e) => setIban(e.target.value)} />
        </div>

        <button type="submit" className="btn btn--primary" disabled={state === 'saving'}>
          {state === 'saving' ? 'Guardando…' : 'Guardar datos fiscales'}
        </button>
      </form>
    </section>
  );
}
