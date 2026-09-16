import { useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconAlert, IconCheck } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/**
 * Identidad de la academia.
 *
 * Al contrario que Stripe o el correo saliente, esto no son secretos: son datos de
 * presentación que el negocio cambia sin desplegar. Viven en la base, se editan aquí y el
 * escaparate los lee de `/api/branding`, que es anónimo.
 *
 * El dominio es el único campo con letra pequeña, y está explicada abajo: cambiarlo aquí NO
 * cambia el dominio con el que se firmaron los certificados ya emitidos.
 */
type Deployment = {
  /** `Academy:PublicBaseUrl`: lo que de verdad construye enlaces. Solo lectura. */
  publicBaseUrl: string | null;
  publicBaseUrlHost: string | null;
  /** Si el dominio escrito aquí coincide con el del despliegue. */
  matches: boolean;
  issuedCertificates: number;
  verificationSample: string;
};

type Branding = {
  academyName: string;
  academyTagline: string;
  logoUrl: string;
  publicDomain: string;
  supportEmail: string;
  deployment: Deployment;
};

type Form = {
  academyName: string;
  academyTagline: string;
  logoUrl: string;
  publicDomain: string;
  supportEmail: string;
};

function toForm(branding: Branding): Form {
  return {
    academyName: branding.academyName,
    academyTagline: branding.academyTagline,
    logoUrl: branding.logoUrl,
    publicDomain: branding.publicDomain,
    supportEmail: branding.supportEmail,
  };
}

export function AdminBranding() {
  const { data, error, loading, reload } = useApi<Branding>('/admin/branding', []);

  if (loading) {
    return <Spinner label="Cargando identidad…" />;
  }

  if (error || !data) {
    return <ErrorMessage>{error?.message ?? 'No se ha podido leer la identidad.'}</ErrorMessage>;
  }

  // El formulario arranca de lo guardado y a partir de ahí manda quien edita. Se le da como
  // `key` lo que hay en la base: cuando el guardado devuelve valores distintos, el formulario
  // se remonta con ellos. Sin `key` haría falta un efecto que copiase datos a estado, que es
  // exactamente lo que React desaconseja.
  const saved = toForm(data);

  return (
    <>
      <BrandingForm key={JSON.stringify(saved)} saved={saved} deployment={data.deployment} onSaved={reload} />
      <AboutForm />
    </>
  );
}

type About = { name: string; headline: string; body: string; photoUrl: string };

/**
 * La página «Sobre mí». Vive aquí porque es lo mismo que el nombre y el lema: quién es la
 * academia. Antes estaba escrita dentro del código de la SPA y cambiar una frase pedía un
 * despliegue.
 *
 * El cuerpo es TEXTO, no HTML, y la SPA lo pinta escapado. Es lo que evita que un texto pegado
 * aquí acabe ejecutando scripts en una página que ve cualquier visitante.
 */
function AboutForm() {
  const { data, loading, reload } = useApi<About>('/admin/about', []);

  if (loading || !data) {
    return null;
  }

  return <AboutFields key={JSON.stringify(data)} stored={data} onSaved={reload} />;
}

function AboutFields({ stored, onSaved }: { stored: About; onSaved: () => void }) {
  const [form, setForm] = useState<About>(stored);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const dirty =
    form.name !== stored.name ||
    form.headline !== stored.headline ||
    form.body !== stored.body ||
    form.photoUrl !== stored.photoUrl;

  async function save() {
    setBusy(true);
    setProblem(null);
    setSaved(false);

    try {
      await api.put('/admin/about', form);
      setSaved(true);
      onSaved();
    } catch (caught) {
      setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido guardar.');
    } finally {
      setBusy(false);
    }
  }

  const set = (key: keyof About) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Página «Sobre mí»</h2>
      </div>

      <p className="muted">
        Lo que se ve en <code>/sobre-mi</code>. Se guarda como texto: se admiten títulos con{' '}
        <code>## </code>, listas con <code>- </code> y párrafos separados por una línea en blanco.
        No se admite HTML, a propósito.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}
      {saved && !dirty && (
        <p className="alert alert--success" role="status">
          Guardado.
        </p>
      )}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor="about-name">Nombre de quien firma</label>
          <input
            id="about-name"
            value={form.name}
            maxLength={120}
            onChange={(e) => set('name')(e.target.value)}
          />
          <p className="muted admin__hint">Vacío: se usa el nombre de la academia.</p>
        </div>

        <div className="field">
          <label htmlFor="about-photo">Foto (URL)</label>
          <input
            id="about-photo"
            value={form.photoUrl}
            placeholder="https://…  o  /imagenes/foto.jpg"
            onChange={(e) => set('photoUrl')(e.target.value)}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor="about-headline">Entradilla</label>
        <textarea
          id="about-headline"
          rows={2}
          maxLength={400}
          value={form.headline}
          onChange={(e) => set('headline')(e.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor="about-body">Texto</label>
        <textarea
          id="about-body"
          rows={14}
          value={form.body}
          onChange={(e) => set('body')(e.target.value)}
        />
        <p className="muted admin__hint">{form.body.length} / 8000 caracteres.</p>
      </div>

      <div className="row">
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={busy || !dirty}
          onClick={() => void save()}
        >
          {busy ? 'Guardando…' : 'Guardar'}
        </button>

        <button
          type="button"
          className="btn btn--ghost btn--sm"
          disabled={busy || !dirty}
          onClick={() => setForm(stored)}
        >
          Descartar cambios
        </button>
      </div>
    </section>
  );
}

