import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconAlert, IconCheck, IconPencil, IconPlus, IconRefresh } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { ProductPicker, type SelectableProducts } from './ProductPicker';
import { api, ApiError } from '../../lib/api';

/**
 * Planes y cobro.
 *
 * Los precios estaban en el seed y solo se podían cambiar tocando código y desplegando. La
 * página pública ya los leía de la base, así que lo único que faltaba era poder editarlos.
 */
type AdminPlan = {
  id: string;
  code: string;
  name: string;
  interval: string;
  priceCents: number;
  benefits: string[];
  includesAllCourses: boolean;
  includesAllPacks: boolean;
  includedProductIds: string[];
  displayOrder: number;
  isActive: boolean;
  stripePriceId: string | null;
};


type SecretState = {
  configured: boolean;
  valid: boolean;
  placeholder?: boolean;
  hint: string | null;
};

type StripeState = {
  secretKey: SecretState;
  webhookSecret: SecretState;
  automaticTax: boolean;
  mode: string;
  webhookPath: string;
};

type SyncResult = { code: string; linked: boolean; priceId: string | null; problem: string | null };

const INTERVALS = [
  { value: 'monthly', label: 'Mensual' },
  { value: 'quarterly', label: 'Trimestral' },
  { value: 'biannual', label: 'Semestral' },
  { value: 'yearly', label: 'Anual' },
  { value: 'lifetime', label: 'Pago único' },
];

const INTERVAL_LABEL = Object.fromEntries(INTERVALS.map((i) => [i.value, i.label]));

const euros = (cents: number) =>
  (cents / 100).toLocaleString('es-ES', { style: 'currency', currency: 'EUR' });

