import { ErrorMessage, Spinner } from '../../components/common';
import { useApi } from '../../hooks/useApi';

type AuditEntry = {
  id: string;
  actorUserId: string;
  action: string;
  entityType: string;
  entityId: string | null;
  detailsJson: string | null;
  occurredAt: string;
};

export function AdminAudit() {
  const { data, error, loading } = useApi<AuditEntry[]>('/admin/audit');

  if (loading) {
    return <Spinner />;
  }

  if (error || !data) {
    return <ErrorMessage>No hemos podido cargar la auditoría.</ErrorMessage>;
  }

  return (
    <section>
      <h2>Auditoría</h2>
      <p className="muted">
        Últimas 200 acciones administrativas. La tabla es de solo escritura: la aplicación no
        borra ni modifica nada de aquí.
      </p>

      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Cuándo</th>
              <th>Acción</th>
              <th>Entidad</th>
              <th>Detalle</th>
            </tr>
          </thead>
          <tbody>
            {data.map((entry) => (
              <tr key={entry.id}>
                <td className="muted" style={{ whiteSpace: 'nowrap' }}>
                  {new Intl.DateTimeFormat('es-ES', { dateStyle: 'short', timeStyle: 'medium' }).format(
                    new Date(entry.occurredAt),
                  )}
                </td>
                <td>
                  <code>{entry.action}</code>
                </td>
                <td className="muted">
                  {entry.entityType}
                  {entry.entityId && (
                    <>
                      <br />
                      <code style={{ fontSize: 'var(--text-xs)' }}>{entry.entityId}</code>
                    </>
                  )}
                </td>
                <td>
                  {entry.detailsJson ? (
                    <details>
                      <summary className="muted">Ver</summary>
                      <pre className="admin__json">{prettify(entry.detailsJson)}</pre>
                    </details>
                  ) : (
                    <span className="muted">—</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function prettify(json: string): string {
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    // El detalle no siempre es JSON (a veces es un texto suelto); se muestra tal cual.
    return json;
  }
}
