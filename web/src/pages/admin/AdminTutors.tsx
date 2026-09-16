import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconCheck, IconPencil, IconPlus } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/**
 * Profesores de tutorías y lo que se les debe.
 *
 * Lo devengado NO se edita aquí. Cada fila sale de una tutoría dada y guarda con qué base y qué
 * porcentaje se calculó; lo único que se hace desde esta pantalla es marcar como pagado lo que
 * ya se ha transferido. Poder retocar el importe convertiría la liquidación en una hoja suelta
 * en la que nadie podría comprobar de dónde salió una cifra.
 */
type Tutor = {
  id: string;
  userId: string | null;
  displayName: string;
  email: string;
  bio: string;
  commissionPercent: number;
  isActive: boolean;
  pendingCents: number;
  paidCents: number;
  currency: string;
  sessionsGiven: number;
  minutesGiven: number;
};

type Earning = {
  id: string;
  sessionId: string;
  studentName: string;
  occurredAt: string;
  minutes: number;
  topic: string;
  baseCents: number;
  percent: number;
  amountCents: number;
  currency: string;
  isPaid: boolean;
  paidAt: string | null;
  payoutNote: string;
};

type TutorDetail = { tutor: Tutor; earnings: Earning[] };

const money = (cents: number, currency = 'EUR') =>
  (cents / 100).toLocaleString('es-ES', { style: 'currency', currency });

const shortDate = (iso: string) => new Date(iso).toLocaleDateString('es-ES');

function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  if (hours === 0) return `${rest} min`;
  if (rest === 0) return `${hours} h`;
  return `${hours} h ${rest} min`;
}

