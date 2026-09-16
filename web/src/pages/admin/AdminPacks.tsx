import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconAlert, IconCheck, IconRefresh } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/**
 * Packs sectoriales: qué versión está publicada y si sus ficheros están de verdad en el volumen.
 *
 * Los ficheros NO se suben desde aquí. Se generan con `content/tools/build_packs.py` y se
 * registran con el botón, que comprueba que existen antes de anotarlos. Subirlos por formulario
 * invitaría a publicar un pack con un documento que alguien editó a mano y que ya no coincide
 * con la fuente ni con su fecha de verificación.
 */
type PackFile = {
  fileName: string;
  sizeInBytes: number;
  /** Si está en el volumen de contenido. Un registrado que falta da un 404 al descargarlo. */
  present: boolean;
};

type AdminPack = {
  id: string;
  slug: string;
  title: string;
  sector: string;
  /** 'draft', 'comingsoon' o 'published'. */
  status: string;
  version: string;
  changelog: string | null;
  regulatoryCheckDate: string | null;
  totalSizeInBytes: number;
  files: PackFile[];
};

/** Los cinco de cada pack. El del caso cambia de nombre según el sector. */
const COMUNES = [
  'checklist-clasificacion-riesgo.docx',
  'plantilla-evaluacion-riesgos.xlsx',
  'registro-logs-exigible.xlsx',
  'clausulas-modelo-proveedor-deployer.docx',
];

const CASOS: Record<string, string> = {
  'pack-seguros-financiero': 'caso-resuelto-meridiana.pdf',
  'pack-salud': 'caso-resuelto-salud.pdf',
  'pack-rrhh': 'caso-resuelto-rrhh.pdf',
  'pack-administracion-publica': 'caso-resuelto-administracion.pdf',
  'pack-retail-ecommerce': 'caso-resuelto-retail.pdf',
  'pack-industria-energia': 'caso-resuelto-industria.pdf',
};

const kb = (bytes: number) => `${Math.round(bytes / 1024)} KB`;

const ESTADO: Record<string, string> = {
  published: 'Publicado',
  comingsoon: 'Próximamente',
  draft: 'Borrador',
};

export function AdminPacks() {
  const packs = useApi<AdminPack[]>('/admin/packs');
  const [releasing, setReleasing] = useState<AdminPack | null>(null);
  const { busy, problem, run } = useAction();

  if (packs.loading) {
    return <Spinner label="Cargando packs…" />;
  }

  if (packs.error || !packs.data) {
    return <ErrorMessage>{packs.error?.message ?? 'No se han podido cargar.'}</ErrorMessage>;
  }

  const list = packs.data;

  return (
    <section>
      <h2>Packs sectoriales</h2>

      <p className="muted">
        Los documentos se generan con <code>python content/tools/build_packs.py</code> y se
        registran aquí. El registro comprueba que cada fichero existe en el volumen antes de
        anotarlo, y guarda su huella: un pack publicado con una referencia rota da un 404 al
        primer cliente que lo descargue.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      {releasing && (
        <ReleaseForm
          pack={releasing}
          onDone={() => {
            setReleasing(null);
            packs.reload();
          }}
          onCancel={() => setReleasing(null)}
        />
      )}

      {list.map((pack) => {
        const faltan = pack.files.filter((f) => !f.present);
        const esperados = [...COMUNES, CASOS[pack.slug]].filter(Boolean);
        const sinRegistrar = esperados.filter(
          (nombre) => !pack.files.some((f) => f.fileName === nombre),
        );

        return (
          <div key={pack.id} className="card admin__panel">
            <div className="admin__panel-head">
              <h3>
                {pack.title} <span className="badge">{ESTADO[pack.status] ?? pack.status}</span>{' '}
                <span className="badge">v{pack.version}</span>
              </h3>

              <div className="row">
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon-text"
                  disabled={busy || sinRegistrar.length === 0}
                  onClick={() =>
                    void run(
                      () =>
                        api.post(`/admin/packs/${pack.id}/files`, {
                          folder: pack.slug,
                          fileNames: sinRegistrar,
                        }),
                      packs.reload,
                    )
                  }
                >
                  <IconRefresh /> Registrar ficheros
                  {sinRegistrar.length > 0 && ` (${sinRegistrar.length})`}
                </button>

                <button
                  type="button"
                  className="btn btn--primary btn--sm"
                  disabled={pack.files.length === 0}
                  onClick={() => setReleasing(pack)}
                >
                  Publicar versión
                </button>
              </div>
            </div>

            <dl className="admin__facts">
              <dt>Sector</dt>
              <dd>{pack.sector}</dd>
              <dt>Verificación normativa</dt>
              <dd>
                {pack.regulatoryCheckDate ? (
                  new Date(pack.regulatoryCheckDate).toLocaleDateString('es-ES')
                ) : (
                  <span className="muted">sin fecha</span>
                )}
              </dd>
              <dt>Peso</dt>
              <dd>{kb(pack.totalSizeInBytes)}</dd>
            </dl>

            {/* Lo que puede estar mal va antes que la lista: un fichero registrado que no está
                en el volumen no se ve mirando nombres, y es lo que rompe una descarga. */}
            {faltan.length > 0 && (
              <ErrorMessage>
                {faltan.length === 1
                  ? `Falta en el volumen: ${faltan[0].fileName}.`
                  : `Faltan ${faltan.length} ficheros en el volumen.`}{' '}
                Vuelve a generarlos con <code>build_packs.py</code>.
              </ErrorMessage>
            )}

            {pack.files.length === 0 ? (
              <p className="muted">
                Sin ficheros registrados. Genéralos y pulsa «Registrar ficheros».
              </p>
            ) : (
              <ul className="admin__history">
                {pack.files.map((file) => (
                  <li key={file.fileName} className="admin__history-item">
                    <div>
                      {file.present ? <IconCheck /> : <IconAlert />} {file.fileName}
                    </div>
                    <span className="muted">{kb(file.sizeInBytes)}</span>
                  </li>
                ))}
              </ul>
            )}

            {pack.changelog && <p className="muted">{pack.changelog}</p>}
          </div>
        );
      })}
    </section>
  );
}

