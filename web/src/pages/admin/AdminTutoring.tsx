import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconAlert, IconCheck, IconPencil, IconPlus, IconRefresh, IconTrash } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/**
 * Tutorías: el catálogo de paquetes y lo que tiene consumido cada alumno.
 *
 * El tiempo viaja en minutos y se enseña en horas. La conversión está en un sitio
 * (`formatMinutes` y `toMinutes`) porque repartirla por los formularios acaba en dos
 * redondeos distintos y en un saldo que no cuadra con el de la API.
 */
type TutoringPackage = {
  id: string;
  slug: string;
  name: string;
  description: string;
  minutes: number;
  priceCents: number;
  currency: string;
  /** 'fixed', 'subscription' o 'never'. */
  expiryMode: string;
  validityDays: number | null;
  displayOrder: number;
  isActive: boolean;
  stripePriceId: string | null;
};

type SyncResult = { slug: string; linked: boolean; priceId: string | null; problem: string | null };

type TutoringSession = {
  id: string;
  minutes: number;
  occurredAt: string;
  topic: string;
  notes: string;
  tutorId: string | null;
  /** Ya resuelto por la API: el histórico se lee sin tener a mano la lista de profesores. */
  tutorName: string | null;
};

/** Lo justo para elegir quién dio una tutoría. La ficha completa vive en su propia pestaña. */
type TutorOption = { id: string; displayName: string; commissionPercent: number; isActive: boolean };

/**
 * Una tutoría CONVOCADA. No ha descontado saldo todavía: lo descuenta al confirmarla, que es
 * cuando de verdad se ha dado.
 */
type Appointment = {
  id: string;
  grantId: string;
  tutorId: string | null;
  tutorName: string | null;
  startsAt: string;
  minutes: number;
  topic: string;
  notes: string;
  location: string;
  /** 'scheduled', 'done' o 'cancelled'. */
  status: string;
  sessionId: string | null;
  cancelledReason: string;
};

type TutoringGrant = {
  id: string;
  packageId: string | null;
  packageName: string;
  minutesTotal: number;
  minutesUsed: number;
  minutesRemaining: number;
  source: string;
  grantedAt: string;
  expiresAt: string | null;
  isExpired: boolean;
  isRevoked: boolean;
  revokedReason: string | null;
  note: string;
  sessions: TutoringSession[];
  appointments: Appointment[];
};

type Balance = {
  userId: string;
  displayName: string;
  email: string;
  minutesTotal: number;
  minutesUsed: number;
  minutesRemaining: number;
  activeGrants: number;
};

type StudentDetail = {
  userId: string;
  displayName: string;
  email: string;
  minutesRemaining: number;
  minutesTotal: number;
  minutesUsed: number;
  grants: TutoringGrant[];
};

type AdminUser = { id: string; email: string; displayName: string };

/** "5 h 30 min". Nunca decimales: el saldo es aritmética entera y así se lee. */
function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  if (hours === 0) return `${rest} min`;
  if (rest === 0) return `${hours} h`;
  return `${hours} h ${rest} min`;
}

/**
 * Número tecleado por una persona en español: "450", "450,50" y "450.50" valen los tres.
 *
 * Va con `type="text"` y no con `type="number"` a propósito. Un campo numérico rechaza la coma
 * en silencio —el valor llega vacío al enviar, sin decir por qué— y sus flechas se mueven de
 * uno en uno según el `step`, que con céntimos obliga a cien pulsaciones por euro.
 */
const parseDecimal = (value: string): number => Number(value.replace(',', '.').trim());

/** Horas en decimal (1,5) a minutos exactos. El redondeo ocurre aquí y solo aquí. */
const toMinutes = (hours: number) => Math.round(hours * 60);

const euros = (cents: number) =>
  (cents / 100).toLocaleString('es-ES', { style: 'currency', currency: 'EUR' });

const shortDate = (iso: string) => new Date(iso).toLocaleDateString('es-ES');

