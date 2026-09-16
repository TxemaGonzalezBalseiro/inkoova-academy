import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconAlert, IconCheck, IconPencil, IconPlus, IconRefresh } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';
import { openAuthenticatedFile } from '../../lib/download';

/**
 * Las marcas de la academia.
 *
 * Cada una tiene su presentación, su buzón de salida y sus plantillas de correo. Lo que NO
 * separa: alumnos, catálogo y cobros son comunes — una persona tiene una sola cuenta aunque
 * curse cosas de dos marcas.
 *
 * La contraseña del buzón nunca llega a esta pantalla: la API solo dice si hay una puesta. Por
 * eso el campo se envía vacío cuando no se quiere cambiar, y eso conserva la guardada.
 */
type Mailbox = {
  host: string;
  port: number;
  username: string;
  security: string;
  fromAddress: string;
  fromName: string;
  hasPassword: boolean;
  canSend: boolean;
};

type Identity = {
  id: string;
  slug: string;
  name: string;
  tagline: string;
  logoUrl: string;
  publicDomain: string;
  supportEmail: string;
  isDefault: boolean;
  isActive: boolean;
  mailbox: Mailbox;
  legal: {
    legalName: string;
    taxId: string;
    address: string;
    registryDetails: string;
    email: string;
    linkedInUrl: string;
    companyUrl: string;
    invoiceType: string;
    correctiveInvoiceType: string;
    /** Las claves admitidas las manda el servidor desde el esquema de la AEAT, no una lista de aquí. */
    invoiceTypeOptions: { code: string; description: string }[];
    correctiveInvoiceTypeOptions: { code: string; description: string }[];
    /** Lo calcula el servidor: la regla de qué es obligatorio no se duplica aquí. */
    missing: string[];
    isComplete: boolean;
  };
};

type Template = {
  name: string;
  label: string;
  overridden: boolean;
  subject: string | null;
  html: string | null;
};

export function AdminIdentities() {
  const identities = useApi<Identity[]>('/admin/identities', []);
  const [creating, setCreating] = useState(false);
  const [open, setOpen] = useState<string | null>(null);

  if (identities.loading) {
    return <Spinner label="Cargando marcas…" />;
  }

  if (identities.error) {
    return <ErrorMessage>{identities.error.message}</ErrorMessage>;
  }

  const all = identities.data ?? [];

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Marcas</h2>

        <button
          type="button"
          className="btn btn--ghost btn--sm btn--icon-text"
          onClick={() => setCreating((value) => !value)}
        >
          <IconPlus /> Nueva marca
        </button>
      </div>

      <p className="muted">
        Cada marca tiene su nombre, su buzón y sus plantillas de correo. Los alumnos, el catálogo
        y los cobros son comunes: una persona tiene una sola cuenta aunque estudie cosas de dos.
      </p>

      {creating && (
        <IdentityForm
          onCancel={() => setCreating(false)}
          onDone={() => {
            setCreating(false);
            identities.reload();
          }}
        />
      )}

      {all.map((identity) => (
        <article key={identity.id} className="card admin__section">
          <header className="admin__section-head">
            <div>
              <h3>
                {identity.name}{' '}
                {identity.isDefault && <span className="badge">Principal</span>}{' '}
                {!identity.isActive && <span className="badge">Desactivada</span>}
              </h3>
              <p className="muted">
                {identity.slug}
                {identity.publicDomain && ` · ${identity.publicDomain}`}
              </p>
            </div>

            <button
              type="button"
              className="btn btn--ghost btn--sm"
              onClick={() => setOpen(open === identity.id ? null : identity.id)}
            >
              {open === identity.id ? 'Cerrar' : 'Configurar'}
            </button>
          </header>

          {!identity.mailbox.canSend && (
            <p className="alert admin__alert-warn">
              <IconAlert />
              <span>
                Sin buzón configurado: sus correos saldrán desde el buzón general del servidor,
                con un remitente que no es de esta marca.
              </span>
            </p>
          )}

          {open === identity.id && (
            <IdentityDetail identity={identity} onChanged={identities.reload} />
          )}
        </article>
      ))}
    </section>
  );
}

