import { useCallback, useState } from 'react';
import { ErrorMessage, Spinner } from '../../components/common';
import { IconPencil, IconPlus, IconTrash } from '../../components/icons';
import { useApi } from '../../hooks/useApi';
import { api, ApiError } from '../../lib/api';

/*
 * El editor trabaja contra /admin/authoring, no contra el catálogo público: necesita los
 * identificadores de curso, sección y clase, y el DTO público no los lleva a propósito.
 */
type AdminCourse = {
  id: string;
  slug: string;
  title: string;
  status: string;
  sections: number;
  lessons: number;
};

type AuthoringLesson = {
  id: string;
  slug: string;
  title: string;
  type: string;
  durationMinutes: number;
  contentRef: string;
  isFreePreview: boolean;
  isRequired: boolean;
};

type AuthoringSection = {
  id: string;
  title: string;
  order: number;
  lessons: AuthoringLesson[];
};

type AuthoringCourse = {
  id: string;
  slug: string;
  title: string;
  shortDescription: string;
  longDescription: string;
  level: string;
  status: string;
  sections: AuthoringSection[];
};

/**
 * Gestor de contenidos: crear y editar cursos, secciones y clases sin pasar por el importador.
 *
 * Dos cosas no se pueden cambiar y se dice en pantalla, no solo en el código: el identificador
 * de un curso y el de una clase. Son sus URLs públicas y el progreso de cada alumno apunta a
 * ellas, así que renombrarlos rompería enlaces ya repartidos y dejaría el avance colgando.
 */
const LEVELS = [
  { value: 'intro', label: 'Introductorio' },
  { value: 'intermediate', label: 'Intermedio' },
  { value: 'advanced', label: 'Avanzado' },
];

const LESSON_TYPES = [
  { value: 'slides', label: 'Slides' },
  { value: 'lab', label: 'Ejercicio' },
  { value: 'quiz', label: 'Quiz' },
  { value: 'video', label: 'Vídeo' },
];

