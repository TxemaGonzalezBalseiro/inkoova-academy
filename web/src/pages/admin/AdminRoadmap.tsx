import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconPencil, IconPlus, IconTrash } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/**
 * El itinerario de aprendizaje: qué se estudia antes de qué.
 *
 * Los nodos se enlazan por CLAVE y no por identificador, porque la clave sobrevive a renombrar
 * el título —que es lo que más se hace— y el identificador no significa nada para quien edita.
 *
 * Las tres cosas que la API rechaza y que conviene entender antes de tocar aquí: claves
 * repetidas, prerrequisitos que no existen y ciclos. Un ciclo no da error en ninguna pantalla:
 * deja los nodos implicados bloqueados para siempre, porque cada uno espera al otro.
 */
type RoadmapNode = {
  id: string;
  key: string;
  title: string;
  description: string;
  courseId: string | null;
  courseSlug: string | null;
  order: number;
  prerequisites: string[];
};

type AdminCourse = { id: string; slug: string; title: string };

export function AdminRoadmap() {
  const nodes = useApi<RoadmapNode[]>('/admin/roadmap', []);
  const courses = useApi<AdminCourse[]>('/admin/courses', []);
  const [editing, setEditing] = useState<RoadmapNode | 'new' | null>(null);
  const { busy, problem, run } = useAction();

  if (nodes.loading || courses.loading) {
    return <Spinner label="Cargando itinerario…" />;
  }

  if (nodes.error) {
    return <ErrorMessage>{nodes.error.message}</ErrorMessage>;
  }

  const list = nodes.data ?? [];

  return (
    <section>
      <div className="admin__panel-head">
        <h2>Itinerario</h2>

        <button
          type="button"
          className="btn btn--primary btn--sm btn--icon-text"
          onClick={() => setEditing('new')}
        >
          <IconPlus /> Nuevo nodo
        </button>
      </div>

      <p className="muted">
        Lo que ve el alumno en <code>/roadmap</code>: qué puede empezar ya, qué lleva a medias y
        qué está bloqueado hasta terminar lo anterior.
      </p>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      {editing !== null && (
        <NodeForm
          node={editing === 'new' ? null : editing}
          nodes={list}
          courses={courses.data ?? []}
          onDone={() => {
            setEditing(null);
            nodes.reload();
          }}
          onCancel={() => setEditing(null)}
        />
      )}

      {list.length === 0 ? (
        <p className="muted">El itinerario está vacío.</p>
      ) : (
        <div className="table-scroll">
          <table>
            <caption className="sr-only">Nodos del itinerario</caption>
            <thead>
              <tr>
                <th scope="col">#</th>
                <th scope="col">Nodo</th>
                <th scope="col">Curso</th>
                <th scope="col">Depende de</th>
                <th scope="col">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {list.map((node) => (
                <tr key={node.id}>
                  <td>{node.order}</td>
                  <td>
                    <strong>{node.title}</strong>
                    <br />
                    <code>{node.key}</code>
                  </td>
                  <td>
                    {node.courseSlug ? (
                      <code>{node.courseSlug}</code>
                    ) : (
                      <span className="muted">sin curso</span>
                    )}
                  </td>
                  <td>
                    {node.prerequisites.length === 0 ? (
                      <span className="muted">nada</span>
                    ) : (
                      node.prerequisites.map((k) => (
                        <span key={k}>
                          <code>{k}</code>{' '}
                        </span>
                      ))
                    )}
                  </td>
                  <td>
                    <div className="row">
                      <button
                        type="button"
                        className="btn btn--ghost btn--sm btn--icon-text"
                        onClick={() => setEditing(node)}
                      >
                        <IconPencil /> Editar
                      </button>

                      <button
                        type="button"
                        className="btn btn--ghost btn--sm btn--icon-text"
                        disabled={busy}
                        onClick={() => {
                          if (!window.confirm(`¿Borrar «${node.title}» del itinerario?`)) return;

                          void run(() => api.del(`/admin/roadmap/${node.id}`), nodes.reload);
                        }}
                      >
                        <IconTrash /> Borrar
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function NodeForm({
  node,
  nodes,
  courses,
  onDone,
  onCancel,
}: {
  node: RoadmapNode | null;
  nodes: RoadmapNode[];
  courses: AdminCourse[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const [form, setForm] = useState({
    key: node?.key ?? '',
    title: node?.title ?? '',
    description: node?.description ?? '',
    courseId: node?.courseId ?? '',
    order: String(node?.order ?? nodes.length + 1),
    prerequisites: node?.prerequisites ?? [],
  });

  const { busy, problem, run } = useAction();

  // Un nodo no puede depender de sí mismo, así que no se ofrece como opción.
  const choosable = nodes.filter((n) => n.id !== node?.id);

  const toggle = (key: string) =>
    setForm((f) => ({
      ...f,
      prerequisites: f.prerequisites.includes(key)
        ? f.prerequisites.filter((k) => k !== key)
        : [...f.prerequisites, key],
    }));

  return (
    <form
      className="card admin__panel admin__form"
      onSubmit={(event) => {
        event.preventDefault();

        void run(
          () =>
            api.post('/admin/roadmap', {
              id: node?.id ?? null,
              key: form.key,
              title: form.title,
              description: form.description,
              courseId: form.courseId || null,
              order: Number(form.order),
              prerequisites: form.prerequisites,
            }),
          onDone,
        );
      }}
    >
      <h3>{node ? 'Editar nodo' : 'Nuevo nodo'}</h3>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="admin__form-grid">
        <div className="field">
          <label htmlFor="rm-title">Título</label>
          <input
            id="rm-title"
            value={form.title}
            required
            maxLength={200}
            onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
          />
        </div>

        <div className="field">
          <label htmlFor="rm-key">Clave</label>
          <input
            id="rm-key"
            value={form.key}
            required
            maxLength={60}
            onChange={(e) => setForm((f) => ({ ...f, key: e.target.value }))}
          />
          <p className="muted admin__hint">
            Es lo que usan los otros nodos para apuntar a este. Cambiarla cuando ya la usan se
            rechaza, para no partir el itinerario.
          </p>
        </div>

        <div className="field">
          <label htmlFor="rm-order">Orden</label>
          <input
            id="rm-order"
            type="number"
            min={1}
            value={form.order}
            required
            onChange={(e) => setForm((f) => ({ ...f, order: e.target.value }))}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor="rm-course">Curso</label>
        <select
          id="rm-course"
          value={form.courseId}
          onChange={(e) => setForm((f) => ({ ...f, courseId: e.target.value }))}
        >
          <option value="">Sin curso asociado</option>
          {courses.map((course) => (
            <option key={course.id} value={course.id}>
              {course.title}
            </option>
          ))}
        </select>
        <p className="muted admin__hint">
          De aquí sale el progreso: un nodo se marca completado cuando el alumno termina las
          clases obligatorias de su curso.
        </p>
      </div>

      <div className="field">
        <label htmlFor="rm-desc">Descripción</label>
        <textarea
          id="rm-desc"
          rows={2}
          value={form.description}
          onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
        />
      </div>

      {choosable.length > 0 && (
        <fieldset className="field">
          <legend>Depende de</legend>

          <div className="admin__checks">
            {choosable.map((other) => (
              <label key={other.id} className="admin__flag">
                <input
                  type="checkbox"
                  checked={form.prerequisites.includes(other.key)}
                  onChange={() => toggle(other.key)}
                />
                <span>{other.title}</span>
              </label>
            ))}
          </div>

          <p className="muted admin__hint">
            Hasta que el alumno no termine todo esto, el nodo le sale bloqueado.
          </p>
        </fieldset>
      )}

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          Guardar
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
