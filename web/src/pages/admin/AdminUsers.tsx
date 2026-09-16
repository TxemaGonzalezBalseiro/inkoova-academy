import { useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { formatDate } from '../../lib/format';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

type AdminUser = {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  createdAt: string;
  plan: string | null;
  subscriptionStatus: string | null;
  currentPeriodEnd: string | null;
};

export function AdminUsers() {
  const [term, setTerm] = useState('');
  const [users, setUsers] = useState<AdminUser[] | null>(null);
  const [selected, setSelected] = useState<AdminUser | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function search(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      setUsers(await api.get<AdminUser[]>(`/admin/users?q=${encodeURIComponent(term)}`));
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'La búsqueda ha fallado.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <h2>Usuarios</h2>

      <form onSubmit={search} className="row" style={{ marginBottom: 'var(--space-6)' }}>
        <div className="field" style={{ marginBottom: 0, flex: '1 1 280px' }}>
          <label htmlFor="user-search">Buscar por email o nombre</label>
          <input
            id="user-search"
            type="search"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
          />
        </div>
        <button type="submit" className="btn btn--primary" disabled={busy}>
          {busy ? 'Buscando…' : 'Buscar'}
        </button>
      </form>

      {error && <ErrorMessage>{error}</ErrorMessage>}
      {busy && <Spinner />}

      {users && users.length === 0 && <p className="muted">Ningún usuario coincide.</p>}

      {users && users.length > 0 && (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Usuario</th>
                <th>Alta</th>
                <th>Plan</th>
                <th>Acceso hasta</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr key={user.id}>
                  <td>
                    <strong>{user.displayName}</strong>
                    <br />
                    <span className="muted">{user.email}</span>
                    {!user.emailConfirmed && (
                      <span className="badge badge--soon" style={{ marginLeft: 'var(--space-2)' }}>
                        Sin confirmar
                      </span>
                    )}
                  </td>
                  <td className="muted">{formatDate(user.createdAt)}</td>
                  <td>{user.plan ?? <span className="muted">—</span>}</td>
                  <td className="muted">{formatDate(user.currentPeriodEnd)}</td>
                  <td>
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm"
                      onClick={() => setSelected(selected?.id === user.id ? null : user)}
                    >
                      {selected?.id === user.id ? 'Cerrar' : 'Accesos'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selected && <GrantForm user={selected} />}
      {selected && <CertificateForm user={selected} />}
    </section>
  );
}

/**
 * Emisión manual de un certificado, para cuando el proceso normal falló: un fallo al generar el
 * PDF, un alumno que terminó el curso antes de que existieran los certificados, un correo que
 * nunca salió.
 *
 * **No salta la comprobación de progreso.** El servidor sigue exigiendo el 100 % de las clases
 * obligatorias, así que esto no sirve para regalar un certificado a quien no ha cursado. Si el
 * alumno no ha terminado, la respuesta lo dice y el certificado no se emite.
 *
 * Es idempotente: pedirlo dos veces devuelve el mismo código, no dos certificados.
 */
function CertificateForm({ user }: { user: AdminUser }) {
  const [courseSlug, setCourseSlug] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function issue() {
    setBusy(true);
    setError(null);
    setMessage(null);

    try {
      const result = await api.post<{ code: string }>(`/admin/users/${user.id}/certificates`, {
        courseSlug,
      });
      setMessage(`Certificado ${result.code} emitido.`);
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'No se ha podido emitir el certificado.',
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="card admin__panel">
      <h3>Certificados de {user.displayName}</h3>

      <p className="muted">
        Para reintentar una emisión que falló. Sigue exigiendo el curso completo: si al alumno le
        falta alguna clase obligatoria, no se emite.
      </p>

      {error && <ErrorMessage>{error}</ErrorMessage>}
      {message && (
        <div className="alert alert--success" role="status">
          {message}
        </div>
      )}

      <div className="field">
        <label htmlFor="cert-course">Curso</label>
        <input
          id="cert-course"
          type="text"
          placeholder="agent-engineering-v3"
          value={courseSlug}
          onChange={(event) => setCourseSlug(event.target.value)}
        />
        <span className="hint">Slug del curso.</span>
      </div>

      <button
        type="button"
        className="btn btn--primary"
        disabled={busy || !courseSlug}
        onClick={() => void issue()}
      >
        {busy ? 'Emitiendo…' : 'Emitir certificado'}
      </button>
    </div>
  );
}

function GrantForm({ user }: { user: AdminUser }) {
  const [productSlug, setProductSlug] = useState('');
  const [note, setNote] = useState('');
  const [validUntil, setValidUntil] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function grant() {
    setBusy(true);
    setError(null);
    setMessage(null);

    try {
      await api.post(`/admin/users/${user.id}/grant`, {
        productSlug,
        validUntil: validUntil ? new Date(validUntil).toISOString() : null,
        note,
      });
      setMessage(`Acceso a ${productSlug} concedido.`);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No se ha podido conceder el acceso.');
    } finally {
      setBusy(false);
    }
  }

  async function revoke() {
    setBusy(true);
    setError(null);
    setMessage(null);

    try {
      await api.post(`/admin/users/${user.id}/revoke`, { productSlug, reason: note || 'revocación manual' });
      setMessage(`Acceso a ${productSlug} revocado.`);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No se ha podido revocar el acceso.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="card admin__panel">
      <h3>Accesos de {user.displayName}</h3>

      {error && <ErrorMessage>{error}</ErrorMessage>}
      {message && (
        <div className="alert alert--success" role="status">
          {message}
        </div>
      )}

      <div className="field">
        <label htmlFor="grant-product">Producto</label>
        <input
          id="grant-product"
          type="text"
          placeholder="agent-engineering-v3"
          value={productSlug}
          onChange={(event) => setProductSlug(event.target.value)}
        />
        <span className="hint">Slug del curso, pack o programa.</span>
      </div>

      <div className="field">
        <label htmlFor="grant-until">Válido hasta (opcional)</label>
        <input
          id="grant-until"
          type="date"
          value={validUntil}
          onChange={(event) => setValidUntil(event.target.value)}
        />
        <span className="hint">Vacío significa acceso permanente.</span>
      </div>

      <div className="field">
        <label htmlFor="grant-note">Motivo</label>
        <input
          id="grant-note"
          type="text"
          required
          placeholder="Beca, alumno de empresa, compensación por incidencia…"
          value={note}
          onChange={(event) => setNote(event.target.value)}
        />
        <span className="hint">Obligatorio para un acceso manual: queda en la auditoría.</span>
      </div>

      <div className="row">
        <button
          type="button"
          className="btn btn--primary"
          disabled={busy || !productSlug || !note}
          onClick={() => void grant()}
        >
          Conceder acceso
        </button>
        <button
          type="button"
          className="btn btn--ghost"
          disabled={busy || !productSlug}
          onClick={() => void revoke()}
        >
          Revocar
        </button>
      </div>

      <PackAccess user={user} />
    </div>
  );
}

/** Un pack publicado y el acceso que tiene un alumno a él. */
type PackAccessRow = {
  slug: string;
  title: string;
  sector: string;
  hasAccess: boolean;
  /** 'purchase', 'planincluded' o 'manual'. Nulo si no tiene ninguna fila propia. */
  source: string | null;
};

/**
 * Los packs con un botón cada uno.
 *
 * El formulario de arriba sirve para cualquier producto, pero obliga a teclear el slug. Con seis
 * packs eso son seis oportunidades de escribir «pack-administracion-publica» con una errata, y
 * un slug mal escrito da «no existe ese producto» sin decir cuál era el bueno.
 *
 * Lo que da el PLAN no se toca desde aquí: revocarlo a mano lo devolvería el siguiente pase de
 * reconciliación, así que el botón se apaga y se dice por qué en vez de dejar pulsar en balde.
 */
function PackAccess({ user }: { user: AdminUser }) {
  const packs = useApi<PackAccessRow[]>(`/admin/users/${user.id}/packs`, [user.id]);
  const [busy, setBusy] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const list = packs.data ?? [];

  if (packs.loading || list.length === 0) {
    return null;
  }

  async function toggle(pack: PackAccessRow) {
    setBusy(pack.slug);
    setProblem(null);

    try {
      if (pack.hasAccess) {
        await api.post(`/admin/users/${user.id}/revoke`, {
          productSlug: pack.slug,
          reason: 'Retirado desde el panel de accesos',
        });
      } else {
        await api.post(`/admin/users/${user.id}/grant`, {
          productSlug: pack.slug,
          validUntil: null,
          note: 'Pack sectorial concedido desde el panel de accesos',
        });
      }

      packs.reload();
    } catch (caught) {
      setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido cambiar.');
    } finally {
      setBusy(null);
    }
  }

  const sinAcceso = list.filter((p) => !p.hasAccess);

  return (
    <section className="admin__section">
      <div className="admin__section-head">
        <h4>Packs sectoriales</h4>

        {sinAcceso.length > 0 && (
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={busy !== null}
            onClick={async () => {
              for (const pack of sinAcceso) {
                await toggle(pack);
              }
            }}
          >
            Dar los {sinAcceso.length} que faltan
          </button>
        )}
      </div>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <ul className="admin__history">
        {list.map((pack) => {
          // Lo que da el plan no se retira a mano: volvería solo.
          const delPlan = pack.hasAccess && pack.source === 'planincluded';

          return (
            <li key={pack.slug} className="admin__history-item">
              <div>
                <strong>{pack.title}</strong>
                <br />
                <span className="muted">
                  {pack.sector}
                  {pack.hasAccess && pack.source && <> · por {origen(pack.source)}</>}
                </span>
              </div>

              <button
                type="button"
                className={`btn btn--sm ${pack.hasAccess ? 'btn--ghost' : 'btn--primary'}`}
                disabled={busy !== null || delPlan}
                title={delPlan ? 'Lo incluye su plan: se retira cambiando el plan.' : undefined}
                onClick={() => void toggle(pack)}
              >
                {pack.hasAccess ? 'Quitar' : 'Dar acceso'}
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function origen(source: string): string {
  if (source === 'planincluded') return 'su plan';
  if (source === 'purchase') return 'compra';

  return 'acceso manual';
}