function BrandingForm({
  saved: stored,
  deployment,
  onSaved,
}: {
  saved: Form;
  deployment: Deployment;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<Form>(stored);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const problems = validate(form);
  const dirty =
    form.academyName !== stored.academyName ||
    form.academyTagline !== stored.academyTagline ||
    form.logoUrl !== stored.logoUrl ||
    form.publicDomain !== stored.publicDomain ||
    form.supportEmail !== stored.supportEmail;

  const set = (patch: Partial<Form>) => {
    setForm((current) => ({ ...current, ...patch }));
    setSaved(false);
  };

  const save = async () => {
    setBusy(true);
    setProblem(null);
    setSaved(false);

    try {
      await api.put<Branding>('/admin/branding', form);
      setSaved(true);
      onSaved();
    } catch (caught) {
      setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido guardar.');
    } finally {
      setBusy(false);
    }
  };

  // El dominio escrito no coincide con el que sirve la web: no es un error, pero sí algo que
  // hay que ver antes de guardar y no después de que alguien no encuentre la academia.
  const domainDiffers =
    form.publicDomain.length > 0 &&
    deployment.publicBaseUrlHost !== null &&
    form.publicDomain.toLowerCase() !== deployment.publicBaseUrlHost.toLowerCase();

  return (
    <section className="admin__panel admin__identity">
      <div className="admin__panel-head">
        <h2>Identidad de la academia</h2>
        <span className="chip chip--brand">
          <IconCheck size={13} />
          se aplica sin desplegar
        </span>
      </div>

      <p className="muted">
        El nombre, el logo y el lema salen en la cabecera, en el pie, en los correos y en el
        certificado. No son secretos: se guardan en la base de datos y los sirve un endpoint
        público, así que un cambio aquí se ve en la web sin tocar el servidor.
      </p>

      <BrandPreview form={form} />

      <form
        className="admin__form"
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <label className="field">
          <span>Nombre de la academia</span>
          <input
            type="text"
            value={form.academyName}
            maxLength={80}
            required
            aria-describedby="branding-name-hint"
            aria-invalid={problems.academyName !== undefined}
            onChange={(e) => set({ academyName: e.target.value })}
          />
          <span className="hint" id="branding-name-hint">
            Cabecera, pie, remitente de los correos y texto del certificado.
          </span>
          {problems.academyName && <FieldError>{problems.academyName}</FieldError>}
        </label>

        <label className="field">
          <span>Lema</span>
          <input
            type="text"
            value={form.academyTagline}
            maxLength={160}
            aria-describedby="branding-tagline-hint"
            aria-invalid={problems.academyTagline !== undefined}
            onChange={(e) => set({ academyTagline: e.target.value })}
          />
          <span className="hint" id="branding-tagline-hint">
            La frase del pie. {form.academyTagline.length}/160.
          </span>
          {problems.academyTagline && <FieldError>{problems.academyTagline}</FieldError>}
        </label>

        <label className="field">
          <span>Logo</span>
          <input
            type="text"
            value={form.logoUrl}
            maxLength={500}
            placeholder="/logo.svg  ·  https://…/logo.png"
            aria-describedby="branding-logo-hint"
            aria-invalid={problems.logoUrl !== undefined}
            onChange={(e) => set({ logoUrl: e.target.value })}
          />
          <span className="hint" id="branding-logo-hint">
            Dirección <code>https://</code> o ruta del propio sitio empezando por <code>/</code>.
            Si se deja vacío se usa la marca actual: el cuadrado con degradado.
          </span>
          {problems.logoUrl && <FieldError>{problems.logoUrl}</FieldError>}
        </label>

        <label className="field">
          <span>Dominio público</span>
          <input
            type="text"
            value={form.publicDomain}
            maxLength={253}
            placeholder="academy.inkoova.com"
            inputMode="url"
            aria-describedby="branding-domain-hint"
            aria-invalid={problems.publicDomain !== undefined}
            onChange={(e) => set({ publicDomain: e.target.value })}
          />
          <span className="hint" id="branding-domain-hint">
            Solo el dominio, sin <code>https://</code> ni barras. Es una etiqueta para enseñar en
            la web: no cambia por dónde se sirve la academia.
          </span>
          {problems.publicDomain && <FieldError>{problems.publicDomain}</FieldError>}
        </label>

        <label className="field">
          <span>Correo de contacto</span>
          <input
            type="email"
            value={form.supportEmail}
            maxLength={254}
            placeholder="hola@inkoova.com"
            aria-describedby="branding-email-hint"
            aria-invalid={problems.supportEmail !== undefined}
            onChange={(e) => set({ supportEmail: e.target.value })}
          />
          <span className="hint" id="branding-email-hint">
            Aparece en el pie y en los correos que se mandan al alumno. No es el buzón desde el
            que se envían: eso se configura en «Correo».
          </span>
          {problems.supportEmail && <FieldError>{problems.supportEmail}</FieldError>}
        </label>

        {problem && <ErrorMessage>{problem}</ErrorMessage>}

        {saved && (
          <div className="alert alert--success" role="status">
            Identidad guardada. Los visitantes la ven en cuanto recargan.
          </div>
        )}

        <div className="row">
          <button
            type="submit"
            className="btn btn--primary btn--sm"
            disabled={busy || !dirty || Object.keys(problems).length > 0}
          >
            {busy ? 'Guardando…' : 'Guardar'}
          </button>

          <button
            type="button"
            className="btn btn--ghost btn--sm"
            disabled={busy || !dirty}
            onClick={() => {
              setForm(stored);
              setProblem(null);
              setSaved(false);
            }}
          >
            Descartar cambios
          </button>
        </div>
      </form>

      <DomainNotice deployment={deployment} differs={domainDiffers} />
    </section>
  );
}

/**
 * Vista previa de la cabecera y el pie con los valores del formulario, sin guardar. No es la
 * cabecera real —eso obligaría a montar el layout entero aquí dentro— pero enseña lo único
 * que se decide en esta pantalla: qué logo, qué nombre y qué frase.
 */
function BrandPreview({ form }: { form: Form }) {
  // Se guarda QUÉ dirección falló, no un booleano: así una dirección nueva vuelve a
  // intentarse sola, sin un efecto que reinicie el estado al cambiar el campo.
  const [brokenLogo, setBrokenLogo] = useState<string | null>(null);
  const logo = form.logoUrl.trim();
  const logoBroken = brokenLogo === logo;

  const showLogo = logo.length > 0 && !logoBroken && isLogoAddress(logo);
  const name = form.academyName.trim() || 'Sin nombre';

  return (
    <div className="admin__preview">
      <span className="admin__preview-label">Vista previa</span>

      <div className="admin__preview-bar">
        <span className="admin__preview-brand">
          {showLogo ? (
            <img
              src={logo}
              alt=""
              className="admin__preview-logo"
              onError={() => setBrokenLogo(logo)}
            />
          ) : (
            <span className="admin__preview-mark" aria-hidden="true" />
          )}
          <span className="admin__preview-name">{name}</span>
        </span>

        <span className="admin__preview-nav" aria-hidden="true">
          Cursos · Precios · Entrar
        </span>
      </div>

      <div className="admin__preview-foot">
        <span>{form.academyTagline.trim() || 'Sin lema.'}</span>
        <span className="muted">
          {[form.supportEmail.trim(), form.publicDomain.trim()].filter(Boolean).join(' · ') || '—'}
        </span>
      </div>

      {logoBroken && (
        <p className="admin__preview-note" role="status">
          La imagen no carga desde esta dirección. Se seguirá usando la marca de siempre.
        </p>
      )}
    </div>
  );
}

/**
 * La letra pequeña del dominio, que es lo único de esta pantalla con consecuencias fuera de
 * ella. Se explica siempre, no solo cuando hay discrepancia: quien no sabe que existen dos
 * dominios no va a entender el aviso el día que salte.
 */
function DomainNotice({ deployment, differs }: { deployment: Deployment; differs: boolean }) {
  return (
    <div className="admin__domain-notice">
      <h3>El dominio y los certificados ya emitidos</h3>

      <dl className="admin__facts">
        <div>
          <dt>Dominio del despliegue</dt>
          <dd>
            <code>{deployment.publicBaseUrl ?? 'sin configurar'}</code>
          </dd>
        </div>
        <div>
          <dt>Certificados vigentes</dt>
          <dd>{deployment.issuedCertificates}</dd>
        </div>
        <div>
          <dt>Enlace de verificación</dt>
          <dd>
            <code>{deployment.verificationSample}</code>
          </dd>
        </div>
      </dl>

      <p className="muted">
        La URL de verificación va impresa —y en el QR— dentro del PDF de cada certificado, que
        se firmó al emitirlo y no se puede reescribir. La construye{' '}
        <code>Academy:PublicBaseUrl</code>, que se pone en el entorno del servidor, junto al DNS
        y al certificado TLS que hacen que ese dominio conteste. Los enlaces de confirmación de
        cuenta y de contraseña salen de ahí mismo.
      </p>

      <p className="muted">
        Por eso el campo de arriba <strong>no</strong> lo sustituye: cambiarlo desde un panel
        dejaría {deployment.issuedCertificates} certificado
        {deployment.issuedCertificates === 1 ? '' : 's'} apuntando a un sitio que quizá ya no
        responde. Para mudarse de dominio de verdad hay que cambiar el entorno y mantener el
        dominio antiguo resolviendo mientras haya certificados que lo lleven impreso.
      </p>

      {differs && (
        <div className="alert admin__alert-warn" role="status">
          <IconAlert size={14} /> El dominio que has escrito no es el del despliegue (
          <code>{deployment.publicBaseUrlHost}</code>). Se guardará igual y se usará donde se
          enseña el dominio, pero los enlaces de certificados y de correo seguirán saliendo del
          de arriba.
        </div>
      )}
    </div>
  );
}

function FieldError({ children }: { children: string }) {
  return (
    <span className="admin__field-error" role="alert">
      {children}
    </span>
  );
}

/**
 * Las mismas reglas que aplica la API, repetidas aquí para que el aviso salga mientras se
 * escribe. La API sigue validando: esto es comodidad, no seguridad.
 */
function validate(form: Form): Partial<Record<keyof Form, string>> {
  const problems: Partial<Record<keyof Form, string>> = {};
  const name = form.academyName.trim();

  if (name.length < 2) {
    problems.academyName = 'El nombre es obligatorio.';
  } else if (name.length > 80) {
    problems.academyName = 'Como mucho 80 caracteres.';
  }

  if (form.academyTagline.trim().length > 160) {
    problems.academyTagline = 'Como mucho 160 caracteres.';
  }

  const logo = form.logoUrl.trim();

  if (logo.length > 0 && !isLogoAddress(logo)) {
    problems.logoUrl = 'Debe empezar por https://, http:// o por «/».';
  }

  const domain = form.publicDomain.trim();

  if (domain.length > 0 && !isHostName(domain.toLowerCase())) {
    problems.publicDomain = 'Solo el dominio: academy.inkoova.com';
  }

  const email = form.supportEmail.trim();

  if (email.length > 0 && !isEmail(email)) {
    problems.supportEmail = 'No parece una dirección de correo.';
  }

  return problems;
}

/** http(s) absoluto o ruta del propio sitio. `//` queda fuera: es un absoluto sin esquema. */
function isLogoAddress(value: string): boolean {
  if (value.startsWith('//')) {
    return false;
  }

  return (
    value.startsWith('/') ||
    value.startsWith('https://') ||
    value.startsWith('http://')
  );
}

function isHostName(value: string): boolean {
  if (value.length === 0 || value.length > 253 || !value.includes('.')) {
    return false;
  }

  const labels = value.split('.');

  const wellFormed = labels.every(
    (label) =>
      label.length > 0 &&
      label.length <= 63 &&
      !label.startsWith('-') &&
      !label.endsWith('-') &&
      /^[a-z0-9-]+$/.test(label),
  );

  return wellFormed && /^[a-z]{2,}$/.test(labels[labels.length - 1]);
}

function isEmail(value: string): boolean {
  if (value.length > 254 || /\s/.test(value)) {
    return false;
  }

  const at = value.indexOf('@');

  return at > 0 && at === value.lastIndexOf('@') && isHostName(value.slice(at + 1).toLowerCase());
}
