import { Link } from 'react-router-dom';
import { EmptyState, ErrorMessage, Spinner } from '../components/common';
import { formatDate, formatMoney } from '../lib/format';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { openAuthenticatedFile } from '../lib/download';
import './account.css';

type Invoice = {
  number: string;
  issueDate: string;
  total: number;
  tax: number;
  currency: string;
  isRectification: boolean;
  hasPdf: boolean;
};

export function InvoicesPage() {
  useDocumentTitle('Mis facturas');

  const { data, error, loading } = useApi<Invoice[]>('/me/invoices');

  return (
    <div className="container section account">
      <header className="section-header">
        <h1>Facturas</h1>
        <p className="muted">
          <Link to="/cuenta">← Volver a mi cuenta</Link>
        </p>
      </header>

      {loading && <Spinner />}
      {error && <ErrorMessage>No hemos podido cargar tus facturas.</ErrorMessage>}

      {!loading && (data?.length ?? 0) === 0 && (
        <EmptyState title="Todavía no hay facturas">
          <p className="muted">Se emiten automáticamente con cada pago.</p>
        </EmptyState>
      )}

      {data && data.length > 0 && (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Número</th>
                <th>Fecha</th>
                <th>Base + IVA</th>
                <th>Total</th>
                <th>PDF</th>
              </tr>
            </thead>
            <tbody>
              {data.map((invoice) => (
                <tr key={invoice.number}>
                  <td>
                    <code>{invoice.number}</code>
                    {invoice.isRectification && (
                      <span className="badge badge--soon" style={{ marginLeft: 'var(--space-2)' }}>
                        Rectificativa
                      </span>
                    )}
                  </td>
                  <td>{formatDate(invoice.issueDate)}</td>
                  <td className="muted">
                    {formatMoney(invoice.total - invoice.tax, invoice.currency)} +{' '}
                    {formatMoney(invoice.tax, invoice.currency)}
                  </td>
                  <td>
                    <strong>{formatMoney(invoice.total, invoice.currency)}</strong>
                  </td>
                  <td>
                    {invoice.hasPdf ? (
                      /*
                        No es un enlace: el endpoint exige sesión y una navegación normal llega
                        sin la cabecera del token, así que devolvía 401 en vez del PDF.
                      */
                      <button
                        type="button"
                        className="btn btn--ghost btn--sm"
                        onClick={() =>
                          void openAuthenticatedFile(
                            `/me/invoices/${invoice.number}/pdf`,
                            `${invoice.number}.pdf`,
                          ).catch(() => undefined)
                        }
                      >
                        Descargar
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
    </div>
  );
}