function IdentityDetail({ identity, onChanged }: { identity: Identity; onChanged: () => void }) {
  const { busy, problem, run } = useAction();

  return (
    <>
      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="row">
        {!identity.isDefault && (
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={busy}
            onClick={() => void run(() => api.post(`/admin/identities/${identity.id}/default`), onChanged)}
          >
            Hacer principal
          </button>
        )}

        <button
          type="button"
          className="btn btn--ghost btn--sm"
          disabled={busy || identity.isDefault}
          title={identity.isDefault ? 'La principal no se puede desactivar.' : undefined}
          onClick={() =>
            void run(
              () =>
                api.post(`/admin/identities/${identity.id}/active`, { active: !identity.isActive }),
              onChanged,
            )
          }
        >
          {identity.isActive ? 'Desactivar' : 'Reactivar'}
        </button>
      </div>

      <IdentityForm identity={identity} onDone={onChanged} onCancel={() => undefined} />
      <LegalForm identity={identity} onDone={onChanged} />
      <MailboxForm identity={identity} onDone={onChanged} />
      <CertificateStyleForm identity={identity} />
      <Templates identity={identity} />
    </>
  );
}

/**
 * Los datos del titular del sitio.
 *
 * Van aquí y no en el código porque el aviso legal, la política de privacidad y el pie los
 * necesitan, y con varias marcas el titular de cada sitio puede ser una sociedad distinta o una
 * persona física.
 *
 * Se guardan aunque estén incompletos: rellenarlos es un trámite por partes —el NIF hoy, los
 * datos registrales cuando llegue la escritura— y exigirlos todos de golpe obligaría a
 * inventarse los que faltan solo para poder guardar. La página avisa de lo que falta.
 */
