import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ErrorMessage, Spinner } from '../components/common';
import { useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import './auth.css';

const MIN_PASSWORD_LENGTH = 10;

export function ConfirmEmailPage() {
  useDocumentTitle('Confirmar email');

  const [params] = useSearchParams();
  const token = params.get('token');

  // La ausencia de token se conoce en el primer render: es estado inicial, no algo que
  // haya que descubrir con un efecto.
  const [state, setState] = useState<'working' | 'ok' | 'error'>(token ? 'working' : 'error');
  const [message, setMessage] = useState(
    token ? '' : 'El enlace no incluye ningún código de confirmación.',
  );

  useEffect(() => {
    if (!token) {
      return;
    }

    api
      .post('/auth/confirm-email', { token })
      .then(() => setState('ok'))
      .catch((caught: unknown) => {
        setState('error');
        setMessage(
          caught instanceof ApiError ? caught.message : 'No hemos podido confirmar el email.',
        );
      });
  }, [token]);

  return (
    <div className="auth container">
      <div className="auth__card card">
        <h1>Confirmar email</h1>

        {state === 'working' && <Spinner label="Confirmando…" />}

        {state === 'ok' && (
          <>
            <div className="alert alert--success" role="status">
              Email confirmado. Tu cuenta ya está activa.
            </div>
            <Link to="/login" className="btn btn--primary auth__submit">
              Entrar
            </Link>
          </>
        )}

        {state === 'error' && (
          <>
            <ErrorMessage>{message}</ErrorMessage>
            <p className="muted">
              Los enlaces caducan a los tres días y solo se pueden usar una vez. Puedes pedir uno
              nuevo desde la pantalla de entrada.
            </p>
            <Link to="/login" className="btn btn--ghost auth__submit">
              Volver
            </Link>
          </>
        )}
      </div>
    </div>
  );
}

export function ForgotPasswordPage() {
  useDocumentTitle('Recuperar contraseña');

  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);

    try {
      await api.post('/auth/forgot-password', { email });
    } finally {
      // Siempre el mismo mensaje: si dijéramos "ese email no existe" tendríamos un
      // comprobador de cuentas gratis para cualquiera.
      setSent(true);
      setBusy(false);
    }
  }

  return (
    <div className="auth container">
      <div className="auth__card card">
        <h1>Recuperar contraseña</h1>

        {sent ? (
          <>
            <div className="alert alert--success" role="status">
              Si hay una cuenta con ese email, le hemos enviado un enlace para cambiar la
              contraseña.
            </div>
            <Link to="/login" className="btn btn--ghost auth__submit">
              Volver
            </Link>
          </>
        ) : (
          <form onSubmit={submit}>
            <p className="muted">Te enviamos un enlace válido durante dos horas.</p>

            <div className="field">
              <label htmlFor="forgot-email">Email</label>
              <input
                id="forgot-email"
                type="email"
                required
                autoComplete="email"
                autoFocus
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </div>

            <button type="submit" className="btn btn--primary auth__submit" disabled={busy}>
              {busy ? 'Enviando…' : 'Enviar enlace'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}

export function ResetPasswordPage() {
  useDocumentTitle('Nueva contraseña');

  const [params] = useSearchParams();
  const token = params.get('token') ?? '';

  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

  const tooShort = password.length > 0 && password.length < MIN_PASSWORD_LENGTH;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      await api.post('/auth/reset-password', { token, newPassword: password });
      setDone(true);
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'No hemos podido cambiar la contraseña.',
      );
      setBusy(false);
    }
  }

  return (
    <div className="auth container">
      <div className="auth__card card">
        <h1>Nueva contraseña</h1>

        {done ? (
          <>
            <div className="alert alert--success" role="status">
              Contraseña cambiada. Hemos cerrado el resto de sesiones abiertas.
            </div>
            <Link to="/login" className="btn btn--primary auth__submit">
              Entrar
            </Link>
          </>
        ) : (
          <form onSubmit={submit}>
            {error && <ErrorMessage>{error}</ErrorMessage>}

            <div className="field">
              <label htmlFor="reset-password">Contraseña nueva</label>
              <input
                id="reset-password"
                type="password"
                required
                autoComplete="new-password"
                minLength={MIN_PASSWORD_LENGTH}
                autoFocus
                aria-invalid={tooShort}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
              <span className="hint">Mínimo {MIN_PASSWORD_LENGTH} caracteres.</span>
            </div>

            <button
              type="submit"
              className="btn btn--primary auth__submit"
              disabled={busy || tooShort || !token}
            >
              {busy ? 'Guardando…' : 'Cambiar contraseña'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
