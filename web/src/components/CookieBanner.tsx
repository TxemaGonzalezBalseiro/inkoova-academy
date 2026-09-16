import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../lib/api';

type ConsentState = {
  policyVersion: string;
  analytics: boolean;
  marketing: boolean;
  decided: boolean;
};

/**
 * Banner de consentimiento real (T-14): nada de analítica se carga antes de que alguien
 * acepte. Rechazar es tan fácil como aceptar — un botón, al mismo nivel — porque un
 * "rechazar" escondido tras dos clics no es consentimiento libre.
 */
export function CookieBanner() {
  const [consent, setConsent] = useState<ConsentState | null>(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    let cancelled = false;

    api
      .get<ConsentState>('/privacy/consent')
      .then((state) => {
        if (cancelled) {
          return;
        }

        setConsent(state);
        setVisible(!state.decided);

        if (state.analytics) {
          loadAnalytics();
        }
      })
      .catch(() => {
        // Si la API no responde, no se muestra el banner ni se carga analítica: el
        // comportamiento por defecto es el que no trata datos.
      });

    return () => {
      cancelled = true;
    };
  }, []);

  async function decide(analytics: boolean) {
    setVisible(false);

    try {
      await api.post('/privacy/consent', { analytics, marketing: false });
    } catch {
      // La decisión se aplica igualmente en esta sesión aunque no se haya podido registrar.
    }

    if (analytics) {
      loadAnalytics();
    }
  }

  if (!visible || !consent) {
    return null;
  }

  return (
    <aside className="cookie-banner" role="dialog" aria-live="polite" aria-label="Consentimiento de cookies">
      <p>
        Usamos cookies propias necesarias para que la sesión y el acceso a los cursos funcionen.
        Nos gustaría añadir analítica sin cookies de terceros para saber qué páginas ayudan. Tú
        decides.
      </p>
      <div className="cookie-banner__actions">
        <button type="button" className="btn btn--ghost btn--sm" onClick={() => void decide(false)}>
          Solo las necesarias
        </button>
        <button type="button" className="btn btn--primary btn--sm" onClick={() => void decide(true)}>
          Aceptar analítica
        </button>
      </div>
      <p className="muted" style={{ marginTop: 'var(--space-3)', marginBottom: 0, fontSize: 'var(--text-xs)' }}>
        Detalle en la <Link to="/legal/cookies">política de cookies</Link>.
      </p>
    </aside>
  );
}

/**
 * Carga Plausible solo tras el consentimiento (T-15). Es analítica sin cookies, pero aun
 * así no se inyecta antes de que alguien lo autorice.
 */
function loadAnalytics(): void {
  if (document.getElementById('inkoova-analytics')) {
    return;
  }

  const domain = import.meta.env.VITE_ANALYTICS_DOMAIN;
  const host = import.meta.env.VITE_ANALYTICS_HOST;

  if (!domain || !host) {
    // TODO(T-15): configurar la instancia self-hosted antes del lanzamiento.
    return;
  }

  const script = document.createElement('script');
  script.id = 'inkoova-analytics';
  script.defer = true;
  script.dataset.domain = domain;
  script.src = `${host}/js/script.js`;
  document.head.appendChild(script);
}