function LegalForm({ identity, onDone }: { identity: Identity; onDone: () => void }) {
  const [form, setForm] = useState({
    legalName: identity.legal.legalName ?? '',
    taxId: identity.legal.taxId ?? '',
    address: identity.legal.address ?? '',
    registryDetails: identity.legal.registryDetails ?? '',
    email: identity.legal.email ?? '',
    linkedInUrl: identity.legal.linkedInUrl ?? '',
    companyUrl: identity.legal.companyUrl ?? '',
    invoiceType: identity.legal.invoiceType ?? 'F2',
    correctiveInvoiceType: identity.legal.correctiveInvoiceType ?? 'R5',
  });

  const { busy, problem, run } = useAction();
  const set = (key: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <div className="stack">
      <h4>Datos identificativos</h4>

      <p className="muted">
        Salen en el aviso legal, en la política de privacidad y en el pie. El artículo 10 de la
        LSSI obliga a publicarlos, así que no se rellenan con un ejemplo: lo que falte se verá
        marcado como pendiente en la página.
      </p>

      {identity.legal.missing.length > 0 && (
        <p className="alert admin__alert-warn">
          <IconAlert />
          <span>Faltan: {identity.legal.missing.join(', ')}.</span>
        </p>
      )}

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor={`lg-name-${identity.id}`}>Razón social o nombre</label>
          <input
            id={`lg-name-${identity.id}`}
            value={form.legalName}
            maxLength={200}
            onChange={(e) => set('legalName')(e.target.value)}
          />
          <p className="muted admin__hint">Si el titular es una persona, su nombre y apellidos.</p>
        </div>

        <div className="field">
          <label htmlFor={`lg-tax-${identity.id}`}>NIF o CIF</label>
          <input
            id={`lg-tax-${identity.id}`}
            value={form.taxId}
            maxLength={40}
            onChange={(e) => set('taxId')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`lg-email-${identity.id}`}>Correo de contacto legal</label>
          <input
            id={`lg-email-${identity.id}`}
            value={form.email}
            onChange={(e) => set('email')(e.target.value)}
          />
          <p className="muted admin__hint">
            Puede no ser el de soporte: el buzón que atiende dudas y el que recibe una
            reclamación formal no tienen por qué ser el mismo.
          </p>
        </div>

        <div className="field">
          <label htmlFor={`lg-linkedin-${identity.id}`}>LinkedIn</label>
          <input
            id={`lg-linkedin-${identity.id}`}
            value={form.linkedInUrl}
            placeholder="https://www.linkedin.com/company/…"
            onChange={(e) => set('linkedInUrl')(e.target.value)}
          />
          <p className="muted admin__hint">Vacío: el enlace no aparece en el pie.</p>
        </div>

        <div className="field">
          <label htmlFor={`lg-web-${identity.id}`}>Web de la empresa</label>
          <input
            id={`lg-web-${identity.id}`}
            value={form.companyUrl}
            placeholder="https://…"
            onChange={(e) => set('companyUrl')(e.target.value)}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor={`lg-address-${identity.id}`}>Domicilio</label>
        <textarea
          id={`lg-address-${identity.id}`}
          rows={2}
          value={form.address}
          onChange={(e) => set('address')(e.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor={`lg-registry-${identity.id}`}>Datos registrales</label>
        <textarea
          id={`lg-registry-${identity.id}`}
          rows={2}
          value={form.registryDetails}
          placeholder="Inscrita en el Registro Mercantil de…, tomo…, folio…, hoja…"
          onChange={(e) => set('registryDetails')(e.target.value)}
        />
        <p className="muted admin__hint">
          Solo para sociedades. Una persona física no está inscrita, y dejarlo vacío no cuenta
          como dato pendiente.
        </p>
      </div>

      <h4>Facturación</h4>

      <p className="muted">
        La clave de tipo de factura que se declara en cada registro de Veri*factu. Las opciones
        son las del esquema oficial de la AEAT: cualquier otra hace que rechace el registro
        entero, y el rechazo llega con el reintento diario, no al facturar.
      </p>

      <div className="field">
        <label htmlFor={`lg-invoice-type-${identity.id}`}>Ventas</label>
        <select
          id={`lg-invoice-type-${identity.id}`}
          value={form.invoiceType}
          onChange={(e) => set('invoiceType')(e.target.value)}
        >
          {identity.legal.invoiceTypeOptions.map((option) => (
            <option key={option.code} value={option.code}>
              {option.code} · {option.description}
            </option>
          ))}
        </select>
        <p className="muted admin__hint">
          F2 mientras no se pida el NIF al cliente. Pedirlo mueve esa venta a F1.
        </p>
      </div>

      <div className="field">
        <label htmlFor={`lg-corrective-type-${identity.id}`}>Rectificativas</label>
        <select
          id={`lg-corrective-type-${identity.id}`}
          value={form.correctiveInvoiceType}
          onChange={(e) => set('correctiveInvoiceType')(e.target.value)}
        >
          {identity.legal.correctiveInvoiceTypeOptions.map((option) => (
            <option key={option.code} value={option.code}>
              {option.code} · {option.description}
            </option>
          ))}
        </select>
        <p className="muted admin__hint">
          Es la que se usa al devolver un cobro. R5 es la que corresponde si las ventas van
          como F2: una rectificativa de simplificada tiene clave propia.
        </p>
      </div>

      <div className="row">
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={busy}
          onClick={() => void run(() => api.put(`/admin/identities/${identity.id}/legal`, form), onDone)}
        >
          Guardar datos
        </button>
      </div>
    </div>
  );
}

type CertificateStyle = {
  heading: string;
  subheading: string;
  issuerName: string;
  issuerNote: string;
  primaryColor: string;
  accentColor: string;
  supportColor: string;
};

/**
 * El certificado de una marca.
 *
 * Se toca la cabecera, quién lo emite y tres colores. **Nada más, y es lo correcto**: el sello,
 * el QR, el código y el pie con la URL de verificación los dibuja el servidor. Si fueran
 * editables, bastaría con borrar un trozo para que ese certificado dejara de poder comprobarse,
 * y el fallo no se vería hasta que alguien intentara verificarlo.
 *
 * Los tonos del marco y de la banda no se configuran: se calculan a partir del color principal.
 * Sueltos, alguien cambiaría el principal y dejaría el marco del color anterior.
 */
function CertificateStyleForm({ identity }: { identity: Identity }) {
  const style = useApi<CertificateStyle>(`/admin/identities/${identity.id}/certificate`, [identity.id]);

  if (style.loading || !style.data) {
    return null;
  }

  return <CertificateFields key={JSON.stringify(style.data)} identity={identity} stored={style.data} onDone={style.reload} />;
}

function CertificateFields({
  identity,
  stored,
  onDone,
}: {
  identity: Identity;
  stored: CertificateStyle;
  onDone: () => void;
}) {
  const [form, setForm] = useState(stored);
  const { busy, problem, run } = useAction();
  const set = (key: keyof CertificateStyle) => (value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  return (
    <div className="stack">
      <h4>Certificado</h4>

      <p className="muted">
        Solo la cabecera, el emisor y los colores. El sello, el QR, el código y el pie los pone el
        servidor y no se tocan: así ningún ajuste puede dejar un certificado que no se pueda
        verificar. Lo que dejes vacío usa lo de siempre.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor={`cert-heading-${identity.id}`}>Cabecera</label>
          <input
            id={`cert-heading-${identity.id}`}
            value={form.heading}
            maxLength={80}
            placeholder="INKOOVA ACADEMY"
            onChange={(e) => set('heading')(e.target.value)}
          />
          <p className="muted admin__hint">Se pasa a mayúsculas automáticamente.</p>
        </div>

        <div className="field">
          <label htmlFor={`cert-sub-${identity.id}`}>Segunda línea</label>
          <input
            id={`cert-sub-${identity.id}`}
            value={form.subheading}
            maxLength={80}
            placeholder="CERTIFICADO DE APROVECHAMIENTO"
            onChange={(e) => set('subheading')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`cert-issuer-${identity.id}`}>Entidad emisora</label>
          <input
            id={`cert-issuer-${identity.id}`}
            value={form.issuerName}
            maxLength={120}
            placeholder="Inkoova Academy"
            onChange={(e) => set('issuerName')(e.target.value)}
          />
          <p className="muted admin__hint">Va al pie, en el sello y en las propiedades del PDF.</p>
        </div>

        <div className="field">
          <label htmlFor={`cert-note-${identity.id}`}>Bajo el emisor</label>
          <input
            id={`cert-note-${identity.id}`}
            value={form.issuerNote}
            maxLength={120}
            placeholder="Entidad emisora"
            onChange={(e) => set('issuerNote')(e.target.value)}
          />
        </div>
      </div>

      <div className="admin__form-grid">
        <ColorField
          id={`cert-primary-${identity.id}`}
          label="Color principal"
          hint="La banda, el nombre del alumno y el marco."
          value={form.primaryColor}
          fallback="#1E3A8A"
          onChange={set('primaryColor')}
        />
        <ColorField
          id={`cert-accent-${identity.id}`}
          label="Color de acento"
          hint="El código de verificación y los rombos."
          value={form.accentColor}
          fallback="#DB2777"
          onChange={set('accentColor')}
        />
        <ColorField
          id={`cert-support-${identity.id}`}
          label="Color de apoyo"
          hint="El año del sello y los detalles finos."
          value={form.supportColor}
          fallback="#0891B2"
          onChange={set('supportColor')}
        />
      </div>

      <div className="row">
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={busy}
          onClick={() =>
            void run(() => api.put(`/admin/identities/${identity.id}/certificate`, form), onDone)
          }
        >
          Guardar certificado
        </button>

        {/*
          Se pide con el cliente y se abre como blob, NO con un `<a href>`. El endpoint exige
          sesión de administrador y el token vive en memoria: una navegación normal llega sin
          cabecera y el servidor responde 401.
        */}
        <button
          type="button"
          className="btn btn--ghost btn--sm"
          disabled={busy}
          onClick={() =>
            void run(() =>
              openAuthenticatedFile(
                `/admin/identities/${identity.id}/certificate/preview`,
                'ejemplo-certificado.pdf',
              ),
            )
          }
        >
          Ver un ejemplo
        </button>
      </div>
    </div>
  );
}

/**
 * Un color con selector y campo de texto a la vez: el selector para elegir, el texto para pegar
 * un hexadecimal que ya se tiene. Vacío significa «el de siempre», así que el selector muestra
 * ese valor sin escribirlo en el formulario.
 */
function ColorField({
  id,
  label,
  hint,
  value,
  fallback,
  onChange,
}: {
  id: string;
  label: string;
  hint: string;
  value: string;
  fallback: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>

      <div className="admin__color">
        <input
          type="color"
          aria-label={`${label}, selector`}
          value={/^#[0-9A-Fa-f]{6}$/.test(value) ? value : fallback}
          onChange={(e) => onChange(e.target.value.toUpperCase())}
        />
        <input
          id={id}
          value={value}
          placeholder={fallback}
          maxLength={7}
          onChange={(e) => onChange(e.target.value)}
        />
        {value && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => onChange('')}>
            Quitar
          </button>
        )}
      </div>

      <p className="muted admin__hint">{hint}</p>
    </div>
  );
}

function IdentityForm({
  identity,
  onCancel,
  onDone,
}: {
  identity?: Identity;
  onCancel: () => void;
  onDone: () => void;
}) {
  const [form, setForm] = useState({
    slug: identity?.slug ?? '',
    name: identity?.name ?? '',
    tagline: identity?.tagline ?? '',
    logoUrl: identity?.logoUrl ?? '',
    publicDomain: identity?.publicDomain ?? '',
    supportEmail: identity?.supportEmail ?? '',
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
            identity
              ? api.put(`/admin/identities/${identity.id}`, form)
              : api.post('/admin/identities', form),
          onDone,
        );
      }}
    >
      <h4>Presentación</h4>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor={`ide-name-${identity?.id ?? 'nueva'}`}>Nombre</label>
          <input
            id={`ide-name-${identity?.id ?? 'nueva'}`}
            value={form.name}
            required
            maxLength={120}
            onChange={(e) => set('name')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`ide-slug-${identity?.id ?? 'nueva'}`}>Identificador</label>
          <input
            id={`ide-slug-${identity?.id ?? 'nueva'}`}
            value={form.slug}
            required
            disabled={identity !== undefined}
            placeholder="segunda-marca"
            onChange={(e) => set('slug')(e.target.value)}
          />
          <p className="muted admin__hint">
            {identity ? 'No se cambia: es la referencia estable.' : 'En minúsculas y con guiones.'}
          </p>
        </div>

        <div className="field">
          <label htmlFor={`ide-domain-${identity?.id ?? 'nueva'}`}>Dominio público</label>
          <input
            id={`ide-domain-${identity?.id ?? 'nueva'}`}
            value={form.publicDomain}
            placeholder="marca.ejemplo.com"
            onChange={(e) => set('publicDomain')(e.target.value)}
          />
          <p className="muted admin__hint">Sin https:// ni barras.</p>
        </div>

        <div className="field">
          <label htmlFor={`ide-support-${identity?.id ?? 'nueva'}`}>Correo de contacto</label>
          <input
            id={`ide-support-${identity?.id ?? 'nueva'}`}
            value={form.supportEmail}
            placeholder="hola@marca.com"
            onChange={(e) => set('supportEmail')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`ide-logo-${identity?.id ?? 'nueva'}`}>Logo</label>
          <input
            id={`ide-logo-${identity?.id ?? 'nueva'}`}
            value={form.logoUrl}
            placeholder="/logo.svg  ·  https://…/logo.png"
            onChange={(e) => set('logoUrl')(e.target.value)}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor={`ide-tagline-${identity?.id ?? 'nueva'}`}>Lema</label>
        <textarea
          id={`ide-tagline-${identity?.id ?? 'nueva'}`}
          rows={2}
          maxLength={200}
          value={form.tagline}
          onChange={(e) => set('tagline')(e.target.value)}
        />
      </div>

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          {identity ? 'Guardar' : 'Crear marca'}
        </button>

        {!identity && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel}>
            Cancelar
          </button>
        )}
      </div>
    </form>
  );
}

