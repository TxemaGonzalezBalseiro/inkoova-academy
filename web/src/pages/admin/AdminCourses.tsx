import { useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

type AdminCourse = {
  id: string;
  slug: string;
  title: string;
  status: string;
  level: string;
  isFeatured: boolean;
  isNew: boolean;
  sections: number;
  lessons: number;
  hours: number;
};

export function AdminCourses() {
  const { data, error, loading, reload } = useApi<AdminCourse[]>('/admin/courses');
  const [busy, setBusy] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  async function run(courseId: string, action: () => Promise<unknown>) {
    setBusy(courseId);
    setActionError(null);

    try {
      await action();
      reload();
    } catch (caught) {
      setActionError(caught instanceof ApiError ? caught.message : 'La acción ha fallado.');
    } finally {
      setBusy(null);
    }
  }

  if (loading) {
    return <Spinner />;
  }

  if (error || !data) {
    return <ErrorMessage>No hemos podido cargar los cursos.</ErrorMessage>;
  }

  return (
    <section>
      <h2>Cursos</h2>
      <p className="muted">
        Publicar o despublicar invalida la caché del catálogo al instante: el cambio se ve en la
        portada sin desplegar nada.
      </p>

      {actionError && <ErrorMessage>{actionError}</ErrorMessage>}

      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Curso</th>
              <th>Estado</th>
              <th>Contenido</th>
              <th>Marca</th>
              <th>Marcas</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {data.map((course) => (
              <tr key={course.id}>
                <td>
                  <strong>{course.title}</strong>
                  <br />
                  <code className="muted">{course.slug}</code>
                </td>
                <td>
                  <span className={`status-pill status-pill--${course.status === 'published' ? 'active' : 'none'}`}>
                    {statusLabel(course.status)}
                  </span>
                </td>
                <td className="muted">
                  {course.sections} secciones · {course.lessons} clases · {course.hours} h
                </td>
                <td>
                  <label className="admin__flag">
                    <input
                      type="checkbox"
                      checked={course.isFeatured}
                      disabled={busy === course.id}
                      onChange={(event) =>
                        void run(course.id, () =>
                          api.put(`/admin/courses/${course.id}/flags`, {
                            isFeatured: event.target.checked,
                            isNew: course.isNew,
                            coverImageUrl: null,
                          }),
                        )
                      }
                    />{' '}
                    Destacado
                  </label>
                  <label className="admin__flag">
                    <input
                      type="checkbox"
                      checked={course.isNew}
                      disabled={busy === course.id}
                      onChange={(event) =>
                        void run(course.id, () =>
                          api.put(`/admin/courses/${course.id}/flags`, {
                            isFeatured: course.isFeatured,
                            isNew: event.target.checked,
                            coverImageUrl: null,
                          }),
                        )
                      }
                    />{' '}
                    Nuevo
                  </label>
                </td>
                <td>
                  {course.status === 'published' ? (
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm"
                      disabled={busy === course.id}
                      onClick={() => void run(course.id, () => api.post(`/admin/courses/${course.id}/unpublish`))}
                    >
                      Despublicar
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="btn btn--primary btn--sm"
                      disabled={busy === course.id || course.lessons === 0}
                      title={course.lessons === 0 ? 'No se puede publicar un curso sin lecciones' : undefined}
                      onClick={() => void run(course.id, () => api.post(`/admin/courses/${course.id}/publish`))}
                    >
                      Publicar
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function statusLabel(status: string): string {
  switch (status) {
    case 'published':
      return 'Publicado';
    case 'comingsoon':
      return 'Próximamente';
    default:
      return 'Borrador';
  }
}
