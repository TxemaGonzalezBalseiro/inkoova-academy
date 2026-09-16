import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../components/common';
import { useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import './account.css';

/** Derechos de acceso, portabilidad y supresión (T-14, RGPD arts. 15, 17 y 20). */
export function PrivacyDataPage() {
  useDocumentTitle('Mis datos');

  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const canDelete = confirmation.trim().toUpperCase() === 'ELIMINAR';

  async function deleteAccount() {
    setBusy(true);
    setError(null);

    try {
      await api.del('/me');
      await logout();
      navigate('/', { replace: true });
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No hemos podido eliminar la cuenta.');
      setBusy(false);
    }
  }

  return (
    <div className="container section account" style={{ maxWidth: 720 }}>
      <header className="section-header">
        <h1>Mis datos</h1>
        <p className="muted">
          <Link to="/cuenta">← Volver a mi cuenta</Link>
        </p>
      </header>

      <section>
        <h2>Descargar mis datos</h2>
        <p className="muted">
          Un fichero JSON con tu cuenta, tu progreso, tus intentos de test, tus certificados y tus
          compras. Es tu derecho de portabilidad.
        </p>
        <a className="btn btn--primary" href="/api/me/data-export">
          Descargar en JSON
        </a>
      </section>

      <section>
        <h2>Qué guardamos y por qué</h2>
        <dl className="faq-list">
          <dt>Cuenta y perfil</dt>
          <dd>
            Email y nombre, para identificarte y emitir certificados a tu nombre. Base legal:
            ejecución del contrato.
          </dd>

          <dt>Progreso y resultados</dt>
          <dd>
            Qué clases has completado y qué has sacado en los tests, para que la plataforma sepa
            dónde lo dejaste y pueda emitir certificados.
          </dd>

          <dt>Pagos y facturación</dt>
          <dd>
            Importes, fechas e identificadores de Stripe. Los datos de tu tarjeta no pasan por
            nuestros servidores en ningún momento: los gestiona Stripe.
          </dd>

          <dt>Registro de consentimiento</dt>
          <dd>
            Tu decisión sobre analítica y la versión de la política que aceptaste, porque hay que
            poder demostrar el consentimiento.
          </dd>
        </dl>
      </section>

      <section>
        <h2>Eliminar mi cuenta</h2>

        <div className="alert alert--error" role="note">
          <strong>Esto no se puede deshacer.</strong> Se borran tu email, tu nombre, tu progreso y
          tus credenciales, y pierdes el acceso a los cursos de inmediato. Tus certificados dejan
          de mostrar tu nombre al verificarlos.
        </div>

        <p className="muted">
          Las facturas ya emitidas se conservan sin datos personales asociables a ti, porque la
          normativa fiscal obliga a guardarlas. Es la única excepción.
        </p>

        {error && <ErrorMessage>{error}</ErrorMessage>}

        <div className="field">
          <label htmlFor="delete-confirm">
            Escribe <strong>ELIMINAR</strong> para confirmar
          </label>
          <input
            id="delete-confirm"
            type="text"
            value={confirmation}
            autoComplete="off"
            onChange={(event) => setConfirmation(event.target.value)}
          />
        </div>

        <button
          type="button"
          className="btn btn--primary"
          style={{ background: 'var(--ink-error)' }}
          disabled={!canDelete || busy}
          onClick={() => void deleteAccount()}
        >
          {busy ? 'Eliminando…' : `Eliminar la cuenta de ${user?.email}`}
        </button>
      </section>
    </div>
  );
}