export function AdminAuthoring() {
  const { data: courses, error, loading, reload } = useApi<AdminCourse[]>('/admin/courses', []);
  const [editing, setEditing] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  if (loading) {
    return <Spinner label="Cargando cursos…" />;
  }

  if (error) {
    return <ErrorMessage>{error.message}</ErrorMessage>;
  }

  if (editing) {
    return (
      <CourseEditor
        courseSlug={editing}
        onClose={() => {
          setEditing(null);
          reload();
        }}
      />
    );
  }

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>Gestor de contenidos</h2>
        <button
          type="button"
          className="btn btn--primary btn--sm btn--icon-text"
          onClick={() => setCreating((open) => !open)}
          aria-expanded={creating}
        >
          <IconPlus />
          Curso nuevo
        </button>
      </div>

      {creating && (
        <NewCourseForm
          onCreated={() => {
            setCreating(false);
            reload();
          }}
        />
      )}

      <div className="table-scroll">
      <table>
        <caption className="sr-only">Cursos que se pueden editar</caption>
        <thead>
          <tr>
            <th scope="col">Curso</th>
            <th scope="col">Identificador</th>
            <th scope="col">Temario</th>
            <th scope="col">Estado</th>
            <th scope="col">
              <span className="sr-only">Acciones</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {(courses ?? []).map((course) => (
            <tr key={course.id}>
              <td>{course.title}</td>
              <td>
                <code>{course.slug}</code>
              </td>
              <td>
                {course.sections} secciones · {course.lessons} clases
              </td>
              <td>{course.status}</td>
              <td>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm btn--icon-text"
                  onClick={() => setEditing(course.slug)}
                >
                  <IconPencil />
                  Editar
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>
    </section>
  );
}

function NewCourseForm({ onCreated }: { onCreated: () => void }) {
  const [form, setForm] = useState({
    slug: '',
    title: '',
    shortDescription: '',
    longDescription: '',
    level: 'intermediate',
    priceEuros: '99',
  });

  const { busy, problem, run } = useAction();

  return (
    <form
      className="admin__form"
      onSubmit={(event) => {
        event.preventDefault();
        void run(
          () =>
            api.post('/admin/authoring/courses', {
              ...form,
              // El precio se escribe en euros y viaja en céntimos: la moneda es entera en el
              // dominio y redondear en el servidor escondería el céntimo que se pierde.
              priceCents: Math.round(Number(form.priceEuros) * 100),
            }),
          onCreated,
        );
      }}
    >
      <div className="admin__form-grid">
        <label className="field">
          <span>Identificador (URL)</span>
          <input
            required
            value={form.slug}
            onChange={(e) => setForm({ ...form, slug: e.target.value })}
            placeholder="agentes-en-produccion"
          />
          <small className="muted">No se puede cambiar después. Solo minúsculas y guiones.</small>
        </label>

        <label className="field">
          <span>Título</span>
          <input
            required
            value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })}
          />
        </label>

        <label className="field">
          <span>Nivel</span>
          <select value={form.level} onChange={(e) => setForm({ ...form, level: e.target.value })}>
            {LEVELS.map((level) => (
              <option key={level.value} value={level.value}>
                {level.label}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>Precio (€)</span>
          <input
            required
            type="number"
            min="0"
            step="1"
            value={form.priceEuros}
            onChange={(e) => setForm({ ...form, priceEuros: e.target.value })}
          />
        </label>
      </div>

      <label className="field">
        <span>Descripción corta</span>
        <input
          required
          value={form.shortDescription}
          onChange={(e) => setForm({ ...form, shortDescription: e.target.value })}
        />
      </label>

      <label className="field">
        <span>Descripción larga</span>
        <textarea
          rows={3}
          value={form.longDescription}
          onChange={(e) => setForm({ ...form, longDescription: e.target.value })}
        />
      </label>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
        {busy ? 'Creando…' : 'Crear curso'}
      </button>

      <p className="muted">
        El curso se crea en borrador, con su producto asociado. No es visible hasta publicarlo.
      </p>
    </form>
  );
}

function CourseEditor({ courseSlug, onClose }: { courseSlug: string; onClose: () => void }) {
  const { data: course, error, loading, reload } = useApi<AuthoringCourse>(
    `/admin/authoring/courses/${courseSlug}`,
    [courseSlug],
  );

  const { busy, problem, run } = useAction();
  const [newSection, setNewSection] = useState('');

  if (loading) {
    return <Spinner label="Cargando temario…" />;
  }

  if (error || !course) {
    return <ErrorMessage>{error?.message ?? 'No se ha podido cargar el curso.'}</ErrorMessage>;
  }

  return (
    <section className="admin__panel">
      <div className="admin__panel-head">
        <h2>{course.title}</h2>
        <button type="button" className="btn btn--ghost btn--sm" onClick={onClose}>
          Volver a la lista
        </button>
      </div>

      <CourseFacts course={course} onSaved={reload} />

      <h3>Temario</h3>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      {course.sections.map((section) => (
        <SectionEditor
          key={section.id}
          courseId={course.id}
          section={section}
          onChanged={reload}
        />
      ))}

      <form
        className="admin__inline-form"
        onSubmit={(event) => {
          event.preventDefault();
          void run(
            () => api.post(`/admin/authoring/courses/${course.id}/sections`, { title: newSection }),
            () => {
              setNewSection('');
              reload();
            },
          );
        }}
      >
        <label className="field">
          <span className="sr-only">Título de la sección nueva</span>
          <input
            required
            value={newSection}
            onChange={(e) => setNewSection(e.target.value)}
            placeholder="B8 · Nueva sección"
          />
        </label>

        <button type="submit" className="btn btn--ghost btn--sm btn--icon-text" disabled={busy}>
          <IconPlus />
          Añadir sección
        </button>
      </form>
    </section>
  );
}

function CourseFacts({ course, onSaved }: { course: AuthoringCourse; onSaved: () => void }) {
  const [form, setForm] = useState({
    title: course.title,
    shortDescription: course.shortDescription,
    longDescription: course.longDescription ?? '',
    level: course.level,
  });

  const { busy, problem, run } = useAction();

  return (
    <form
      className="admin__form"
      onSubmit={(event) => {
        event.preventDefault();
        void run(() => api.put(`/admin/authoring/courses/${course.id}`, form), onSaved);
      }}
    >
      <div className="admin__form-grid">
        <label className="field">
          <span>Título</span>
          <input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
        </label>

        <label className="field">
          <span>Nivel</span>
          <select value={form.level} onChange={(e) => setForm({ ...form, level: e.target.value })}>
            {LEVELS.map((level) => (
              <option key={level.value} value={level.value}>
                {level.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      <label className="field">
        <span>Descripción corta</span>
        <input
          required
          value={form.shortDescription}
          onChange={(e) => setForm({ ...form, shortDescription: e.target.value })}
        />
      </label>

      <label className="field">
        <span>Descripción larga</span>
        <textarea
          rows={3}
          value={form.longDescription}
          onChange={(e) => setForm({ ...form, longDescription: e.target.value })}
        />
      </label>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
        {busy ? 'Guardando…' : 'Guardar ficha'}
      </button>
    </form>
  );
}

type SectionShape = AuthoringSection;

function SectionEditor({
  courseId,
  section,
  onChanged,
}: {
  courseId: string;
  section: SectionShape;
  onChanged: () => void;
}) {
  const { busy, problem, run } = useAction();
  const [title, setTitle] = useState(section.title);
  const [adding, setAdding] = useState(false);

  return (
    <article className="admin__section">
      <div className="admin__section-head">
        <label className="field field--inline">
          <span className="sr-only">Título de la sección</span>
          <input value={title} onChange={(e) => setTitle(e.target.value)} />
        </label>

        <button
          type="button"
          className="btn btn--ghost btn--sm"
          disabled={busy || title === section.title}
          onClick={() =>
            void run(
              () =>
                api.put(`/admin/authoring/courses/${courseId}/sections/${section.id}`, { title }),
              onChanged,
            )
          }
        >
          Renombrar
        </button>

        <button
          type="button"
          className="btn btn--ghost btn--sm btn--icon admin__danger"
          aria-label={`Borrar la sección ${section.title}`}
          disabled={busy}
          onClick={() => {
            // Se lleva por delante todas sus clases, así que se pregunta antes.
            if (!window.confirm(`¿Borrar "${section.title}" y sus ${section.lessons.length} clases?`)) {
              return;
            }

            void run(() => api.del(`/admin/authoring/sections/${section.id}`), onChanged);
          }}
        >
          <IconTrash />
        </button>
      </div>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <ul className="admin__lessons">
        {section.lessons.map((lesson) => (
          <li key={lesson.id}>
            <LessonRow courseId={courseId} lesson={lesson} onChanged={onChanged} />
          </li>
        ))}
      </ul>

      {adding ? (
        <LessonForm
          courseId={courseId}
          sectionId={section.id}
          onDone={() => {
            setAdding(false);
            onChanged();
          }}
        />
      ) : (
        <button
          type="button"
          className="btn btn--ghost btn--sm btn--icon-text"
          onClick={() => setAdding(true)}
        >
          <IconPlus />
          Añadir clase
        </button>
      )}
    </article>
  );
}

type LessonShape = AuthoringLesson;

function LessonRow({
  courseId,
  lesson,
  onChanged,
}: {
  courseId: string;
  lesson: LessonShape;
  onChanged: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const { busy, run } = useAction();

  if (editing) {
    return (
      <LessonForm
        courseId={courseId}
        lesson={lesson}
        onDone={() => {
          setEditing(false);
          onChanged();
        }}
      />
    );
  }

  return (
    <div className="admin__lesson">
      <span className="admin__lesson-title">{lesson.title}</span>

      <span className="admin__lesson-meta muted">
        {lesson.type} · {lesson.durationMinutes} min
        {lesson.isFreePreview ? ' · gratis' : ''}
        {lesson.isRequired ? '' : ' · opcional'}
      </span>

      <button
        type="button"
        className="btn btn--ghost btn--sm btn--icon"
        aria-label={`Editar ${lesson.title}`}
        onClick={() => setEditing(true)}
      >
        <IconPencil />
      </button>

      <button
        type="button"
        className="btn btn--ghost btn--sm btn--icon admin__danger"
        aria-label={`Borrar ${lesson.title}`}
        disabled={busy}
        onClick={() => {
          if (!window.confirm(`¿Borrar la clase "${lesson.title}"?`)) {
            return;
          }

          void run(() => api.del(`/admin/authoring/lessons/${lesson.id}`), onChanged);
        }}
      >
        <IconTrash />
      </button>
    </div>
  );
}

function LessonForm({
  courseId,
  sectionId,
  lesson,
  onDone,
}: {
  courseId: string;
  sectionId?: string;
  lesson?: LessonShape;
  onDone: () => void;
}) {
  const [form, setForm] = useState({
    title: lesson?.title ?? '',
    type: lesson?.type ?? 'slides',
    durationMinutes: String(lesson?.durationMinutes ?? 5),
    contentRef: lesson?.contentRef ?? '',
    isFreePreview: lesson?.isFreePreview ?? false,
    isRequired: lesson?.isRequired ?? true,
  });

  const { busy, problem, run } = useAction();

  return (
    <form
      className="admin__form admin__form--nested"
      onSubmit={(event) => {
        event.preventDefault();

        const body = { ...form, durationMinutes: Number(form.durationMinutes) };

        void run(
          () =>
            lesson
              ? api.put(`/admin/authoring/courses/${courseId}/lessons/${lesson.id}`, body)
              : api.post(`/admin/authoring/courses/${courseId}/sections/${sectionId}/lessons`, body),
          onDone,
        );
      }}
    >
      <div className="admin__form-grid">
        <label className="field">
          <span>Título</span>
          <input
            required
            value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })}
          />
        </label>

        <label className="field">
          <span>Tipo</span>
          <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
            {LESSON_TYPES.map((type) => (
              <option key={type.value} value={type.value}>
                {type.label}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>Duración (min)</span>
          <input
            required
            type="number"
            min="0"
            value={form.durationMinutes}
            onChange={(e) => setForm({ ...form, durationMinutes: e.target.value })}
          />
        </label>
      </div>

      <label className="field">
        <span>Fichero de contenido</span>
        <input
          required
          value={form.contentRef}
          onChange={(e) => setForm({ ...form, contentRef: e.target.value })}
          placeholder="agent-engineering-v3/bloques/B0-fundamentos.html#slide-3"
        />
        <small className="muted">
          Ruta dentro del volumen de contenido. El ancla tras <code>#</code> es opcional.
        </small>
      </label>

      <div className="admin__checks">
        <label className="check">
          <input
            type="checkbox"
            checked={form.isFreePreview}
            onChange={(e) => setForm({ ...form, isFreePreview: e.target.checked })}
          />
          <span>Gratis sin suscripción</span>
        </label>

        <label className="check">
          <input
            type="checkbox"
            checked={form.isRequired}
            onChange={(e) => setForm({ ...form, isRequired: e.target.checked })}
          />
          <span>Obligatoria para el certificado</span>
        </label>
      </div>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}

      <div className="row">
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
          {busy ? 'Guardando…' : lesson ? 'Guardar clase' : 'Añadir clase'}
        </button>

        <button type="button" className="btn btn--ghost btn--sm" onClick={onDone}>
          Cancelar
        </button>
      </div>
    </form>
  );
}

/**
 * Estado compartido de las acciones que escriben: ocupado, y el mensaje del servidor si falla.
 * Se muestra el mensaje tal cual llega porque el dominio ya los escribe para leerse ("La
 * lección necesita un título"), y reescribirlos aquí los haría divergir.
 */
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