function MailboxForm({ identity, onDone }: { identity: Identity; onDone: () => void }) {
  const box = identity.mailbox;
  const [form, setForm] = useState({
    host: box.host,
    port: String(box.port),
    username: box.username,
    password: '',
    security: box.security,
    fromAddress: box.fromAddress,
    fromName: box.fromName,
  });

  const [testTo, setTestTo] = useState('');
  const [sent, setSent] = useState(false);
  const { busy, problem, run } = useAction();
  const set = (key: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <div className="stack">
      <h4>Buzón de salida</h4>

      <p className="muted">
        Desde aquí salen los correos de esta marca. Conviene que el dominio del remitente sea el
        suyo: si escribe desde el dominio de otra, el SPF no cuadra y acaba en spam.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor={`mb-host-${identity.id}`}>Servidor SMTP</label>
          <input
            id={`mb-host-${identity.id}`}
            value={form.host}
            placeholder="smtp.office365.com"
            onChange={(e) => set('host')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`mb-port-${identity.id}`}>Puerto</label>
          <input
            id={`mb-port-${identity.id}`}
            type="number"
            value={form.port}
            onChange={(e) => set('port')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`mb-sec-${identity.id}`}>Cifrado</label>
          <select
            id={`mb-sec-${identity.id}`}
            value={form.security}
            onChange={(e) => set('security')(e.target.value)}
          >
            <option value="auto">Automático por puerto</option>
            <option value="starttls">STARTTLS (587)</option>
            <option value="ssl">SSL al conectar (465)</option>
            <option value="none">Sin cifrado</option>
          </select>
        </div>

        <div className="field">
          <label htmlFor={`mb-user-${identity.id}`}>Usuario</label>
          <input
            id={`mb-user-${identity.id}`}
            value={form.username}
            autoComplete="off"
            onChange={(e) => set('username')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`mb-pass-${identity.id}`}>Contraseña</label>
          <input
            id={`mb-pass-${identity.id}`}
            type="password"
            value={form.password}
            autoComplete="new-password"
            placeholder={box.hasPassword ? 'Guardada · escribe para cambiarla' : 'Sin contraseña'}
            onChange={(e) => set('password')(e.target.value)}
          />
          <p className="muted admin__hint">
            Se guarda cifrada y no se puede volver a leer. Déjala vacía para conservar la actual.
            Con Office 365 o Gmail suele hacer falta una contraseña de aplicación.
          </p>
        </div>

        <div className="field">
          <label htmlFor={`mb-from-${identity.id}`}>Remitente</label>
          <input
            id={`mb-from-${identity.id}`}
            value={form.fromAddress}
            placeholder="hola@marca.com"
            onChange={(e) => set('fromAddress')(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor={`mb-fromname-${identity.id}`}>Nombre del remitente</label>
          <input
            id={`mb-fromname-${identity.id}`}
            value={form.fromName}
            placeholder={identity.name}
            onChange={(e) => set('fromName')(e.target.value)}
          />
          <p className="muted admin__hint">Vacío: se usa «{identity.name}».</p>
        </div>
      </div>

      <div className="row">
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={busy}
          onClick={() =>
            void run(
              () =>
                api.put(`/admin/identities/${identity.id}/mailbox`, {
                  ...form,
                  port: Number(form.port),
                }),
              () => {
                // La contraseña se limpia tras guardar: dejarla en el campo invita a reenviarla
                // sin querer en el siguiente guardado.
                setForm((f) => ({ ...f, password: '' }));
                onDone();
              },
            )
          }
        >
          Guardar buzón
        </button>
      </div>

      <div className="row">
        <div className="field">
          <label htmlFor={`mb-test-${identity.id}`}>Enviar una prueba a</label>
          <input
            id={`mb-test-${identity.id}`}
            type="email"
            value={testTo}
            placeholder="tu@correo.com"
            onChange={(e) => setTestTo(e.target.value)}
          />
        </div>

        <button
          type="button"
          className="btn btn--ghost btn--sm btn--icon-text"
          disabled={busy || !testTo}
          onClick={() =>
            void run(
              () => api.post(`/admin/identities/${identity.id}/mailbox/test`, { to: testTo }),
              () => setSent(true),
            )
          }
        >
          <IconRefresh /> Probar
        </button>
      </div>

      {sent && (
        <p className="alert alert--success" role="status">
          <IconCheck /> Enviado. Si no llega, mira la carpeta de spam antes de tocar nada.
        </p>
      )}
    </div>
  );
}

function Templates({ identity }: { identity: Identity }) {
  const templates = useApi<Template[]>(`/admin/identities/${identity.id}/templates`, [identity.id]);
  const [editing, setEditing] = useState<string | null>(null);

  if (templates.loading || !templates.data) {
    return null;
  }

  return (
    <div className="stack">
      <h4>Plantillas de correo</h4>

      <p className="muted">
        Lo que no reescribas aquí se manda con la plantilla original, así que una marca nueva
        funciona desde el primer minuto. Los textos entre llaves —{'{{nombre}}'}, {'{{enlace}}'}—
        los rellena la plataforma: si los borras, el correo saldrá sin ese dato.
      </p>

      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Correo</th>
              <th scope="col">Estado</th>
              <th scope="col">
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>

          <tbody>
            {templates.data.map((template) => (
              <tr key={template.name}>
                <td>
                  <strong>{template.label}</strong>
                  <br />
                  <span className="muted">{template.name}</span>
                </td>
                <td>{template.overridden ? 'Propia de esta marca' : 'La original'}</td>
                <td>
                  <div className="row">
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm btn--icon-text"
                      onClick={() => setEditing(editing === template.name ? null : template.name)}
                    >
                      <IconPencil /> {editing === template.name ? 'Cerrar' : 'Editar'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editing && (
        <TemplateEditor
          key={editing}
          identityId={identity.id}
          name={editing}
          onDone={() => {
            setEditing(null);
            templates.reload();
          }}
        />
      )}
    </div>
  );
}

type TemplateDetail = {
  name: string;
  label: string;
  overridden: boolean;
  subject: string;
  html: string;
  originalSubject: string;
  originalHtml: string;
};

type Rendered = { subject: string; html: string; text: string };

/**
 * Editor de una plantilla de correo.
 *
 * Dos cosas que faltaban y sin las que esto no se puede usar:
 *
 * 1. **Arranca de la original.** Antes, una plantilla no reescrita abría el cuadro VACÍO: para
 *    cambiar una frase había que escribir el correo entero, y por el camino se pierde el pie
 *    legal o el enlace de confirmación sin que nadie lo note hasta que un alumno no puede
 *    confirmar su cuenta.
 * 2. **Vista previa.** Se compone en el servidor con la misma sustitución y la misma marca que
 *    un correo de verdad, así que lo que se ve es lo que se manda. Compararlo a ojo con el HTML
 *    en bruto no es revisar.
 */
function TemplateEditor({
  identityId,
  name,
  onDone,
}: {
  identityId: string;
  name: string;
  onDone: () => void;
}) {
  const detail = useApi<TemplateDetail>(`/admin/identities/${identityId}/templates/${name}`, [
    identityId,
    name,
  ]);

  if (detail.loading || !detail.data) {
    return <Spinner label="Cargando la plantilla…" />;
  }

  return (
    <TemplateFields
      key={`${detail.data.name}-${String(detail.data.overridden)}`}
      identityId={identityId}
      detail={detail.data}
      onDone={onDone}
    />
  );
}

function TemplateFields({
  identityId,
  detail,
  onDone,
}: {
  identityId: string;
  detail: TemplateDetail;
  onDone: () => void;
}) {
  const [subject, setSubject] = useState(detail.subject);
  const [html, setHtml] = useState(detail.html);
  const [preview, setPreview] = useState<Rendered | null>(null);
  const { busy, problem, run } = useAction();

  const changed = subject !== detail.subject || html !== detail.html;

  return (
    <div className="card admin__section">
      <h4>{detail.label}</h4>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <p className="muted">
        {detail.overridden
          ? 'Esta marca usa su propia versión. «Volver a la original» descarta lo escrito.'
          : 'Todavía usa la original, que es lo que ves aquí cargado. Al guardar, esta marca pasa a usar tu versión.'}
      </p>

      <div className="field">
        <label htmlFor={`tpl-subject-${detail.name}`}>Asunto</label>
        <input
          id={`tpl-subject-${detail.name}`}
          value={subject}
          maxLength={300}
          onChange={(e) => setSubject(e.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor={`tpl-html-${detail.name}`}>Cuerpo (HTML)</label>
        <textarea
          id={`tpl-html-${detail.name}`}
          rows={16}
          value={html}
          onChange={(e) => setHtml(e.target.value)}
        />
        <p className="muted admin__hint">
          Lo que va entre llaves lo rellena la plataforma al enviar. Si borras{' '}
          <code>{'{{enlace}}'}</code> de un correo que lo lleva, ese correo se manda sin el enlace.
        </p>
      </div>

      <div className="row">
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={busy || !subject || !html}
          onClick={() =>
            void run(
              () => api.put(`/admin/identities/${identityId}/templates/${detail.name}`, { subject, html }),
              onDone,
            )
          }
        >
          Guardar
        </button>

        <button
          type="button"
          className="btn btn--ghost btn--sm"
          disabled={busy}
          onClick={() =>
            void run(
              () =>
                api
                  .post<Rendered>(`/admin/identities/${identityId}/templates/${detail.name}/preview`, {
                    subject,
                    html,
                  })
                  .then(setPreview),
            )
          }
        >
          Ver cómo queda
        </button>

        {changed && (
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={busy}
            onClick={() => {
              setSubject(detail.originalSubject);
              setHtml(detail.originalHtml);
            }}
          >
            Restaurar el texto original
          </button>
        )}

        {detail.overridden && (
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={busy}
            onClick={() =>
              void run(
                () => api.del(`/admin/identities/${identityId}/templates/${detail.name}`),
                onDone,
              )
            }
          >
            Volver a la original
          </button>
        )}
      </div>

      {preview && (
        <div className="admin__preview-mail">
          <p className="admin__preview-label">Vista previa</p>

          <p className="admin__preview-subject">
            <span className="muted">Asunto:</span> {preview.subject}
          </p>

          {/*
            En un iframe con `sandbox` vacío: el cuerpo es HTML escrito a mano en el panel, y
            pintarlo dentro de la propia página con `dangerouslySetInnerHTML` le daría acceso a
            la sesión de quien lo revisa. Sin `allow-scripts` no se ejecuta nada, que además es
            justo cómo lo verá el alumno: los clientes de correo tampoco ejecutan scripts.
          */}
          <iframe
            className="admin__preview-frame"
            title="Vista previa del correo"
            sandbox=""
            srcDoc={preview.html}
          />
        </div>
      )}
    </div>
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
