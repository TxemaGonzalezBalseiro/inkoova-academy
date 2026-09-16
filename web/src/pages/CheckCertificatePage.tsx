import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ErrorMessage } from '../components/common';
import { formatDate } from '../lib/format';
import { useDocumentTitle } from '../hooks/useApi';
import { api } from '../lib/api';
import type { CertificateVerification } from '../lib/types';
import './certificate.css';

/**
 * Verificación pública de certificados (T-10). Sin login, con rate limit en la API. Un
 * código inválido y un código revocado se distinguen: el segundo dice por qué.
 */
export function CheckCertificatePage() {
  useDocumentTitle('Verificar certificado');

  const { code: codeFromUrl } = useParams();
  const navigate = useNavigate();

  const [code, setCode] = useState(codeFromUrl ?? '');
  const [result, setResult] = useState<CertificateVerification | null>(null);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (codeFromUrl) {
      void verify(codeFromUrl);
    }
  }, [codeFromUrl]);

  async function verify(value: string) {
    setBusy(true);
    setFailed(false);

    try {
      setResult(await api.get<CertificateVerification>(`/certificates/${encodeURIComponent(value)}`));
    } catch {
      setFailed(true);
      setResult(null);
    } finally {
      setBusy(false);
    }
  }

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const normalized = code.trim().toUpperCase();
    navigate(`/check-certificate/${encodeURIComponent(normalized)}`);
    void verify(normalized);
  }

  return (
    <div className="container section certificate">
      <header className="section-header">
        <h1>Verificar un certificado</h1>
        <p className="muted">
          Introduce el código que aparece en el certificado. La comprobación es pública: cualquiera
          puede hacerla, sin cuenta.
        </p>
      </header>

      <form onSubmit={submit} className="certificate__form">
        <div className="field">
          <label htmlFor="cert-code">Código</label>
          <input
            id="cert-code"
            type="text"
            required
            placeholder="INK-XXXX-XXXX"
            value={code}
            spellCheck={false}
            autoCapitalize="characters"
            onChange={(event) => setCode(event.target.value.toUpperCase())}
          />
          <span className="hint">Formato INK seguido de dos grupos de cuatro caracteres.</span>
        </div>

        <button type="submit" className="btn btn--primary" disabled={busy || code.trim().length === 0}>
          {busy ? 'Comprobando…' : 'Verificar'}
        </button>
      </form>

      {failed && <ErrorMessage>No hemos podido comprobar el código. Inténtalo de nuevo.</ErrorMessage>}

      {result && (
        <div
          className={`certificate__result ${result.isValid ? 'certificate__result--valid' : 'certificate__result--invalid'}`}
          role="status"
          aria-live="polite"
        >
          {result.isValid ? (
            <>
              <p className="certificate__stamp">✓ Certificado válido</p>
              <dl>
                <dt>Alumno</dt>
                <dd>{result.studentName}</dd>
                <dt>Formación</dt>
                <dd>{result.subject}</dd>
                <dt>Fecha de emisión</dt>
                <dd>{formatDate(result.issuedOn)}</dd>
              </dl>

              {/*
                Quien verifica suele ser alguien que contrata, y lo siguiente que quiere es ver
                el documento. Sin esto, la comprobación terminaba en tres líneas de texto.
              */}
              <div className="row certificate__downloads">
                <a className="btn btn--ghost btn--sm" href={`/api/certificates/${code}/pdf`}>
                  Ver el certificado (PDF)
                </a>
                <a
                  className="btn btn--ghost btn--sm"
                  href={`/api/certificates/${code}/imagen`}
                  download={`${code}.png`}
                >
                  Imagen
                </a>
              </div>
            </>
          ) : (
            <>
              <p className="certificate__stamp">✕ No válido</p>
              <p>
                {result.revocationReason
                  ? `Este certificado fue revocado: ${result.revocationReason}`
                  : 'No existe ningún certificado con ese código.'}
              </p>
            </>
          )}
        </div>
      )}
    </div>
  );
}
