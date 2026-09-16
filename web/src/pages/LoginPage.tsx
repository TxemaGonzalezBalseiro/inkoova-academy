import { useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { ErrorMessage } from '../components/common';
import { SocialLogin } from '../components/SocialLogin';
import { externalErrorMessage } from '../lib/externalAuth';
import { useDocumentTitle } from '../hooks/useApi';
import { ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import './auth.css';

export function LoginPage() {
  useDocumentTitle('Entrar');

  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const destination = (location.state as { from?: string } | null)?.from ?? '/cuenta';

  /*
   * El rodeo por Google o Apple no puede volver con un mensaje: vuelve con una redirección. El
   * motivo llega en la query como código, y aquí se traduce. El texto vive en la SPA y no en la
   * URL para que no acabe escrito en el historial ni en los registros del servidor.
   */
  const [params] = useSearchParams();
  const externalError = externalErrorMessage(params.get('error'));

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      await login(email, password);
      navigate(destination, { replace: true });
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No hemos podido iniciar sesión.');
      setBusy(false);
    }
  }

  return (
    <div className="auth container">
      <div className="auth__card card">
        <h1>Entrar</h1>

        {(error || externalError) && <ErrorMessage>{error ?? externalError}</ErrorMessage>}

        <form onSubmit={submit} noValidate>
          <div className="field">
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              type="email"
              required
              autoComplete="email"
              autoFocus
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>

          <div className="field">
            <label htmlFor="login-password">Contraseña</label>
            <input
              id="login-password"
              type="password"
              required
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </div>

          <button type="submit" className="btn btn--primary auth__submit" disabled={busy}>
            {busy ? 'Entrando…' : 'Entrar'}
          </button>
        </form>

        <SocialLogin returnUrl={destination} />

        <p className="auth__links">
          <Link to="/recuperar">He olvidado la contraseña</Link>
        </p>
        <p className="auth__links muted">
          ¿Aún no tienes cuenta? <Link to="/registro">Crear una</Link>
        </p>
      </div>
    </div>
  );
}
