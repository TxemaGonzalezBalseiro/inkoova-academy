import { useState } from 'react';
import { ErrorMessage } from '../../components/common';
import { api, ApiError } from '../../lib/api';

type ImportDiff = {
  courseSlug: string;
  courseExists: boolean;
  sectionsAdded: string[];
  sectionsRemoved: string[];
  lessonsAdded: string[];
  lessonsRemoved: string[];
  lessonsChanged: string[];
  quizzesAffected: string[];
  missingContentFiles: string[];
  /** Ficheros con clases gratuitas y de pago mezcladas. Ver el aviso más abajo. */
  mixedAccessFiles: string[];
  hasChanges: boolean;
};

/**
 * Importador de manifest con vista previa del diff (T-11). Nada se escribe hasta que se ve
 * qué va a cambiar: aplicar a ciegas un manifest mal generado puede vaciar un temario.
 */
export function AdminImport() {
  const [manifest, setManifest] = useState('');
  const [diff, setDiff] = useState<ImportDiff | null>(null);
  const [applied, setApplied] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function parseManifest(): unknown | null {
    try {
      return JSON.parse(manifest);
    } catch {
      setError('El JSON no es válido. Pega el fichero course.manifest.json tal cual.');
      return null;
    }
  }

  async function preview() {
    setError(null);
    setApplied(false);

    const parsed = parseManifest();
    if (!parsed) {
      return;
    }

    setBusy(true);

    try {
      setDiff(await api.post<ImportDiff>('/admin/import/preview', parsed));
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'No hemos podido calcular el diff.');
      setDiff(null);
    } finally {
      setBusy(false);
    }
  }

  async function apply() {
    setError(null);
    const parsed = parseManifest();

    if (!parsed) {
      return;
    }

    setBusy(true);

    try {
      await api.post('/admin/import/apply', parsed);
      setApplied(true);
      setDiff(null);
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'La importación ha fallado.');
    } finally {
      setBusy(false);
    }
  }

  async function loadFile(file: File) {
    setManifest(await file.text());
    setDiff(null);
    setApplied(false);
  }

  return (
    <section>
      <h2>Importar contenido</h2>
      <p className="muted">
        Genera el manifest con <code>content/tools/importer.py</code> y pégalo aquí. Importar no
        publica el curso: eso se hace después, a mano.
      </p>

      {error && <ErrorMessage>{error}</ErrorMessage>}

      {applied && (
        <div className="alert alert--success" role="status">
          Manifest aplicado. Revisa el curso y publícalo cuando esté listo.
        </div>
      )}

      <div className="field">
        <label htmlFor="manifest-file">Fichero del manifest</label>
        <input
          id="manifest-file"
          type="file"
          accept="application/json,.json"
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) {
              void loadFile(file);
            }
          }}
        />
      </div>

      <div className="field">
        <label htmlFor="manifest-json">O pégalo aquí</label>
        <textarea
          id="manifest-json"
          rows={10}
          spellCheck={false}
          value={manifest}
          onChange={(event) => {
            setManifest(event.target.value);
            setDiff(null);
          }}
          style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-xs)' }}
        />
      </div>

      <div className="row">
        <button
          type="button"
          className="btn btn--ghost"
          disabled={busy || manifest.trim().length === 0}
          onClick={() => void preview()}
        >
          {busy ? 'Calculando…' : 'Ver diff'}
        </button>

        <button
          type="button"
          className="btn btn--primary"
          disabled={busy || diff === null}
          onClick={() => void apply()}
        >
          Aplicar
        </button>
      </div>

      {diff && (
        <div className="admin__diff">
          <h3>
            {diff.courseSlug}{' '}
            <span className="muted">{diff.courseExists ? '(curso existente)' : '(curso nuevo)'}</span>
          </h3>

          {!diff.hasChanges && <p className="muted">Sin cambios: el manifest coincide con lo que ya hay.</p>}

          {/*
            El contenido se sirve por fichero entero: una clase es un ancla dentro de él. Un
            fichero con clases gratuitas y de pago mezcladas regala las de pago, y no se nota
            porque el temario sigue enseñando su candado.
          */}
          {diff.mixedAccessFiles.length > 0 && (
            <div className="alert alert--error" role="alert">
              <strong>
                {diff.mixedAccessFiles.length} fichero(s) mezclan clases gratuitas y de pago.
              </strong>{' '}
              El contenido se sirve por fichero entero, así que abrir la clase gratuita entrega
              también las de pago. Marca todas las clases de cada fichero igual.
              <ul>
                {diff.mixedAccessFiles.map((file) => (
                  <li key={file}>
                    <code>{file}</code>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {diff.missingContentFiles.length > 0 && (
            <div className="alert alert--error" role="alert">
              <strong>Faltan {diff.missingContentFiles.length} ficheros en el volumen de contenido.</strong>{' '}
              Si aplicas ahora, esas lecciones darán error al abrirse. Copia el contenido primero
              con <code>--copy-to</code>.
              <ul>
                {diff.missingContentFiles.slice(0, 10).map((file) => (
                  <li key={file}>
                    <code>{file}</code>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <DiffList title="Secciones nuevas" items={diff.sectionsAdded} tone="add" />
          <DiffList title="Secciones que desaparecen" items={diff.sectionsRemoved} tone="remove" />
          <DiffList title="Lecciones nuevas" items={diff.lessonsAdded} tone="add" />
          <DiffList title="Lecciones que desaparecen" items={diff.lessonsRemoved} tone="remove" />
          <DiffList title="Lecciones modificadas" items={diff.lessonsChanged} tone="change" />
          <DiffList title="Cuestionarios afectados" items={diff.quizzesAffected} tone="change" />

          {diff.lessonsRemoved.length > 0 && (
            <p className="muted">
              El progreso de las lecciones que desaparecen se borra en cascada. Las que se
              mantienen conservan su identificador y su progreso.
            </p>
          )}
        </div>
      )}
    </section>
  );
}

function DiffList({ title, items, tone }: { title: string; items: string[]; tone: string }) {
  if (items.length === 0) {
    return null;
  }

  return (
    <div className={`admin__diff-group admin__diff-group--${tone}`}>
      <h4>
        {title} ({items.length})
      </h4>
      <ul>
        {items.map((item) => (
          <li key={item}>
            <code>{item}</code>
          </li>
        ))}
      </ul>
    </div>
  );
}