/**
 * Publicar una versión avisa por correo a todo el que descargó una anterior, así que pide
 * changelog y fecha de verificación a propósito: son lo que el alumno lee para decidir si tiene
 * que volver a bajarse el pack.
 */
function ReleaseForm({
  pack,
  onDone,
  onCancel,
}: {
  pack: AdminPack;
  onDone: () => void;
  onCancel: () => void;
}) {
  const [form, setForm] = useState({
    version: pack.version,
    changelog: '',
    regulatoryCheckDate:
      pack.regulatoryCheckDate?.slice(0, 10) ?? new Date().toISOString().slice(0, 10),
  });

  const { busy, problem, run } = useAction();

  return (
    <form
      className="card admin__panel admin__form"
      onSubmit={(event) => {
        event.preventDefault();

        void run(
          () => api.post(`/admin/packs/${pack.id}/release`, form),
          onDone,
        );
      }}
    >
      <h3>Publicar {pack.title}</h3>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor="pk-version">Versión</label>
          <input
            id="pk-version"
            value={form.version}
            required
            onChange={(e) => setForm((f) => ({ ...f, version: e.target.value }))}
          />
          <p className="muted admin__hint">
            Semántica. Ya publicado, tiene que ser posterior a la actual.
          </p>
        </div>

        <div className="field">
          <label htmlFor="pk-check">Verificación normativa</label>
          <input
            id="pk-check"
            type="date"
            value={form.regulatoryCheckDate}
            required
            onChange={(e) => setForm((f) => ({ ...f, regulatoryCheckDate: e.target.value }))}
          />
          <p className="muted admin__hint">
            La fecha en que se abrieron las fuentes. No se estima.
          </p>
        </div>
      </div>

      <div className="field">
        <label htmlFor="pk-changelog">Qué cambia</label>
        <textarea
          id="pk-changelog"
          rows={3}
          value={form.changelog}
          required
          onChange={(e) => setForm((f) => ({ ...f, changelog: e.target.value }))}
        />
        <p className="muted admin__hint">
          Va en el correo que recibe quien ya descargó una versión anterior.
        </p>
      </div>

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          Publicar y avisar
        </button>

        <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </form>
  );
}

function useAction() {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const run = useCallback(async (action: () => Promise<unknown>, onDone?: () => void) => {
    setBusy(true);
    setProblem(null);

    try {
      await action();
      onDone?.();
    } catch (caught) {
      setProblem(caught instanceof ApiError ? caught.message : 'No se ha podido guardar.');
    } finally {
      setBusy(false);
    }
  }, []);

  return { busy, problem, run };
}