export function AdminBilling() {
  const plans = useApi<AdminPlan[]>('/admin/billing/plans', []);
  const stripe = useApi<StripeState>('/admin/billing/stripe', []);

  const [editing, setEditing] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [sync, setSync] = useState<SyncResult[] | null>(null);
  const { busy, problem, run } = useAction();

  if (plans.loading || stripe.loading) {
    return <Spinner label="Cargando planes…" />;
  }

  if (plans.error) {
    return <ErrorMessage>{plans.error.message}</ErrorMessage>;
  }

  const unlinked = (plans.data ?? []).filter((plan) => plan.isActive && !plan.stripePriceId);

  return (
    <>
      {stripe.data && <StripePanel state={stripe.data} />}

      <section className="admin__panel">
        <div className="admin__panel-head">
          <h2>Planes</h2>

          <div className="row">
            <button
              type="button"
              className="btn btn--ghost btn--sm btn--icon-text"
              disabled={busy}
              onClick={() =>
                void run(
                  () => api.post<SyncResult[]>('/admin/billing/stripe/sync'),
                  (result) => {
                    setSync(result as SyncResult[]);
                    plans.reload();
                  },
                )
              }
            >
              <IconRefresh />
              Sincronizar con Stripe
            </button>

            <button
              type="button"
              className="btn btn--primary btn--sm btn--icon-text"
              onClick={() => setCreating((open) => !open)}
              aria-expanded={creating}
            >
              <IconPlus />
              Plan nuevo
            </button>
          </div>
        </div>

        {problem && <ErrorMessage>{problem}</ErrorMessage>}

        {/*
          Un plan activo sin precio en Stripe se vende y falla en el checkout con
          `plan.no_stripe_price`. Es el error más caro de descubrir tarde, así que se avisa
          aquí y no en los logs.
        */}
        {unlinked.length > 0 && (
          <div className="alert alert--error" role="alert">
            <strong>{unlinked.length} plan(es) sin precio en Stripe:</strong>{' '}
            {unlinked.map((plan) => plan.code).join(', ')}. Quien los elija verá un error al
            pagar. Pulsa «Sincronizar con Stripe».
          </div>
        )}

        {sync && (
          <div className="alert alert--info" role="status">
            {sync.map((item) => (
              <div key={item.code}>
                {item.linked ? '✓' : '✗'} {item.code}
                {item.problem ? ` — ${item.problem}` : ''}
              </div>
            ))}
          </div>
        )}

        {creating && (
          <PlanForm
            onDone={() => {
              setCreating(false);
              plans.reload();
            }}
          />
        )}

        <div className="table-scroll">
          <table>
            <caption className="sr-only">Planes de suscripción</caption>
            <thead>
              <tr>
                <th scope="col">Plan</th>
                <th scope="col">Periodo</th>
                <th scope="col">Precio</th>
                <th scope="col">Stripe</th>
                <th scope="col">Estado</th>
                <th scope="col">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {(plans.data ?? []).map((plan) => (
                <tr key={plan.id}>
                  <td>
                    <strong>{plan.name}</strong>
                    <br />
                    <code className="muted">{plan.code}</code>
                  </td>
                  <td>{INTERVAL_LABEL[plan.interval] ?? plan.interval}</td>
                  <td>{euros(plan.priceCents)}</td>
                  <td>
                    {plan.stripePriceId ? (
                      <span className="chip">
                        <IconCheck size={13} />
                        enlazado
                      </span>
                    ) : (
                      <span className="chip chip--accent">
                        <IconAlert size={13} />
                        sin precio
                      </span>
                    )}
                  </td>
                  <td>{plan.isActive ? 'En venta' : 'Retirado'}</td>
                  <td>
                    <div className="row">
                      <button
                        type="button"
                        className="btn btn--ghost btn--sm btn--icon-text"
                        onClick={() => setEditing(editing === plan.id ? null : plan.id)}
                      >
                        <IconPencil />
                        Editar
                      </button>

                      <button
                        type="button"
                        className="btn btn--ghost btn--sm"
                        disabled={busy}
                        onClick={() =>
                          void run(
                            () =>
                              api.post(`/admin/billing/plans/${plan.id}/active`, {
                                active: !plan.isActive,
                              }),
                            plans.reload,
                          )
                        }
                      >
                        {plan.isActive ? 'Retirar' : 'Reactivar'}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {editing && (
          <PlanForm
            plan={(plans.data ?? []).find((plan) => plan.id === editing)}
            onDone={() => {
              setEditing(null);
              plans.reload();
            }}
          />
        )}
      </section>
    </>
  );
}

function StripePanel({ state }: { state: StripeState }) {
  const listo = state.secretKey.valid && state.webhookSecret.valid;

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Configuración de Stripe</h2>
        <span className={`chip ${listo ? 'chip--brand' : 'chip--accent'}`}>
          {listo ? <IconCheck size={13} /> : <IconAlert size={13} />}
          {state.mode}
        </span>
      </div>

      <dl className="admin__facts">
        <div>
          <dt>Clave secreta</dt>
          <dd>
            <SecretBadge state={state.secretKey} />
          </dd>
        </div>
        <div>
          <dt>Secreto del webhook</dt>
          <dd>
            <SecretBadge state={state.webhookSecret} />
          </dd>
        </div>
        <div>
          <dt>Impuestos automáticos</dt>
          <dd>{state.automaticTax ? 'Activados' : 'Desactivados'}</dd>
        </div>
        <div>
          <dt>Endpoint del webhook</dt>
          <dd>
            <code>{state.webhookPath}</code>
          </dd>
        </div>
      </dl>

      {!listo && (
        <>
          <p className="muted">
            Las claves no se guardan en la base ni se editan aquí: son secretos y van en el
            entorno del servidor, donde no aparecen en una copia de seguridad ni en una captura
            de pantalla. Esto solo dice si están puestas.
          </p>

          <ol className="steps">
            <li>
              <strong>Copia las claves de Stripe.</strong> En el panel de Stripe, la clave
              secreta está en «Developers → API keys» (empieza por <code>sk_test_</code> en
              pruebas y por <code>sk_live_</code> en real).
            </li>
            <li>
              <strong>Crea el webhook.</strong> En «Developers → Webhooks», apuntando a{' '}
              <code>https://TU-DOMINIO{state.webhookPath}</code>. Stripe te da entonces el
              secreto de firma, que empieza por <code>whsec_</code>.
            </li>
            <li>
              <strong>Ponlas en el entorno</strong> como{' '}
              <code>Academy__Stripe__SecretKey</code> y{' '}
              <code>Academy__Stripe__WebhookSecret</code>. En producción van en{' '}
              <code>infra/.env</code>; en local, en las variables{' '}
              <code>STRIPE_SECRET_KEY</code> y <code>STRIPE_WEBHOOK_SECRET</code> antes de{' '}
              <code>.\dev.ps1 up</code>.
            </li>
            <li>
              <strong>Reinicia la API y sincroniza.</strong> Con las claves puestas, el botón
              «Sincronizar con Stripe» crea el precio de cada plan y lo enlaza.
            </li>
          </ol>
        </>
      )}
    </section>
  );
}

function SecretBadge({ state }: { state: SecretState }) {
  if (!state.configured) {
    return <span className="chip chip--accent">sin configurar</span>;
  }

  if (state.placeholder) {
    return <span className="chip chip--accent">marcador de desarrollo</span>;
  }

  if (!state.valid) {
    return <span className="chip chip--accent">formato inesperado {state.hint}</span>;
  }

  return <span className="chip chip--brand">configurada {state.hint}</span>;
}

function PlanForm({ plan, onDone }: { plan?: AdminPlan; onDone: () => void }) {
  const [form, setForm] = useState({
    code: plan?.code ?? '',
    name: plan?.name ?? '',
    interval: plan?.interval ?? 'monthly',
    priceEuros: ((plan?.priceCents ?? 0) / 100).toString(),
    benefits: (plan?.benefits ?? []).join('\n'),
    // Un plan nuevo arranca dando todo el catálogo, que es lo que hacían todos hasta que esto
    // fue configurable: el que quiera estrecharlo lo hace a la vista, y el que no, no se
    // encuentra un plan que no da nada por no haber marcado nada.
    includesAllCourses: plan?.includesAllCourses ?? true,
    includesAllPacks: plan?.includesAllPacks ?? false,
    includedProductIds: plan?.includedProductIds ?? [],
    displayOrder: String(plan?.displayOrder ?? 1),
  });

  const catalog = useApi<SelectableProducts>('/admin/billing/plans/selectable-products', []);

  const { busy, problem, run } = useAction();
  const [warning, setWarning] = useState<string | null>(null);

  function toggleProduct(id: string) {
    setForm((f) => ({
      ...f,
      includedProductIds: f.includedProductIds.includes(id)
        ? f.includedProductIds.filter((x) => x !== id)
        : [...f.includedProductIds, id],
    }));
  }

  const packIds = new Set((catalog.data?.packs ?? []).map((p) => p.id));
  const courseIds = new Set((catalog.data?.courses ?? []).map((p) => p.id));

  const cursosMarcados = form.includedProductIds.filter((id) => courseIds.has(id)).length;
  const packsMarcados = form.includedProductIds.filter((id) => packIds.has(id)).length;

  // Un plan que no da nada es un cobro sin contraprestación. El servidor también lo rechaza;
  // aquí se apaga el botón para no descubrirlo después de escribirlo todo.
  const daAlgo =
    form.includesAllCourses || form.includesAllPacks || form.includedProductIds.length > 0;

  return (
    <form
      className="admin__form"
      onSubmit={(event) => {
        event.preventDefault();

        const body = {
          name: form.name,
          priceCents: Math.round(Number(form.priceEuros) * 100),
          benefits: form.benefits
            .split('\n')
            .map((line) => line.trim())
            .filter(Boolean),
          includesAllCourses: form.includesAllCourses,
          includesAllPacks: form.includesAllPacks,
          // Solo viaja lo marcado de la familia que NO va entera: con el interruptor puesto, la
          // lista es ruido, y guardarla haría que quitar el interruptor resucitara marcas
          // viejas que nadie ha vuelto a mirar.
          includedProductIds: form.includedProductIds.filter(
            (id) =>
              (courseIds.has(id) && !form.includesAllCourses) ||
              (packIds.has(id) && !form.includesAllPacks),
          ),
          displayOrder: Number(form.displayOrder),
        };

        void run(
          () =>
            plan
              ? api.put<{ needsStripeSync: boolean }>(`/admin/billing/plans/${plan.id}`, body)
              : api.post('/admin/billing/plans', {
                  ...body,
                  code: form.code,
                  interval: form.interval,
                }),
          (result) => {
            const changed = (result as { needsStripeSync?: boolean } | undefined)?.needsStripeSync;

            if (changed) {
              // No se cierra el formulario: el aviso importa más que volver a la lista.
              setWarning(
                'Has cambiado el precio. El plan se ha quedado sin precio en Stripe: pulsa ' +
                  '«Sincronizar con Stripe» antes de que nadie compre. Quien ya está suscrito ' +
                  'conserva el precio que contrató.',
              );
              return;
            }

            onDone();
          },
        );
      }}
    >
      <div className="admin__form-grid">
        {!plan && (
          <label className="field">
            <span>Código</span>
            <input
              required
              value={form.code}
              onChange={(e) => setForm({ ...form, code: e.target.value })}
              placeholder="monthly"
            />
            <small className="muted">No se puede cambiar: viaja en Stripe y en Discord.</small>
          </label>
        )}

        <label className="field">
          <span>Nombre</span>
          <input
            required
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
          />
        </label>

        {!plan && (
          <label className="field">
            <span>Periodo</span>
            <select
              value={form.interval}
              onChange={(e) => setForm({ ...form, interval: e.target.value })}
            >
              {INTERVALS.map((interval) => (
                <option key={interval.value} value={interval.value}>
                  {interval.label}
                </option>
              ))}
            </select>
          </label>
        )}

        <label className="field">
          <span>Precio (€)</span>
          <input
            required
            type="number"
            min="1"
            step="1"
            value={form.priceEuros}
            onChange={(e) => setForm({ ...form, priceEuros: e.target.value })}
          />
        </label>

        <label className="field">
          <span>Orden</span>
          <input
            required
            type="number"
            min="1"
            value={form.displayOrder}
            onChange={(e) => setForm({ ...form, displayOrder: e.target.value })}
          />
        </label>
      </div>

      <ProductPicker
        titulo="Cursos"
        todoLabel="Todo el catálogo, incluidos los que se publiquen"
        todoAyuda="Lo recomendable para los planes de arriba: un curso nuevo entra solo, sin tener que acordarse de volver aquí."
        sueltosLabel="Solo los cursos que marque"
        todo={form.includesAllCourses}
        onTodo={(v) => setForm({ ...form, includesAllCourses: v })}
        productos={catalog.data?.courses ?? []}
        marcados={form.includedProductIds}
        onToggle={toggleProduct}
        cargando={catalog.loading}
        marcadosCount={cursosMarcados}
      />

      <ProductPicker
        titulo="Packs sectoriales"
        todoLabel="Todos los packs, incluidos los que se publiquen"
        todoAyuda="Un pack nuevo entra solo en este plan."
        sueltosLabel="Solo los packs que marque"
        todo={form.includesAllPacks}
        onTodo={(v) => setForm({ ...form, includesAllPacks: v })}
        productos={catalog.data?.packs ?? []}
        marcados={form.includedProductIds}
        onToggle={toggleProduct}
        cargando={catalog.loading}
        marcadosCount={packsMarcados}
      />

      {!daAlgo && (
        <div className="alert alert--warn" role="status">
          Este plan no incluye nada. Marca todo el catálogo, todos los packs, o al menos un
          producto.
        </div>
      )}

      <label className="field">
        <span>Otras ventajas</span>
        <textarea
          rows={3}
          value={form.benefits}
          onChange={(e) => setForm({ ...form, benefits: e.target.value })}
          placeholder={'Comunidad Discord\nSesiones grupales mensuales\nPrioridad en soporte'}
        />
        <small className="muted">
          Una línea por punto, y solo para lo que <strong>no</strong> es un curso ni un pack:
          Discord, sesiones, soporte. Los cursos y los packs salen solos en la página de precios
          desde lo que hayas marcado arriba, así que escribirlos aquí es duplicarlos y arriesgarse
          a que un día digan cosas distintas.
        </small>
      </label>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}
      {warning && (
        <div className="alert alert--info" role="status">
          {warning}
        </div>
      )}

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !daAlgo}>
          {busy ? 'Guardando…' : plan ? 'Guardar plan' : 'Crear plan'}
        </button>

        <button type="button" className="btn btn--ghost btn--sm" onClick={onDone}>
          {warning ? 'Cerrar' : 'Cancelar'}
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
