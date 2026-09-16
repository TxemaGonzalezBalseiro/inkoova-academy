import { useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconAlert, IconCheck, IconRefresh } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/**
 * Facturación fiscal: el estado de Veri*Factu.
 *
 * Lo que se enseña aquí no es el listado de facturas —eso lo tiene cada alumno en su cuenta—
 * sino lo que puede salir mal sin que nadie se entere: que falte el NIF y no se esté emitiendo,
 * que la cadena de registros esté rota, o que haya asientos sin remitir a la AEAT.
 *
 * Nada de esto se puede corregir editando: un registro no se modifica nunca. Lo único que se
 * hace desde aquí es reintentar la remisión.
 */
type ChainLink = {
  recordId: string;
  seriesNumber: string;
  issueDate: string;
  generatedAt: string;
  /** 'alta' o 'anulacion'. */
  kind: string;
  totalCents: number;
  currency: string;
  hash: string;
  previousHash: string;
  /** 'pending', 'sent', 'rejected' o 'not_required'. */
  submissionState: string;
  submissionError: string;
  hashIsIntact: boolean;
  chainsFromPrevious: boolean;
};

type Chain = {
  issuerTaxId: string;
  records: number;
  isIntact: boolean;
  pending: number;
  rejected: number;
  broken: ChainLink[];
  links: ChainLink[];
};

type Status = {
  submissionEnabled: boolean;
  /** El bloque «sistema informático» del registro. Sin él la AEAT rechaza el registro entero. */
  softwareDeclared: boolean;
  canSubmit: boolean;
  canIssue: boolean;
  issuerProblem: string | null;
  issuer: { name: string; taxId: string; address: string } | null;
  chains: Chain[];
};

const euros = (cents: number, currency = 'EUR') =>
  (cents / 100).toLocaleString('es-ES', { style: 'currency', currency });

const shortDate = (iso: string) => new Date(iso).toLocaleDateString('es-ES');

const STATE_LABEL: Record<string, string> = {
  sent: 'Remitido',
  pending: 'Pendiente',
  rejected: 'Rechazado',
  not_required: 'Sin remitir',
};

