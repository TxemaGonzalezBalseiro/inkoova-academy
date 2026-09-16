import { useEffect, useState } from 'react';
import { api } from '../lib/api';
import './social-login.css';

/**
 * Botones de «entrar con Google» y «entrar con Apple».
 *
 * No se pintan a ciegas: primero se pregunta a la API qué proveedores están configurados. Un
 * botón de Apple en un despliegue sin Apple lleva a un error del proveedor, y el alumno no tiene
 * forma de saber que el problema no es suyo.
 *
 * La navegación es un enlace de verdad, no un `fetch`: el navegador tiene que **salir** hacia
 * Google o Apple, con sus cookies y su ventana. Una petición XHR contra el proveedor la bloquea
 * el propio proveedor, y con razón.
 */
type Providers = { google: boolean; apple: boolean };

export function SocialLogin({ returnUrl }: { returnUrl?: string }) {
  const [providers, setProviders] = useState<Providers | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    api
      .get<Providers>('/auth/external/providers', controller.signal)
      .then((result) => setProviders(result))
      // Si la consulta falla, no se enseña ningún botón. Es mejor que enseñar uno que no va.
      .catch(() => undefined);

    return () => controller.abort();
  }, []);

  if (!providers || (!providers.google && !providers.apple)) {
    return null;
  }

  const url = (provider: string) =>
    `${api.baseUrl}/auth/external/${provider}/start` +
    (returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : '');

  return (
    <div className="social-login">
      <div className="social-login__divider">
        <span>o</span>
      </div>

      {providers.google && (
        <a className="social-login__btn" href={url('google')}>
          <GoogleMark />
          Continuar con Google
        </a>
      )}

      {providers.apple && (
        <a className="social-login__btn" href={url('apple')}>
          <AppleMark />
          Continuar con Apple
        </a>
      )}
    </div>
  );
}

/*
 * Los logotipos van en línea y con sus colores de marca fijos, no con tokens del tema: Google y
 * Apple exigen su marca tal cual en sus guías de uso, y recolorearla para que combine con el
 * modo oscuro es justo lo que no permiten.
 */
function GoogleMark() {
  return (
    <svg viewBox="0 0 18 18" width="18" height="18" aria-hidden="true" focusable="false">
      <path
        fill="#4285F4"
        d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.92c1.7-1.57 2.68-3.88 2.68-6.62Z"
      />
      <path
        fill="#34A853"
        d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.92-2.26c-.8.54-1.84.86-3.04.86-2.34 0-4.32-1.58-5.03-3.7H.96v2.33A9 9 0 0 0 9 18Z"
      />
      <path
        fill="#FBBC05"
        d="M3.97 10.72a5.4 5.4 0 0 1 0-3.44V4.95H.96a9 9 0 0 0 0 8.1l3.01-2.33Z"
      />
      <path
        fill="#EA4335"
        d="M9 3.58c1.32 0 2.5.45 3.44 1.35l2.58-2.58C13.46.9 11.43 0 9 0A9 9 0 0 0 .96 4.95l3.01 2.33C4.68 5.16 6.66 3.58 9 3.58Z"
      />
    </svg>
  );
}

function AppleMark() {
  return (
    <svg viewBox="0 0 16 20" width="16" height="20" aria-hidden="true" focusable="false">
      <path
        fill="currentColor"
        d="M13.36 10.6c-.02-2.2 1.8-3.26 1.88-3.31-1.02-1.5-2.62-1.7-3.19-1.73-1.36-.14-2.65.8-3.34.8-.69 0-1.75-.78-2.87-.76-1.48.02-2.84.86-3.6 2.18-1.53 2.66-.39 6.6 1.1 8.76.73 1.06 1.6 2.25 2.74 2.2 1.1-.04 1.51-.71 2.84-.71 1.32 0 1.7.71 2.86.69 1.18-.02 1.93-1.08 2.65-2.14.84-1.23 1.18-2.42 1.2-2.48-.03-.01-2.29-.88-2.31-3.5ZM11.2 3.9c.6-.74 1.01-1.76.9-2.78-.87.04-1.93.58-2.56 1.31-.56.65-1.05 1.7-.92 2.7.97.08 1.96-.5 2.58-1.23Z"
      />
    </svg>
  );
}