/** "14/09/2026, 17:00", en la hora de quien mira. La API la manda siempre en UTC. */
const longMoment = (iso: string) =>
  new Date(iso).toLocaleString('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });

/**
 * La fecha y la hora locales para rellenar los campos del formulario.
 *
 * `toISOString()` no vale: pasa a UTC y una tutoría de las 00:30 aparecería el día anterior a
 * las 22:30. Se leen los componentes locales, que es lo que el campo espera.
 */
const pad = (value: number) => String(value).padStart(2, '0');

const localDate = (moment: Date) =>
  `${moment.getFullYear()}-${pad(moment.getMonth() + 1)}-${pad(moment.getDate())}`;

const localTime = (moment: Date) => `${pad(moment.getHours())}:${pad(moment.getMinutes())}`;

export function AdminTutoring() {
  const packages = useApi<TutoringPackage[]>('/admin/tutoring/packages', []);
  const balances = useApi<Balance[]>('/admin/tutoring/balances', []);
  const [selected, setSelected] = useState<string | null>(null);

  if (packages.loading || balances.loading) {
    return <Spinner label="Cargando tutorías…" />;
  }

  if (packages.error) {
    return <ErrorMessage>{packages.error.message}</ErrorMessage>;
  }

  const reloadAll = () => {
    balances.reload();
    packages.reload();
  };

  return (
    <>
      <AgendaPanel onOpenStudent={setSelected} />

      <PackagesPanel packages={packages.data ?? []} onChanged={packages.reload} />

      <GrantPanel packages={packages.data ?? []} onGranted={reloadAll} />

      <BalancesPanel
        balances={balances.data ?? []}
        selected={selected}
        onSelect={setSelected}
        onChanged={reloadAll}
      />
    </>
  );
}

// ── agenda ──────────────────────────────────────────────────────────────────────────────

/**
 * Lo convocado y todavía sin apuntar, de todos los alumnos.
 *
 * Va lo primero porque es lo único de esta pantalla que caduca: una tutoría que se dio ayer y
 * que nadie ha apuntado no descuenta saldo y no le paga al profesor. Enseña también las de
 * ayer, que es justo cuando hay que acordarse.
 */
function AgendaPanel({ onOpenStudent }: { onOpenStudent: (userId: string) => void }) {
  const agenda = useApi<
    {
      appointment: Appointment;
      studentId: string;
      studentName: string;
      studentEmail: string;
      /** Lo decide el servidor: es el mismo reloj con el que se guardó la cita. */
      isOverdue: boolean;
    }[]
  >('/admin/tutoring/appointments', []);

  const rows = agenda.data ?? [];
  if (agenda.loading || rows.length === 0) {
    return null;
  }

  return (
    <section className="card admin__panel">
      <div className="admin__panel-head">
        <h2>Agenda</h2>
        <span className="muted">{rows.length} convocadas</span>
      </div>

      <ul className="admin__history">
        {rows.map(({ appointment, studentId, studentName, isOverdue }) => {
          return (
            <li key={appointment.id} className="admin__history-item">
              <div>
                <strong>{longMoment(appointment.startsAt)}</strong> ·{' '}
                {formatMinutes(appointment.minutes)}
                {isOverdue && (
                  <>
                    {' '}
                    <span className="badge">Sin apuntar</span>
                  </>
                )}
                <br />
                {studentName} · {appointment.topic}
                {appointment.tutorName && <> · {appointment.tutorName}</>}
              </div>

              <button
                type="button"
                className="btn btn--ghost btn--sm"
                onClick={() => onOpenStudent(studentId)}
              >
                Ver ficha
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

// ── catálogo ────────────────────────────────────────────────────────────────────────────

function PackagesPanel({
  packages,
  onChanged,
}: {
  packages: TutoringPackage[];
  onChanged: () => void;
}) {
  const [editing, setEditing] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [sync, setSync] = useState<SyncResult[] | null>(null);
  const { busy, problem, run } = useAction();

  // Un paquete a la venta sin precio en Stripe no se puede cobrar. Se avisa arriba en vez de
  // esperar a que alguien mire la columna: es la diferencia entre venderlo y no venderlo.
  const unlinked = packages.filter((pack) => pack.isActive && !pack.stripePriceId && pack.priceCents > 0);

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Paquetes de tutorías</h2>

        <div className="row">
          <button
            type="button"
            className="btn btn--ghost btn--sm btn--icon-text"
            disabled={busy}
            onClick={() =>
              void run(
                () => api.post<SyncResult[]>('/admin/tutoring/stripe/sync'),
                (result) => {
                  setSync(result as SyncResult[]);
                  onChanged();
                },
              )
            }
          >
            <IconRefresh /> Sincronizar con Stripe
          </button>

          <button
            type="button"
            className="btn btn--ghost btn--sm btn--icon-text"
            onClick={() => {
              setCreating(true);
              setEditing(null);
            }}
          >
            <IconPlus /> Nuevo paquete
          </button>
        </div>
      </div>

      <p className="muted">
        Lo que se vende: horas y precio. Editar un paquete no cambia lo que ya tiene concedido
        ningún alumno, porque cada concesión guardó sus horas en el momento de darse.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      {unlinked.length > 0 && (
        <p className="alert admin__alert-warn">
          <IconAlert />
          <span>
            {unlinked.length === 1
              ? 'Hay un paquete a la venta sin precio en Stripe: no se puede cobrar hasta sincronizar.'
              : `Hay ${unlinked.length} paquetes a la venta sin precio en Stripe: no se pueden cobrar hasta sincronizar.`}
          </span>
        </p>
      )}

      {sync && (
        <ul className="admin__checks">
          {sync.map((item) => (
            <li key={item.slug}>
              {item.linked ? <IconCheck /> : <IconAlert />} {item.slug}
              {item.problem ? ` — ${item.problem}` : item.priceId ? ` — ${item.priceId}` : ''}
            </li>
          ))}
        </ul>
      )}

      {creating && (
        <PackageForm
          onCancel={() => setCreating(false)}
          onDone={() => {
            setCreating(false);
            onChanged();
          }}
        />
      )}

      {packages.length === 0 && !creating ? (
        <p className="muted">Todavía no hay paquetes. Crea el primero para poder venderlos.</p>
      ) : (
        <div className="table-scroll">
          <table >
            <thead>
              <tr>
                <th scope="col">Paquete</th>
                <th scope="col">Tiempo</th>
                <th scope="col">Importe</th>
                <th scope="col">Validez</th>
                <th scope="col">Estado</th>
                <th scope="col">Stripe</th>
                <th scope="col">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>

            <tbody>
              {packages.map((pack) =>
                editing === pack.id ? (
                  <tr key={pack.id}>
                    <td colSpan={7}>
                      <PackageForm
                        pack={pack}
                        onCancel={() => setEditing(null)}
                        onDone={() => {
                          setEditing(null);
                          onChanged();
                        }}
                      />
                    </td>
                  </tr>
                ) : (
                  <tr key={pack.id} >
                    <td>
                      <strong>{pack.name}</strong>
                      <br />
                      <span className="muted">{pack.slug}</span>
                    </td>
                    <td>{formatMinutes(pack.minutes)}</td>
                    <td>{euros(pack.priceCents)}</td>
                    <td>{expiryLabel(pack)}</td>
                    <td>{pack.isActive ? 'A la venta' : 'Retirado'}</td>
                    <td>
                      {pack.stripePriceId ? (
                        <code>{pack.stripePriceId}</code>
                      ) : (
                        <span className="muted">Sin enlazar</span>
                      )}
                    </td>
                    <td>
                      <div className="row">
                        <button
                          type="button"
                          className="btn btn--ghost btn--sm btn--icon-text"
                          onClick={() => {
                            setEditing(pack.id);
                            setCreating(false);
                          }}
                        >
                          <IconPencil /> Editar
                        </button>

                        <button
                          type="button"
                          className="btn btn--ghost btn--sm"
                          disabled={busy}
                          onClick={() =>
                            void run(
                              () =>
                                api.post(`/admin/tutoring/packages/${pack.id}/active`, {
                                  active: !pack.isActive,
                                }),
                              onChanged,
                            )
                          }
                        >
                          {pack.isActive ? 'Retirar' : 'Reactivar'}
                        </button>
                      </div>
                    </td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function PackageForm({
  pack,
  onCancel,
  onDone,
}: {
  pack?: TutoringPackage;
  onCancel: () => void;
  onDone: () => void;
}) {
  const [form, setForm] = useState({
    slug: pack?.slug ?? '',
    name: pack?.name ?? '',
    description: pack?.description ?? '',
    hours: pack ? String(pack.minutes / 60) : '5',
    price: pack ? String(pack.priceCents / 100) : '',
    expiryMode: pack?.expiryMode ?? 'subscription',
    validityDays: pack?.validityDays === null || pack === undefined ? '' : String(pack.validityDays),
    displayOrder: String(pack?.displayOrder ?? 0),
  });

  const { busy, problem, run } = useAction();
  const set = (key: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      className="stack"
      onSubmit={(event) => {
        event.preventDefault();

        void run(
          () =>
            api.post('/admin/tutoring/packages', {
              id: pack?.id ?? null,
              slug: form.slug,
              name: form.name,
              description: form.description,
              minutes: toMinutes(parseDecimal(form.hours)),
              priceCents: Math.round(parseDecimal(form.price) * 100),
              currency: 'EUR',
              // Vacío significa "no caduca", que no es lo mismo que cero días.
              expiryMode: form.expiryMode,
              // Los días solo viajan en el modo de fecha: en los otros la fecha no la
              // pone el paquete y el servidor los descarta igual.
              validityDays:
                form.expiryMode === 'fixed' && form.validityDays.trim() !== ''
                  ? Number(form.validityDays)
                  : null,
              displayOrder: Number(form.displayOrder),
            }),
          onDone,
        );
      }}
    >
      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor="pack-name">Nombre</label>
          <input
            id="pack-name"
            value={form.name}
            required
            maxLength={160}
            onChange={(e) => set('name')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="pack-slug">Identificador</label>
          <input
            id="pack-slug"
            value={form.slug}
            required
            disabled={pack !== undefined}
            placeholder="pack-5-tutorias"
            onChange={(e) => set('slug')(e.target.value)}
          />
          <p className="muted admin__hint">
            {pack ? 'No se cambia: es la referencia estable del paquete.' : 'En minúsculas y con guiones.'}
          </p>
        </div>

        <div className="field">
          <label htmlFor="pack-hours">Horas incluidas</label>
          <input
            id="pack-hours"
            type="text"
            inputMode="decimal"
            value={form.hours}
            required
            onChange={(e) => set('hours')(e.target.value)}
          />
          <p className="muted admin__hint">
            Se guarda en minutos: {formatMinutes(toMinutes(parseDecimal(form.hours) || 0))}.
          </p>
        </div>

        <div className="field">
          <label htmlFor="pack-price">Importe (€)</label>
          <input
            id="pack-price"
            type="text"
            inputMode="decimal"
            placeholder="450"
            value={form.price}
            required
            onChange={(e) => set('price')(e.target.value)}
          />
          <p className="muted admin__hint">
            {form.price.trim() === '' || Number.isNaN(parseDecimal(form.price))
              ? 'Con coma o con punto: 450 o 450,50.'
              : `Se cobra ${euros(Math.round(parseDecimal(form.price) * 100))}.`}
          </p>
        </div>

        <div className="field">
          <label htmlFor="pack-expiry">Cuándo caduca</label>
          <select id="pack-expiry" value={form.expiryMode} onChange={(e) => set('expiryMode')(e.target.value)}>
            <option value="subscription">Con la suscripción del alumno</option>
            <option value="fixed">A los X días de concederse</option>
            <option value="never">No caduca</option>
          </select>
          <p className="muted admin__hint">
            {form.expiryMode === 'subscription'
              ? 'Las horas viven mientras el alumno siga suscrito. Si renueva siguen ahí; si deja de renovar, se pierden.'
              : form.expiryMode === 'never'
                ? 'No se pierden nunca.'
                : 'Cuenta desde el día en que se conceden.'}
          </p>
        </div>

        {form.expiryMode === 'fixed' && (
          <div className="field">
            <label htmlFor="pack-validity">Validez en días</label>
            <input
              id="pack-validity"
              type="number"
              min="1"
              value={form.validityDays}
              required
              onChange={(e) => set('validityDays')(e.target.value)}
            />
          </div>
        )}

        <div className="field">
          <label htmlFor="pack-order">Orden</label>
          <input
            id="pack-order"
            type="number"
            value={form.displayOrder}
            onChange={(e) => set('displayOrder')(e.target.value)}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor="pack-description">Descripción</label>
        <textarea
          id="pack-description"
          rows={2}
          value={form.description}
          onChange={(e) => set('description')(e.target.value)}
        />
      </div>

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          {pack ? 'Guardar' : 'Crear paquete'}
        </button>

        <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </form>
  );
}

// ── conceder ────────────────────────────────────────────────────────────────────────────

function GrantPanel({
  packages,
  onGranted,
}: {
  packages: TutoringPackage[];
  onGranted: () => void;
}) {
  const [term, setTerm] = useState('');
  const [found, setFound] = useState<AdminUser[]>([]);
  // Se distingue «no he buscado» de «he buscado y no hay nadie». Sin esto, una búsqueda sin
  // resultados deja la pantalla igual que antes de buscar y parece que el botón no funciona.
  const [searched, setSearched] = useState(false);
  const [student, setStudent] = useState<AdminUser | null>(null);
  const [packageId, setPackageId] = useState('');
  const [hours, setHours] = useState('1');
  const [source, setSource] = useState('purchase');
  const [note, setNote] = useState('');
  const [done, setDone] = useState<string | null>(null);
  const { busy, problem, run } = useAction();

  const active = packages.filter((p) => p.isActive);

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Conceder tutorías</h2>
      </div>

      <p className="muted">
        Busca al alumno y dale un paquete del catálogo, o un tiempo suelto si es un acuerdo
        concreto. Queda registrado con tu usuario y la hora.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}
      {done && (
        <p className="alert alert--success">
          <IconCheck /> {done}
        </p>
      )}

      <form
        className="row"
        onSubmit={(event) => {
          event.preventDefault();
          void run(
            () => api.get<AdminUser[]>(`/admin/users?q=${encodeURIComponent(term)}`),
            (result) => {
              setFound(result as AdminUser[]);
              setSearched(true);
            },
          );
        }}
      >
        <div className="field">
          <label htmlFor="tut-search">Alumno</label>
          <input
            id="tut-search"
            type="search"
            value={term}
            placeholder="Email o nombre"
            onChange={(e) => setTerm(e.target.value)}
          />
        </div>

        <button type="submit" className="btn btn--ghost btn--sm" disabled={busy || term.length < 2}>
          Buscar
        </button>
      </form>

      {searched && found.length === 0 && (
        <p className="muted">Ningún alumno coincide con «{term}».</p>
      )}

      {found.length > 0 && (
        /*
          Una lista de resultados, no una fila de etiquetas. `.chip` es un adorno informativo
          —texto diminuto, sin hover, sin foco— y aquí lo que hay que hacer es ELEGIR a una
          persona: hace falta ver el nombre y el correo separados, saber cuál está marcado y
          poder recorrerla con el teclado.
        */
        <ul className="admin__results" aria-label="Resultados de la búsqueda">
          {found.map((user) => (
            <li key={user.id}>
              <button
                type="button"
                className="admin__result"
                // `aria-pressed` y no una clase: quien no ve la pantalla necesita que el
                // control diga si está seleccionado, no que lo esté por dentro.
                aria-pressed={student?.id === user.id}
                onClick={() => setStudent(user)}
              >
                <span className="admin__result-avatar" aria-hidden="true">
                  {user.displayName.trim().charAt(0).toUpperCase()}
                </span>

                <span className="admin__result-text">
                  <strong>{user.displayName}</strong>
                  <span className="muted">{user.email}</span>
                </span>

                {student?.id === user.id && <IconCheck />}
              </button>
            </li>
          ))}
        </ul>
      )}

      {student && (
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();

            void run(
              () =>
                api.post('/admin/tutoring/grants', {
                  userId: student.id,
                  packageId: packageId === '' ? null : packageId,
                  minutes: packageId === '' ? toMinutes(parseDecimal(hours)) : null,
                  name: packageId === '' ? 'Tutorías' : null,
                  source,
                  expiresAt: null,
                  note,
                }),
              () => {
                setDone(`Tutorías concedidas a ${student.displayName}.`);
                setNote('');
                onGranted();
              },
            );
          }}
        >
          <div className="admin__form-grid">
            <div className="field">
              <label htmlFor="tut-package">Paquete</label>
              <select
                id="tut-package"
                value={packageId}
                onChange={(e) => setPackageId(e.target.value)}
              >
                <option value="">Tiempo suelto (sin paquete)</option>
                {active.map((pack) => (
                  <option key={pack.id} value={pack.id}>
                    {pack.name} · {formatMinutes(pack.minutes)} · {euros(pack.priceCents)}
                  </option>
                ))}
              </select>
            </div>

            {packageId === '' && (
              <div className="field">
                <label htmlFor="tut-hours">Horas</label>
                <input
                  id="tut-hours"
                  type="text"
                  inputMode="decimal"
                  value={hours}
                  onChange={(e) => setHours(e.target.value)}
                />
              </div>
            )}

            <div className="field">
              <label htmlFor="tut-source">Origen</label>
              <select id="tut-source" value={source} onChange={(e) => setSource(e.target.value)}>
                <option value="purchase">Compra</option>
                <option value="manual">Manual (beca, cortesía, acuerdo)</option>
              </select>
            </div>

            <div className="field">
              <label htmlFor="tut-note">Nota</label>
              <input
                id="tut-note"
                value={note}
                maxLength={300}
                onChange={(e) => setNote(e.target.value)}
              />
            </div>
          </div>

          <div className="row">
            <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
              Conceder a {student.displayName}
            </button>

            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStudent(null)}>
              Cambiar de alumno
            </button>
          </div>
        </form>
      )}
    </section>
  );
}

// ── saldos y consumo ────────────────────────────────────────────────────────────────────

function BalancesPanel({
  balances,
  selected,
  onSelect,
  onChanged,
}: {
  balances: Balance[];
  selected: string | null;
  onSelect: (id: string | null) => void;
  onChanged: () => void;
}) {
  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Alumnos con tutorías</h2>
      </div>

      {balances.length === 0 ? (
        <p className="muted">Todavía no hay tutorías concedidas.</p>
      ) : (
        <div className="table-scroll">
          <table >
            <thead>
              <tr>
                <th scope="col">Alumno</th>
                <th scope="col">Concedido</th>
                <th scope="col">Consumido</th>
                <th scope="col">Restante</th>
                <th scope="col">Bolsas vivas</th>
                <th scope="col">
                  <span className="sr-only">Detalle</span>
                </th>
              </tr>
            </thead>

            <tbody>
              {balances.map((row) => (
                <tr key={row.userId}>
                  <td>
                    <strong>{row.displayName}</strong>
                    <br />
                    <span className="muted">{row.email}</span>
                  </td>
                  <td>{formatMinutes(row.minutesTotal)}</td>
                  <td>{formatMinutes(row.minutesUsed)}</td>
                  <td>
                    <strong>{formatMinutes(row.minutesRemaining)}</strong>
                  </td>
                  <td>{row.activeGrants}</td>
                  <td>
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm"
                      onClick={() => onSelect(selected === row.userId ? null : row.userId)}
                    >
                      {selected === row.userId ? 'Cerrar' : 'Ver ficha'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selected && <StudentDetailPanel userId={selected} onChanged={onChanged} />}
    </section>
  );
}

function StudentDetailPanel({ userId, onChanged }: { userId: string; onChanged: () => void }) {
  const detail = useApi<StudentDetail>(`/admin/tutoring/students/${userId}`, [userId]);

  if (detail.loading) {
    return <Spinner label="Cargando ficha…" />;
  }

  if (detail.error) {
    return <ErrorMessage>{detail.error.message}</ErrorMessage>;
  }

  const student = detail.data;
  if (!student) {
    return null;
  }

  const refresh = () => {
    detail.reload();
    onChanged();
  };

  return (
    <div className="admin__section">
      <h3>
        {student.displayName} · {formatMinutes(student.minutesRemaining)} disponibles
      </h3>

      <p className="muted">
        Concedido {formatMinutes(student.minutesTotal)} · consumido{' '}
        {formatMinutes(student.minutesUsed)}
      </p>

      {student.grants.map((grant) => (
        <GrantCard key={grant.id} grant={grant} onChanged={refresh} />
      ))}
    </div>
  );
}

function GrantCard({ grant, onChanged }: { grant: TutoringGrant; onChanged: () => void }) {
  const [recording, setRecording] = useState(false);
  const [scheduling, setScheduling] = useState(false);
  const { busy, problem, run } = useAction();

  const pending = grant.appointments.filter((a) => a.status === 'scheduled');
  // Lo convocado y sin dar también ocupa: convocar cinco tutorías contra una bolsa de una hora
  // dejaría cuatro sin poder apuntarse.
  const booked = pending.reduce((total, a) => total + a.minutes, 0);

  const state = grant.isRevoked
    ? 'Revocada'
    : grant.isExpired
      ? 'Caducada'
      : grant.minutesRemaining === 0
        ? 'Agotada'
        : 'Disponible';

  return (
    <article className="card admin__section">
      <header className="admin__section-head">
        <div>
          <strong>{grant.packageName}</strong>{' '}
          <span className="badge">{grant.source === 'purchase' ? 'Compra' : 'Manual'}</span>{' '}
          <span className="badge">{state}</span>
          <p className="muted">
            {formatMinutes(grant.minutesRemaining)} de {formatMinutes(grant.minutesTotal)} ·
            concedida el {shortDate(grant.grantedAt)}
            {grant.expiresAt && ` · caduca el ${shortDate(grant.expiresAt)}`}
          </p>
          {grant.note && <p className="muted">{grant.note}</p>}
          {grant.revokedReason && <p className="muted">Motivo: {grant.revokedReason}</p>}
        </div>

        <div className="row">
          {!grant.isRevoked && (
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              onClick={() => setScheduling((value) => !value)}
            >
              Convocar
            </button>
          )}

          {!grant.isRevoked && (
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              onClick={() => setRecording((value) => !value)}
            >
              Apuntar tutoría
            </button>
          )}

          {!grant.isRevoked && (
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              disabled={busy}
              onClick={() => {
                const reason = window.prompt('Motivo de la revocación');
                if (reason === null) return;

                void run(
                  () => api.post(`/admin/tutoring/grants/${grant.id}/revoke`, { reason }),
                  onChanged,
                );
              }}
            >
              Revocar
            </button>
          )}
        </div>
      </header>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      {scheduling && (
        <AppointmentForm
          grantId={grant.id}
          appointment={null}
          remaining={grant.minutesRemaining - booked}
          onDone={() => {
            setScheduling(false);
            onChanged();
          }}
          onCancel={() => setScheduling(false)}
        />
      )}

      {grant.appointments.length > 0 && (
        <ul className="admin__history">
          {grant.appointments.map((appointment) => (
            <AppointmentRow
              key={appointment.id}
              appointment={appointment}
              remaining={grant.minutesRemaining - booked + appointment.minutes}
              onChanged={onChanged}
            />
          ))}
        </ul>
      )}

      {recording && (
        <SessionForm
          grantId={grant.id}
          remaining={grant.minutesRemaining}
          onDone={() => {
            setRecording(false);
            onChanged();
          }}
          onCancel={() => setRecording(false)}
        />
      )}

      {grant.sessions.length > 0 && (
        <ul className="admin__history">
          {grant.sessions.map((session) => (
            <li key={session.id} className="admin__history-item">
              <div>
                <strong>{formatMinutes(session.minutes)}</strong> · {shortDate(session.occurredAt)}
                {session.tutorName && <> · {session.tutorName}</>}
                <br />
                {session.topic}
                {session.notes && <p className="muted">{session.notes}</p>}
              </div>

              <button
                type="button"
                className="btn btn--ghost btn--sm btn--icon-text"
                disabled={busy}
                onClick={() =>
                  void run(
                    () =>
                      api.del(`/admin/tutoring/grants/${grant.id}/sessions/${session.id}`),
                    onChanged,
                  )
                }
              >
                <IconTrash /> Deshacer
              </button>
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}

/**
 * Una tutoría convocada, con lo que se puede hacer con ella: moverla, anularla o darla por
 * dada. Confirmarla es lo que descuenta el saldo y devenga lo del profesor.
 */
function AppointmentRow({
  appointment,
  remaining,
  onChanged,
}: {
  appointment: Appointment;
  remaining: number;
  onChanged: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const { busy, problem, run } = useAction();

  const state =
    appointment.status === 'done'
      ? 'Dada'
      : appointment.status === 'cancelled'
        ? 'Anulada'
        : 'Convocada';

  return (
    <li className="admin__history-item">
      <div>
        <strong>{longMoment(appointment.startsAt)}</strong> · {formatMinutes(appointment.minutes)}{' '}
        <span className="badge">{state}</span>
        <br />
        {appointment.topic}
        {appointment.tutorName && <> · {appointment.tutorName}</>}
        {appointment.location && <p className="muted">{appointment.location}</p>}
        {appointment.cancelledReason && (
          <p className="muted">Motivo: {appointment.cancelledReason}</p>
        )}

        {problem && <ErrorMessage>{problem}</ErrorMessage>}

        {editing && (
          <AppointmentForm
            grantId={appointment.grantId}
            appointment={appointment}
            remaining={remaining}
            onDone={() => {
              setEditing(false);
              onChanged();
            }}
            onCancel={() => setEditing(false)}
          />
        )}
      </div>

      {appointment.status === 'scheduled' && !editing && (
        <div className="row">
          <button
            type="button"
            className="btn btn--primary btn--sm btn--icon-text"
            disabled={busy}
            onClick={() =>
              void run(
                () =>
                  api.post(`/admin/tutoring/appointments/${appointment.id}/confirm`, {
                    minutes: null,
                    notes: null,
                  }),
                onChanged,
              )
            }
          >
            <IconCheck /> Se dio
          </button>

          <button
            type="button"
            className="btn btn--ghost btn--sm btn--icon-text"
            onClick={() => setEditing(true)}
          >
            <IconPencil /> Mover
          </button>

          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={busy}
            onClick={() => {
              const reason = window.prompt('Motivo de la anulación');
              if (reason === null) return;

              void run(
                () =>
                  api.post(`/admin/tutoring/appointments/${appointment.id}/cancel`, { reason }),
                onChanged,
              );
            }}
          >
            Anular
          </button>
        </div>
      )}
    </li>
  );
}

/**
 * Convocar o mover una tutoría.
 *
 * Es el mismo formulario para las dos cosas porque los datos son los mismos; lo único que
 * cambia es a dónde va. Mover reenvía la invitación con una versión nueva, y el calendario de
 * los invitados actualiza el evento que ya tenían en vez de crear otro al lado.
 */
function AppointmentForm({
  grantId,
  appointment,
  remaining,
  onDone,
  onCancel,
}: {
  grantId: string;
  appointment: Appointment | null;
  remaining: number;
  onDone: () => void;
  onCancel: () => void;
}) {
  const start = appointment ? new Date(appointment.startsAt) : null;

  const [form, setForm] = useState({
    date: start ? localDate(start) : new Date().toISOString().slice(0, 10),
    time: start ? localTime(start) : '17:00',
    hours: String((appointment?.minutes ?? 60) / 60).replace('.', ','),
    topic: appointment?.topic ?? '',
    notes: appointment?.notes ?? '',
    location: appointment?.location ?? '',
    tutorId: appointment?.tutorId ?? '',
  });

  const { busy, problem, run } = useAction();
  const tutors = useApi<TutorOption[]>('/admin/tutors', []);
  const choosable = (tutors.data ?? []).filter((t) => t.isActive || t.id === form.tutorId);

  const id = appointment?.id ?? grantId;

  return (
    <form
      className="stack admin__form--nested"
      onSubmit={(event) => {
        event.preventDefault();

        const body = {
          grantId,
          tutorId: form.tutorId || null,
          // La hora la teclea alguien en su huso; se manda en UTC y cada calendario la traduce.
          startsAt: new Date(`${form.date}T${form.time}`).toISOString(),
          minutes: toMinutes(parseDecimal(form.hours)),
          topic: form.topic,
          notes: form.notes,
          location: form.location,
        };

        void run(
          () =>
            appointment
              ? api.put(`/admin/tutoring/appointments/${appointment.id}`, body)
              : api.post('/admin/tutoring/appointments', body),
          onDone,
        );
      }}
    >
      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor={`cita-fecha-${id}`}>Día</label>
          <input
            id={`cita-fecha-${id}`}
            type="date"
            value={form.date}
            required
            onChange={(e) => setForm((f) => ({ ...f, date: e.target.value }))}
          />
        </div>

        <div className="field">
          <label htmlFor={`cita-hora-${id}`}>Hora</label>
          <input
            id={`cita-hora-${id}`}
            type="time"
            value={form.time}
            required
            onChange={(e) => setForm((f) => ({ ...f, time: e.target.value }))}
          />
        </div>

        <div className="field">
          <label htmlFor={`cita-horas-${id}`}>Duración (horas)</label>
          <input
            id={`cita-horas-${id}`}
            type="text"
            inputMode="decimal"
            value={form.hours}
            required
            onChange={(e) => setForm((f) => ({ ...f, hours: e.target.value }))}
          />
          <p className="muted admin__hint">
            Quedan {formatMinutes(Math.max(remaining, 0))} sin convocar.
          </p>
        </div>
      </div>

      <div className="field">
        <label htmlFor={`cita-tema-${id}`}>De qué va</label>
        <input
          id={`cita-tema-${id}`}
          value={form.topic}
          required
          maxLength={200}
          onChange={(e) => setForm((f) => ({ ...f, topic: e.target.value }))}
        />
        <p className="muted admin__hint">Va en el asunto de la invitación de calendario.</p>
      </div>

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor={`cita-profe-${id}`}>Profesor</label>
          <select
            id={`cita-profe-${id}`}
            value={form.tutorId}
            onChange={(e) => setForm((f) => ({ ...f, tutorId: e.target.value }))}
          >
            <option value="">Sin asignar</option>
            {choosable.map((tutor) => (
              <option key={tutor.id} value={tutor.id}>
                {tutor.displayName} · {tutor.commissionPercent}%
              </option>
            ))}
          </select>
          <p className="muted admin__hint">Se le invita también a él.</p>
        </div>

        <div className="field">
          <label htmlFor={`cita-donde-${id}`}>Dónde</label>
          <input
            id={`cita-donde-${id}`}
            value={form.location}
            maxLength={500}
            placeholder="https://meet.google.com/…"
            onChange={(e) => setForm((f) => ({ ...f, location: e.target.value }))}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor={`cita-notas-${id}`}>Notas</label>
        <textarea
          id={`cita-notas-${id}`}
          rows={2}
          value={form.notes}
          onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
        />
      </div>

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          {appointment ? 'Guardar y reenviar invitación' : 'Convocar e invitar'}
        </button>

        <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </form>
  );
}

function SessionForm({
  grantId,
  remaining,
  onDone,
  onCancel,
}: {
  grantId: string;
  remaining: number;
  onDone: () => void;
  onCancel: () => void;
}) {
  const today = new Date().toISOString().slice(0, 10);
  const [form, setForm] = useState({ hours: '1', date: today, topic: '', notes: '', tutorId: '' });
  const { busy, problem, run } = useAction();

  // Se carga al abrir el formulario y no con la página: la lista de profesores solo hace falta
  // aquí, y traerla siempre encarecería una pantalla que casi nunca apunta tutorías.
  const tutors = useApi<TutorOption[]>('/admin/tutors', []);
  const choosable = (tutors.data ?? []).filter((t) => t.isActive);

  return (
    <form
      className="stack"
      onSubmit={(event) => {
        event.preventDefault();

        void run(
          () =>
            api.post(`/admin/tutoring/grants/${grantId}/sessions`, {
              minutes: toMinutes(parseDecimal(form.hours)),
              // Mediodía y no medianoche: con medianoche, un huso por detrás mueve la tutoría
              // al día anterior en el histórico del alumno.
              occurredAt: new Date(`${form.date}T12:00:00`).toISOString(),
              topic: form.topic,
              notes: form.notes,
              // Vacío significa «la dio la casa»: no hay a quién liquidar y no se devenga nada.
              tutorId: form.tutorId || null,
            }),
          onDone,
        );
      }}
    >
      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor={`ses-hours-${grantId}`}>Duración (horas)</label>
          <input
            id={`ses-hours-${grantId}`}
            type="text"
            inputMode="decimal"
            value={form.hours}
            required
            onChange={(e) => setForm((f) => ({ ...f, hours: e.target.value }))}
          />
          <p className="muted admin__hint">Quedan {formatMinutes(remaining)}.</p>
        </div>

        <div className="field">
          <label htmlFor={`ses-date-${grantId}`}>Fecha</label>
          <input
            id={`ses-date-${grantId}`}
            type="date"
            value={form.date}
            required
            onChange={(e) => setForm((f) => ({ ...f, date: e.target.value }))}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor={`ses-topic-${grantId}`}>De qué fue</label>
        <input
          id={`ses-topic-${grantId}`}
          value={form.topic}
          required
          maxLength={200}
          onChange={(e) => setForm((f) => ({ ...f, topic: e.target.value }))}
        />
      </div>

      <div className="field">
        <label htmlFor={`ses-tutor-${grantId}`}>Quién la dio</label>
        <select
          id={`ses-tutor-${grantId}`}
          value={form.tutorId}
          onChange={(e) => setForm((f) => ({ ...f, tutorId: e.target.value }))}
        >
          <option value="">La casa · sin liquidación</option>
          {choosable.map((tutor) => (
            <option key={tutor.id} value={tutor.id}>
              {tutor.displayName} · {tutor.commissionPercent}%
            </option>
          ))}
        </select>
        <p className="muted admin__hint">
          {choosable.length === 0
            ? 'No hay profesores activos. Se dan de alta en la pestaña «Profesores».'
            : 'Se le devenga su porcentaje sobre la parte pagada de estas horas.'}
        </p>
      </div>

      <div className="field">
        <label htmlFor={`ses-notes-${grantId}`}>Notas</label>
        <textarea
          id={`ses-notes-${grantId}`}
          rows={2}
          value={form.notes}
          onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
        />
      </div>

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          Apuntar y descontar
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

  const run = useCallback(
    async (action: () => Promise<unknown>, onDone?: (result: unknown) => void) => {
      setBusy(true);
      setProblem(null);

      try {
        onDone?.(await action());
      } catch (caught) {
        setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido guardar.');
      } finally {
        setBusy(false);
      }
    },
    [],
  );

  return { busy, problem, run };
}

/**
 * Cómo caduca un paquete, en una celda de tabla.
 *
 * «Con la suscripción» no lleva fecha a propósito: la de cada alumno es distinta y se mueve en
 * cada renovación, así que poner una aquí sería inventarse la de alguien.
 */
function expiryLabel(pack: TutoringPackage): string {
  if (pack.expiryMode === 'subscription') return 'Con la suscripción';
  if (pack.expiryMode === 'never') return 'No caduca';

  return pack.validityDays === null ? 'Sin caducidad' : `${pack.validityDays} días`;
}