export function AdminTutors() {
  const tutors = useApi<Tutor[]>('/admin/tutors', []);
  const [editing, setEditing] = useState<Tutor | 'new' | null>(null);
  const [open, setOpen] = useState<string | null>(null);

  if (tutors.loading) {
    return <Spinner label="Cargando profesores…" />;
  }

  if (tutors.error) {
    return <ErrorMessage>{tutors.error.message}</ErrorMessage>;
  }

  const list = tutors.data ?? [];
  const owed = list.reduce((total, t) => total + t.pendingCents, 0);

  return (
    <section>
      <div className="admin__panel-head">
        <h2>Profesores</h2>

        <button
          type="button"
          className="btn btn--primary btn--sm btn--icon-text"
          onClick={() => setEditing('new')}
        >
          <IconPlus /> Nuevo profesor
        </button>
      </div>

      <p className="muted">
        Cada tutoría apuntada a un profesor le devenga su porcentaje sobre lo que el alumno pagó
        por esas horas, prorrateado por la duración de la clase. Las bolsas concedidas a mano no
        generaron ingreso, así que devengan cero.
      </p>

      {editing !== null && (
        <TutorForm
          tutor={editing === 'new' ? null : editing}
          onDone={() => {
            setEditing(null);
            tutors.reload();
          }}
          onCancel={() => setEditing(null)}
        />
      )}

      {list.length === 0 ? (
        <p className="muted">Todavía no hay profesores dados de alta.</p>
      ) : (
        <>
          <div className="table-scroll">
            <table>
              <caption className="sr-only">Profesores y lo que se les debe</caption>
              <thead>
                <tr>
                  <th scope="col">Profesor</th>
                  <th scope="col">Comisión</th>
                  <th scope="col">Impartido</th>
                  <th scope="col">Pendiente</th>
                  <th scope="col">Pagado</th>
                  <th scope="col">
                    <span className="sr-only">Acciones</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {list.map((tutor) => (
                  <tr key={tutor.id}>
                    <td>
                      <strong>{tutor.displayName}</strong>
                      {!tutor.isActive && <> · <span className="muted">retirado</span></>}
                      <br />
                      <span className="muted">{tutor.email}</span>
                    </td>
                    <td>{tutor.commissionPercent}%</td>
                    <td>
                      {tutor.sessionsGiven === 0
                        ? '—'
                        : `${tutor.sessionsGiven} · ${formatMinutes(tutor.minutesGiven)}`}
                    </td>
                    <td>
                      {tutor.pendingCents === 0 ? (
                        <span className="muted">—</span>
                      ) : (
                        <strong>{money(tutor.pendingCents, tutor.currency)}</strong>
                      )}
                    </td>
                    <td className="muted">{money(tutor.paidCents, tutor.currency)}</td>
                    <td>
                      <div className="row">
                        <button
                          type="button"
                          className="btn btn--ghost btn--sm btn--icon-text"
                          onClick={() => setEditing(tutor)}
                        >
                          <IconPencil /> Editar
                        </button>

                        <button
                          type="button"
                          className="btn btn--ghost btn--sm"
                          aria-expanded={open === tutor.id}
                          onClick={() => setOpen(open === tutor.id ? null : tutor.id)}
                        >
                          {open === tutor.id ? 'Cerrar' : 'Liquidación'}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {owed > 0 && (
            <p className="muted">
              Pendiente de pagar en total: <strong>{money(owed)}</strong>.
            </p>
          )}
        </>
      )}

      {open !== null && <TutorLedger tutorId={open} onPaid={tutors.reload} />}
    </section>
  );
}

/**
 * El detalle de un profesor: cada tutoría que dio, con qué base y qué porcentaje se calculó, y
 * el botón para apuntar lo que ya se le ha transferido.
 */
function TutorLedger({ tutorId, onPaid }: { tutorId: string; onPaid: () => void }) {
  const detail = useApi<TutorDetail>(`/admin/tutors/${tutorId}`, [tutorId]);
  const [picked, setPicked] = useState<string[]>([]);
  const [note, setNote] = useState('');
  const { busy, problem, run } = useAction();

  if (detail.loading) {
    return <Spinner label="Cargando liquidación…" />;
  }

  if (detail.error || !detail.data) {
    return <ErrorMessage>{detail.error?.message ?? 'No se ha podido cargar.'}</ErrorMessage>;
  }

  const { tutor, earnings } = detail.data;
  const pending = earnings.filter((e) => !e.isPaid);
  const chosen = pending.filter((e) => picked.includes(e.id));
  const total = chosen.reduce((sum, e) => sum + e.amountCents, 0);

  const toggle = (id: string) =>
    setPicked((current) =>
      current.includes(id) ? current.filter((x) => x !== id) : [...current, id],
    );

  return (
    <div className="card admin__panel">
      <h3>{tutor.displayName}</h3>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      {earnings.length === 0 ? (
        <p className="muted">Todavía no ha dado ninguna tutoría.</p>
      ) : (
        <>
          <div className="table-scroll">
            <table>
              <caption className="sr-only">Tutorías dadas y lo devengado por cada una</caption>
              <thead>
                <tr>
                  <th scope="col">
                    <span className="sr-only">Seleccionar</span>
                  </th>
                  <th scope="col">Fecha</th>
                  <th scope="col">Alumno</th>
                  <th scope="col">Tutoría</th>
                  <th scope="col">Base</th>
                  <th scope="col">%</th>
                  <th scope="col">Importe</th>
                  <th scope="col">Estado</th>
                </tr>
              </thead>
              <tbody>
                {earnings.map((earning) => (
                  <tr key={earning.id}>
                    <td>
                      {earning.isPaid ? (
                        <IconCheck />
                      ) : (
                        <input
                          type="checkbox"
                          checked={picked.includes(earning.id)}
                          aria-label={`Incluir la tutoría de ${shortDate(earning.occurredAt)} en el pago`}
                          onChange={() => toggle(earning.id)}
                        />
                      )}
                    </td>
                    <td>{shortDate(earning.occurredAt)}</td>
                    <td>{earning.studentName}</td>
                    <td>
                      {formatMinutes(earning.minutes)}
                      <br />
                      <span className="muted">{earning.topic}</span>
                    </td>
                    <td>{money(earning.baseCents, earning.currency)}</td>
                    <td>{earning.percent}%</td>
                    <td>
                      <strong>{money(earning.amountCents, earning.currency)}</strong>
                    </td>
                    <td>
                      {earning.isPaid ? (
                        <span className="muted">
                          Pagado {earning.paidAt ? shortDate(earning.paidAt) : ''}
                          {earning.payoutNote && <> · {earning.payoutNote}</>}
                        </span>
                      ) : (
                        'Pendiente'
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {pending.length > 0 && (
            <form
              className="stack"
              onSubmit={(event) => {
                event.preventDefault();

                void run(
                  () =>
                    api.post(`/admin/tutors/${tutorId}/pay`, {
                      earningIds: picked,
                      note,
                    }),
                  () => {
                    setPicked([]);
                    setNote('');
                    detail.reload();
                    onPaid();
                  },
                );
              }}
            >
              <div className="field">
                <label htmlFor={`pay-note-${tutorId}`}>Referencia del pago</label>
                <input
                  id={`pay-note-${tutorId}`}
                  value={note}
                  maxLength={300}
                  placeholder="Transferencia 12/03, factura F-2026-014"
                  onChange={(event) => setNote(event.target.value)}
                />
                <p className="muted admin__hint">
                  Esto no mueve dinero: la transferencia se hace fuera y aquí solo se apunta que
                  ya está hecha.
                </p>
              </div>

              <div className="row">
                <button
                  type="submit"
                  className="btn btn--primary btn--sm"
                  disabled={busy || picked.length === 0}
                >
                  Marcar como pagado {total > 0 && `· ${money(total)}`}
                </button>

                <button
                  type="button"
                  className="btn btn--ghost btn--sm"
                  disabled={busy}
                  onClick={() => setPicked(pending.map((e) => e.id))}
                >
                  Seleccionar todo lo pendiente
                </button>
              </div>
            </form>
          )}
        </>
      )}
    </div>
  );
}

function TutorForm({
  tutor,
  onDone,
  onCancel,
}: {
  tutor: Tutor | null;
  onDone: () => void;
  onCancel: () => void;
}) {
  const [form, setForm] = useState({
    displayName: tutor?.displayName ?? '',
    email: tutor?.email ?? '',
    bio: tutor?.bio ?? '',
    commissionPercent: String(tutor?.commissionPercent ?? 50),
    isActive: tutor?.isActive ?? true,
  });

  const { busy, problem, run } = useAction();

  return (
    <form
      className="card admin__panel admin__form"
      onSubmit={(event) => {
        event.preventDefault();

        const body = {
          userId: tutor?.userId ?? null,
          displayName: form.displayName,
          email: form.email,
          bio: form.bio,
          // Con coma o con punto: lo teclea una persona, y "33,33" es lo natural en español.
          commissionPercent: Number(form.commissionPercent.replace(',', '.').trim()),
          isActive: form.isActive,
        };

        void run(
          () => (tutor ? api.put(`/admin/tutors/${tutor.id}`, body) : api.post('/admin/tutors', body)),
          onDone,
        );
      }}
    >
      <h3>{tutor ? 'Editar profesor' : 'Nuevo profesor'}</h3>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor="tutor-name">Nombre</label>
          <input
            id="tutor-name"
            value={form.displayName}
            required
            maxLength={120}
            onChange={(event) => setForm((f) => ({ ...f, displayName: event.target.value }))}
          />
        </div>

        <div className="field">
          <label htmlFor="tutor-email">Correo</label>
          <input
            id="tutor-email"
            type="email"
            value={form.email}
            required
            onChange={(event) => setForm((f) => ({ ...f, email: event.target.value }))}
          />
          <p className="muted admin__hint">
            Por aquí se le convoca a las tutorías y se le liquida. No hace falta que tenga cuenta
            en la academia.
          </p>
        </div>
      </div>

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor="tutor-percent">Comisión (%)</label>
          <input
            id="tutor-percent"
            type="text"
            inputMode="decimal"
            value={form.commissionPercent}
            required
            onChange={(event) =>
              setForm((f) => ({ ...f, commissionPercent: event.target.value }))
            }
          />
          <p className="muted admin__hint">
            Sobre lo que el alumno pagó por las horas que imparte. Cambiarlo no toca lo ya
            devengado.
          </p>
        </div>

        <div className="field">
          <label className="admin__flag" htmlFor="tutor-active">
            <input
              id="tutor-active"
              type="checkbox"
              checked={form.isActive}
              onChange={(event) => setForm((f) => ({ ...f, isActive: event.target.checked }))}
            />
            <span>Activo</span>
          </label>
          <p className="muted admin__hint">
            Los retirados no aparecen al apuntar una tutoría, pero conservan su histórico y lo
            que se les deba.
          </p>
        </div>
      </div>

      <div className="field">
        <label htmlFor="tutor-bio">Nota interna</label>
        <textarea
          id="tutor-bio"
          rows={2}
          value={form.bio}
          onChange={(event) => setForm((f) => ({ ...f, bio: event.target.value }))}
        />
      </div>

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          Guardar
        </button>

        <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </form>
  );
}

function useAction() {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const run = useCallback(async (action: () => Promise<unknown>, onDone?: () => void) => {
    setBusy(true);
    setProblem(null);

    try {
      await action();
      onDone?.();
    } catch (caught) {
      setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido guardar.');
    } finally {
      setBusy(false);
    }
  }, []);

  return { busy, problem, run };
}