export function AdminInvoicing() {
  const status = useApi<Status>('/admin/facturacion/estado', []);
  const [open, setOpen] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  if (status.loading) {
    return <Spinner label="Cargando facturación…" />;
  }

  if (status.error || !status.data) {
    return <ErrorMessage>{status.error?.message ?? 'No se ha podido cargar.'}</ErrorMessage>;
  }

  const data = status.data;
  const broken = data.chains.filter((c) => !c.isIntact);
  const pending = data.chains.reduce((total, c) => total + c.pending + c.rejected, 0);

  async function retry() {
    setBusy(true);
    setProblem(null);
    setMessage(null);

    try {
      const result = await api.post<{ sent: number }>('/admin/facturacion/reintentar');
      setMessage(
        result.sent === 0
          ? 'No se ha podido remitir nada. Mira el motivo de cada registro rechazado.'
          : `${result.sent} registros remitidos.`,
      );
      status.reload();
    } catch (caught) {
      setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido reintentar.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <h2>Facturación</h2>

      <p className="muted">
        Veri*Factu no regula el PDF: regula el <strong>registro de facturación</strong>, un
        asiento por cada factura, encadenado con el anterior y remitido a la AEAT. Aquí se ve si
        esa cadena está intacta y qué falta por remitir.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}
      {message && (
        <div className="alert alert--success" role="status">
          {message}
        </div>
      )}

      {/* Una cadena rota es lo más grave que puede salir aquí, y no se ve si hay que abrir
          cada emisor para encontrarlo. */}
      {broken.length > 0 && (
        <div className="card admin__panel admin__alert-warn">
          <h3>
            <IconAlert /> Hay una cadena de registros rota
          </h3>
          <p>
            {broken.map((c) => c.issuerTaxId).join(', ')}. Un registro de facturación no se
            modifica nunca después de encadenado: si no cuadra, algo ha tocado la base de datos
            por debajo. Está detallado más abajo, eslabón a eslabón.
          </p>
        </div>
      )}

      {/* Lo que impide facturar va primero: sin esto, cada cobro pasa sin factura. */}
      {!data.canIssue && (
        <div className="card admin__panel admin__alert-warn">
          <h3>
            <IconAlert /> No se está facturando
          </h3>
          <p>{data.issuerProblem}</p>
          <p className="muted">
            Cada cobro que entre mientras tanto se cobra y da acceso, pero no genera factura. Hay
            que emitirlas luego a mano.
          </p>
        </div>
      )}

      <div className="card admin__panel">
        <h3>Emisor</h3>

        {data.issuer ? (
          <dl className="admin__facts">
            <dt>Razón social</dt>
            <dd>{data.issuer.name}</dd>
            <dt>NIF</dt>
            <dd>{data.issuer.taxId}</dd>
            <dt>Domicilio</dt>
            <dd>{data.issuer.address || <span className="muted">sin indicar</span>}</dd>
          </dl>
        ) : (
          <p className="muted">
            Sale de los datos legales de la marca principal. Se edita en Marcas → Datos legales.
          </p>
        )}
      </div>

      <div className="card admin__panel">
        <div className="admin__panel-head">
          <h3>Remisión a la AEAT</h3>

          {data.submissionEnabled && (
            <button
              type="button"
              className="btn btn--ghost btn--sm btn--icon-text"
              disabled={busy || pending === 0}
              onClick={() => void retry()}
            >
              <IconRefresh /> Reintentar {pending > 0 && `(${pending})`}
            </button>
          )}
        </div>

        {data.submissionEnabled && !data.softwareDeclared && (
          <ErrorMessage>
            Falta describir el sistema informático (<code>Academy:Verifactu:Software</code>). El
            registro lo exige aparte del emisor, y sin ese bloque la AEAT lo rechaza entero. Sale
            de la declaración responsable del software.
          </ErrorMessage>
        )}

        {data.submissionEnabled ? (
          <p className="muted">
            {pending === 0
              ? 'Todo lo emitido está remitido.'
              : `${pending} registros pendientes o rechazados.`}
          </p>
        ) : (
          <>
            <p>
              <span className="badge">No conectada</span>
            </p>
            <p className="muted">
              La numeración, el encadenamiento, la huella y el PDF funcionan y son verificables.
              Lo que falta para declarar es la librería Veri*Factu y el certificado cualificado:
              el formato exacto del registro y de la firma lo fija la AEAT y no se puede
              aproximar. Los asientos quedan guardados y encadenados, listos para remitirse el
              día que se conecte.
            </p>
          </>
        )}
      </div>

      {data.chains.length === 0 ? (
        <p className="muted">Todavía no se ha emitido ninguna factura.</p>
      ) : (
        data.chains.map((chain) => (
          <div key={chain.issuerTaxId} className="card admin__panel">
            <div className="admin__panel-head">
              <h3>
                Cadena de {chain.issuerTaxId}{' '}
                {chain.isIntact ? (
                  <span className="badge">
                    <IconCheck /> Íntegra
                  </span>
                ) : (
                  <span className="badge">
                    <IconAlert /> Rota
                  </span>
                )}
              </h3>

              <button
                type="button"
                className="btn btn--ghost btn--sm"
                aria-expanded={open === chain.issuerTaxId}
                onClick={() => setOpen(open === chain.issuerTaxId ? null : chain.issuerTaxId)}
              >
                {open === chain.issuerTaxId ? 'Cerrar' : `Ver los ${chain.records}`}
              </button>
            </div>

            {!chain.isIntact && (
              <ErrorMessage>
                {chain.broken.length === 1
                  ? `El registro ${chain.broken[0].seriesNumber} no cuadra.`
                  : `${chain.broken.length} registros no cuadran, desde ${chain.broken[0].seriesNumber}.`}{' '}
                Alguien ha modificado la base de datos por debajo: un registro no se toca después
                de encadenado.
              </ErrorMessage>
            )}

            {open === chain.issuerTaxId && (
              <div className="table-scroll">
                <table>
                  <caption className="sr-only">Registros de facturación de {chain.issuerTaxId}</caption>
                  <thead>
                    <tr>
                      <th scope="col">Factura</th>
                      <th scope="col">Fecha</th>
                      <th scope="col">Tipo</th>
                      <th scope="col">Importe</th>
                      <th scope="col">Huella</th>
                      <th scope="col">Cadena</th>
                      <th scope="col">AEAT</th>
                    </tr>
                  </thead>
                  <tbody>
                    {chain.links.map((link) => (
                      <tr key={link.recordId}>
                        <td>{link.seriesNumber}</td>
                        <td>{shortDate(link.issueDate)}</td>
                        <td>{link.kind === 'anulacion' ? 'Anulación' : 'Alta'}</td>
                        <td>{euros(link.totalCents, link.currency)}</td>
                        <td>
                          <code title={link.hash}>{link.hash.slice(0, 12)}…</code>
                        </td>
                        <td>
                          {link.hashIsIntact && link.chainsFromPrevious ? (
                            <IconCheck />
                          ) : (
                            <span className="badge">
                              {!link.hashIsIntact ? 'Huella alterada' : 'No encadena'}
                            </span>
                          )}
                        </td>
                        <td>
                          {STATE_LABEL[link.submissionState] ?? link.submissionState}
                          {link.submissionError && (
                            <p className="muted">{link.submissionError}</p>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        ))
      )}
    </section>
  );
}
