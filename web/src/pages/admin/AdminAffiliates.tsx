import { useState } from 'react';
import { ErrorMessage } from '../../components/common';
import { api, ApiError } from '../../lib/api';

/** Alta de afiliados y ejecución manual de la liquidación mensual (T-16). */
export function AdminAffiliates() {
  const [email, setEmail] = useState('');
  const [code, setCode] = useState('');
  const [percent, setPercent] = useState(20);
  const [recurringMonths, setRecurringMonths] = useState(12);

  const [periodStart, setPeriodStart] = useState(defaultPeriodStart());
  const [periodEnd, setPeriodEnd] = useState(defaultPeriodEnd());

  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function createAffiliate(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);

    try {
      const created = await api.post<{ id: string; code: string }>('/admin/affiliates', {
        email,
        code,
        commissionPercent: percent,
        recurringMonths,
      });

      setMessage(`Afiliado ${created.code} creado y activado.`);
      setEmail('');
      setCode('');
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No se ha podido crear el afiliado.');
    } finally {
      setBusy(false);
    }
  }

  async function settle() {
    setBusy(true);
    setError(null);
    setMessage(null);

    try {
      const result = await api.post<{ payouts: string[] }>('/admin/affiliates/settle', {
        periodStart,
        periodEnd,
      });

      setMessage(
        result.payouts.length === 0
          ? 'No había nada que liquidar en ese periodo.'
          : `${result.payouts.length} liquidaciones generadas.`,
      );
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'La liquidación ha fallado.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <h2>Afiliados</h2>

      {error && <ErrorMessage>{error}</ErrorMessage>}
      {message && (
        <div className="alert alert--success" role="status">
          {message}
        </div>
      )}

      <div className="card admin__panel">
        <h3>Alta de afiliado</h3>
        <p className="muted">
          La persona debe tener ya una cuenta en la academia. Se le asigna el rol de afiliado y su
          panel queda disponible al instante.
        </p>

        <form onSubmit={createAffiliate} style={{ maxWidth: 460 }}>
          <div className="field">
            <label htmlFor="aff-email">Email de la cuenta</label>
            <input
              id="aff-email"
              type="email"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>

          <div className="field">
            <label htmlFor="aff-code">Código público</label>
            <input
              id="aff-code"
              type="text"
              required
              placeholder="TXEMA20"
              value={code}
              onChange={(event) => setCode(event.target.value.toUpperCase())}
            />
            <span className="hint">De 3 a 20 caracteres alfanuméricos. Aparece en el enlace y en Stripe.</span>
          </div>

          <div className="field">
            <label htmlFor="aff-percent">Comisión (%)</label>
            <input
              id="aff-percent"
              type="number"
              min={1}
              max={50}
              required
              value={percent}
              onChange={(event) => setPercent(Number(event.target.value))}
            />
            <span className="hint">Sobre la base neta: cobrado menos IVA, menos comisión de Stripe.</span>
          </div>

          <div className="field">
            <label htmlFor="aff-months">Meses de recurrencia</label>
            <input
              id="aff-months"
              type="number"
              min={0}
              max={36}
              required
              value={recurringMonths}
              onChange={(event) => setRecurringMonths(Number(event.target.value))}
            />
            <span className="hint">Renovaciones que también generan comisión. 0 = solo la primera venta.</span>
          </div>

          <button type="submit" className="btn btn--primary" disabled={busy}>
            Crear afiliado
          </button>
        </form>
      </div>

      <div className="card admin__panel">
        <h3>Liquidación mensual</h3>
        <p className="muted">
          El job la ejecuta sola el día 1. Este botón sirve para relanzarla: es idempotente, un
          periodo ya liquidado no se duplica.
        </p>

        <div className="row">
          <div className="field" style={{ marginBottom: 0 }}>
            <label htmlFor="period-start">Desde</label>
            <input
              id="period-start"
              type="date"
              value={periodStart}
              onChange={(event) => setPeriodStart(event.target.value)}
            />
          </div>

          <div className="field" style={{ marginBottom: 0 }}>
            <label htmlFor="period-end">Hasta</label>
            <input
              id="period-end"
              type="date"
              value={periodEnd}
              onChange={(event) => setPeriodEnd(event.target.value)}
            />
          </div>

          <button type="button" className="btn btn--ghost" disabled={busy} onClick={() => void settle()}>
            {busy ? 'Liquidando…' : 'Ejecutar liquidación'}
          </button>
        </div>
      </div>
    </section>
  );
}

/** Por defecto, el mes anterior completo: es el periodo que toca liquidar el día 1. */
function defaultPeriodStart(): string {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 1, 1)).toISOString().slice(0, 10);
}

function defaultPeriodEnd(): string {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 0)).toISOString().slice(0, 10);
}
