import { useState } from 'react';
import { Link } from 'react-router-dom';
import { SocialLogin } from '../components/SocialLogin';
import { ErrorMessage } from '../components/common';
import { useDocumentTitle } from '../hooks/useApi';
import { ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import './auth.css';

/** Coincide con AccountOptions.MinimumPasswordLength en la API. */
const MIN_PASSWORD_LENGTH = 10;

export function RegisterPage() {
  useDocumentTitle('Crear cuenta');

  const { register } = useAuth();

  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [accepted, setAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

  const passwordTooShort = password.length > 0 && password.length < MIN_PASSWORD_LENGTH;

  async function submit(event: React.FormEvent) {
    event.preventDefault();

    if (passwordTooShort) {
      return;
    }

    setBusy(true);
    setError(null);

    try {
      await register(email, password, displayName);
      setDone(true);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No hemos podido crear la cuenta.');
      setBusy(false);
    }
  }

  if (done) {
    return (
      <div className="auth container">
        <div className="auth__card card">
          <h1>Revisa tu correo</h1>
          <div className="alert alert--success" role="status">
            Te hemos enviado un enlace a <strong>{email}</strong>. Confírmalo y ya podrás entrar.
          </div>
          <p className="muted">
            Si no llega en unos minutos, mira en spam. El enlace caduca en tres días.
          </p>
          <Link to="/login" className="btn btn--ghost">
            Ir a entrar
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="auth container">
      <div className="auth__card card">
        <h1>Crear cuenta</h1>
        <p className="muted">Gratis. El acceso a los cursos se elige después.</p>

        {error && <ErrorMessage>{error}</ErrorMessage>}

        <form onSubmit={submit} noValidate>
          <div className="field">
            <label htmlFor="reg-name">Nombre</label>
            <input
              id="reg-name"
              type="text"
              required
              autoComplete="name"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
            />
            <span className="hint">Es el nombre que aparecerá en tus certificados.</span>
          </div>

          <div className="field">
            <label htmlFor="reg-email">Email</label>
            <input
              id="reg-email"
              type="email"
              required
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>

          <div className="field">
            <label htmlFor="reg-password">Contraseña</label>
            <input
              id="reg-password"
              type="password"
              required
              autoComplete="new-password"
              minLength={MIN_PASSWORD_LENGTH}
              aria-describedby="reg-password-hint"
              aria-invalid={passwordTooShort}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
            <span id="reg-password-hint" className="hint">
              {passwordTooShort
                ? `Faltan ${MIN_PASSWORD_LENGTH - password.length} caracteres.`
                : `Mínimo ${MIN_PASSWORD_LENGTH} caracteres. Una frase larga vale más que símbolos raros.`}
            </span>
          </div>

          <div className="field field--check">
            <label htmlFor="reg-terms">
              <input
                id="reg-terms"
                type="checkbox"
                required
                checked={accepted}
                onChange={(event) => setAccepted(event.target.checked)}
              />
              {/*
                El texto va dentro de un span. La etiqueta es un contenedor flex y sin él cada
                trozo —"He leído los", el enlace, "y la", el otro enlace, el punto— era un
                elemento flex suelto que se rompía por su cuenta en cuatro líneas.
              */}
              <span>
                He leído los <Link to="/legal/terminos">términos</Link> y la{' '}
                <Link to="/legal/privacidad">política de privacidad</Link>.
              </span>
            </label>
          </div>

          <button
            type="submit"
            className="btn btn--primary auth__submit"
            disabled={busy || !accepted || passwordTooShort}
          >
            {busy ? 'Creando…' : 'Crear cuenta'}
          </button>
        </form>

        <SocialLogin />

        <p className="auth__links muted">
          ¿Ya tienes cuenta? <Link to="/login">Entrar</Link>
        </p>
      </div>
    </div>
  );
}
