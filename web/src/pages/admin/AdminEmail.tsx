import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconAlert, IconCheck } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/**
 * Correo saliente.
 *
 * Los datos van en el entorno del servidor, no en la base: la contraseña del buzón es un
 * secreto. Esta pantalla dice qué hay puesto, explica cómo ponerlo según el proveedor y —lo
 * único que de verdad demuestra que funciona— manda un correo de prueba.
 */
type EmailState = {
  configured: boolean;
  delivering: boolean;
  host: string | null;
  port: number;
  security: string;
  effectiveSecurity: string;
  username: string | null;
  hasPassword: boolean;
  from: string | null;
  fromName: string | null;
  redirectAllTo: string | null;
  provider: 'office365' | 'gmail' | 'otro' | 'sin configurar';
};

/**
 * Los tres proveedores de siempre, con su trampa. Los puertos y hosts son públicos y estables;
 * lo que cambia de uno a otro es qué credencial aceptan, que es donde se atasca todo el mundo.
 */
const PROVIDERS = [
  {
    id: 'office365',
    name: 'Microsoft 365 / Outlook',
    host: 'smtp.office365.com',
    port: 587,
    security: 'starttls',
    warning:
      'Microsoft desactiva la autenticación SMTP por defecto desde 2023. Hay que habilitarla ' +
      'para ese buzón en el centro de administración (Usuarios → Correo → Aplicaciones de ' +
      'correo) y, si la cuenta tiene MFA, usar una contraseña de aplicación.',
  },
  {
    id: 'gmail',
    name: 'Gmail / Google Workspace',
    host: 'smtp.gmail.com',
    port: 587,
    security: 'starttls',
    warning:
      'Google no acepta la contraseña normal de la cuenta. Hay que activar la verificación en ' +
      'dos pasos y generar una contraseña de aplicación de 16 caracteres. El puerto 465 ' +
      'también sirve: con él la conexión va cifrada desde el principio (ssl).',
  },
  {
    id: 'otro',
    name: 'Otro proveedor (IMAP/SMTP)',
    host: 'smtp.tu-proveedor.com',
    port: 587,
    security: 'auto',
    warning:
      'Vale cualquier servidor SMTP: el de tu hosting, un relay propio o un servicio de envío. ' +
      'Con «auto» se usa STARTTLS en el 587 y TLS directo en el 465.',
  },
] as const;

export function AdminEmail() {
  const { data, error, loading, reload } = useApi<EmailState>('/admin/email', []);
  const [provider, setProvider] = useState<string>('office365');
  const [to, setTo] = useState('');
  const [sent, setSent] = useState<string | null>(null);
  const { busy, problem, run } = useAction();

  if (loading) {
    return <Spinner label="Cargando configuración…" />;
  }

  if (error || !data) {
    return <ErrorMessage>{error?.message ?? 'No se ha podido leer la configuración.'}</ErrorMessage>;
  }

  const selected = PROVIDERS.find((p) => p.id === provider) ?? PROVIDERS[0];

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Correo saliente</h2>
        <span className={`chip ${data.delivering ? 'chip--brand' : 'chip--accent'}`}>
          {data.delivering ? <IconCheck size={13} /> : <IconAlert size={13} />}
          {data.delivering ? 'enviando' : 'sin enviar'}
        </span>
      </div>

      {/*
        Sin servidor, la API escribe los correos en el log en vez de mandarlos. Nada falla, y
        por eso conviene decirlo aquí: si no, se descubre cuando alguien no recibe la
        confirmación de su cuenta y no puede entrar.
      */}
      {!data.delivering && (
        <div className="alert alert--error" role="alert">
          Los correos <strong>no se están enviando</strong>: sin servidor configurado, la API los
          escribe en el log. Nadie recibe la confirmación de cuenta ni el enlace para recuperar
          la contraseña.
        </div>
      )}

      <dl className="admin__facts">
        <div>
          <dt>Servidor</dt>
          <dd>{data.host ?? <span className="chip chip--accent">sin configurar</span>}</dd>
        </div>
        <div>
          <dt>Puerto</dt>
          <dd>
            {data.port} · {data.effectiveSecurity}
          </dd>
        </div>
        <div>
          <dt>Usuario</dt>
          <dd>{data.username ?? '—'}</dd>
        </div>
        <div>
          <dt>Contraseña</dt>
          <dd>
            {data.hasPassword ? (
              <span className="chip chip--brand">configurada</span>
            ) : (
              <span className="chip chip--accent">sin configurar</span>
            )}
          </dd>
        </div>
        <div>
          <dt>Remitente</dt>
          <dd>
            {data.fromName} &lt;{data.from}&gt;
          </dd>
        </div>
        {data.redirectAllTo && (
          <div>
            <dt>Redirección de pruebas</dt>
            <dd>
              <span className="chip chip--accent">todo a {data.redirectAllTo}</span>
            </dd>
          </div>
        )}
      </dl>

      <h3>Enviar un correo de prueba</h3>

      <p className="muted">
        Es lo único que demuestra que la configuración sirve. Si el servidor rechaza el usuario o
        la contraseña, aquí se ve el motivo.
      </p>

      <form
        className="admin__inline-form"
        onSubmit={(event) => {
          event.preventDefault();
          setSent(null);

          void run(
            () => api.post<{ to: string }>('/admin/email/test', { to }),
            (result) => setSent((result as { to: string }).to),
          );
        }}
      >
        <label className="field">
          <span>Destinatario</span>
          <input
            type="email"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            placeholder="tu@correo.com"
          />
        </label>

        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          {busy ? 'Enviando…' : 'Enviar prueba'}
        </button>
      </form>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}
      {sent && (
        <div className="alert alert--success" role="status">
          Enviado a {sent}. Si no llega en un par de minutos, mira la carpeta de spam y los
          registros del proveedor.
        </div>
      )}

      <h3>Cómo configurarlo</h3>

      <div className="admin__provider-tabs" role="group" aria-label="Proveedor de correo">
        {PROVIDERS.map((option) => (
          <button
            key={option.id}
            type="button"
            className={`btn btn--sm ${provider === option.id ? 'btn--primary' : 'btn--ghost'}`}
            onClick={() => setProvider(option.id)}
          >
            {option.name}
          </button>
        ))}
      </div>

      <div className="alert alert--info">{selected.warning}</div>

      <p className="muted">
        Las variables van en el entorno de la API: en producción, en <code>infra/.env</code>. En
        local se pueden exportar antes de <code>.\dev.ps1 up</code>.
      </p>

      <pre className="admin__snippet">
        {[
          `Academy__Email__Host=${selected.host}`,
          `Academy__Email__Port=${selected.port}`,
          `Academy__Email__Security=${selected.security}`,
          'Academy__Email__Username=cuenta@tu-dominio.com',
          'Academy__Email__Password=LA-CONTRASEÑA-DE-APLICACIÓN',
          'Academy__Email__From=hola@tu-dominio.com',
          'Academy__Email__FromName=Inkoova Academy',
          '# Opcional: durante la beta, manda TODO a esta dirección en vez de al alumno.',
          '# Academy__Email__RedirectAllTo=pruebas@tu-dominio.com',
        ].join('\n')}
      </pre>

      <p className="muted">
        Después reinicia la API y vuelve a esta pantalla: el estado de arriba se lee al arrancar.{' '}
        <button type="button" className="btn btn--ghost btn--sm" onClick={reload}>
          Volver a comprobar
        </button>
      </p>
    </section>
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
        setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido enviar.');
      } finally {
        setBusy(false);
      }
    },
    [],
  );

  return { busy, problem, run };
}
