import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { EmptyState, ErrorMessage, ProgressBar, Spinner } from '../components/common';
import {
  IconDownload,
  IconFlask,
  IconHelp,
  IconLayers,
  IconLock,
  IconPlay,
} from '../components/icons';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import type { CourseDetail } from '../lib/types';
import './course-detail.css';

export function CourseDetailPage() {
  const { slug = '' } = useParams();
  const { data: course, error, loading } = useApi<CourseDetail>(`/courses/${slug}`, [slug]);

  useDocumentTitle(course?.title ?? 'Curso');

  if (loading) {
    return <Spinner />;
  }

  if (error?.status === 404) {
    return (
      <div className="container section">
        <EmptyState title="Ese curso no existe">
          <p>
            Puede que haya cambiado de dirección. <Link to="/cursos">Ver el catálogo</Link>.
          </p>
        </EmptyState>
      </div>
    );
  }

  if (error || !course) {
    return (
      <div className="container section">
        <ErrorMessage>No hemos podido cargar el curso.</ErrorMessage>
      </div>
    );
  }

  const comingSoon = course.status === 'comingsoon';

  return (
    <>
      <header className="course-hero">
        <div className="container course-hero__inner">
          <div>
            <div className="row">
              {course.isNew && <span className="badge badge--new">Nuevo</span>}
              {comingSoon && <span className="badge badge--soon">Próximamente</span>}
              <span className="muted course-hero__level">{levelLabel(course.level)}</span>
            </div>

            <h1>{course.title}</h1>
            <p className="course-hero__lead">{course.shortDescription}</p>

            <ul className="course-hero__meta card">
              <li>
                <strong>{course.hours}</strong> horas
              </li>
              <li>
                <strong>{course.lessonCount}</strong> clases
              </li>
              <li>
                <strong>{course.sectionCount}</strong> secciones
              </li>
            </ul>
          </div>

          <aside className="course-hero__cta card">
            {comingSoon ? (
              <WaitlistForm courseSlug={course.slug} />
            ) : (
              <EnrolmentCta course={course} />
            )}
          </aside>
        </div>
      </header>

      <div className="container section course-body">
        <main>
          <section>
            <h2>Sobre este curso</h2>
            {course.longDescription
              .split('\n')
              .filter(Boolean)
              .map((paragraph) => (
                <p key={paragraph.slice(0, 40)}>{paragraph}</p>
              ))}
          </section>

          <section>
            <h2>Temario</h2>
            <p className="muted">
              El temario es público. El contenido se abre con una suscripción activa, salvo las
              clases marcadas como muestra gratuita.
            </p>

            <div className="syllabus">
              {course.sections.map((section, index) => (
                <details key={section.title} open={index === 0} className="syllabus__section">
                  <summary>
                    <span className="syllabus__title">{section.title}</span>
                    <span className="muted syllabus__count">
                      {section.lessons.length} clases ·{' '}
                      {section.lessons.reduce((total, lesson) => total + lesson.durationMinutes, 0)} min
                    </span>
                  </summary>

                  <ul className="syllabus__lessons">
                    {section.lessons.map((lesson) => (
                      <li key={lesson.slug}>
                        <span className="syllabus__lesson-type" aria-hidden="true">
                          <TypeIcon type={lesson.type} />
                        </span>

                        {lesson.hasAccess ? (
                          <Link to={`/aprender/${course.slug}/${lesson.slug}`}>{lesson.title}</Link>
                        ) : (
                          <span>{lesson.title}</span>
                        )}

                        {lesson.isFreePreview && <span className="badge badge--featured">Muestra</span>}
                        {!lesson.hasAccess && (
                          <span className="muted syllabus__locked">
                            <IconLock size={14} aria-hidden="true" />
                            <span className="sr-only">Solo para miembros</span>
                          </span>
                        )}

                        <span className="muted syllabus__duration">{lesson.durationMinutes} min</span>
                      </li>
                    ))}
                  </ul>
                </details>
              ))}
            </div>
          </section>

          {course.admissionQuizSlug && (
            <section className="admission-callout">
              <h2>¿Es este curso para ti?</h2>
              <p>
                Este curso da por sabidas bastantes cosas. El test de nivel son 12 preguntas y unos
                siete minutos, y te dice honestamente si es tu momento o si te conviene empezar
                por Fundamentos de IA Engineer.
              </p>
              <Link to={`/nivel/${course.admissionQuizSlug}`} className="btn btn--ghost">
                Comprobar mi nivel
              </Link>
            </section>
          )}
        </main>

        <aside className="course-side">
          <div className="card course-side__box">
            <h2>Instructor</h2>
            <p className="muted">
              Txema González. Ingeniería de software y sistemas con LLM en producción, con foco en
              sectores regulados.
            </p>
            <Link to="/sobre-mi">Ver biografía</Link>
          </div>

          <div className="card course-side__box">
            <h2>Al terminar</h2>
            <p className="muted">
              Obtienes un certificado con código público verificable por cualquiera, sin necesidad
              de cuenta.
            </p>
            <Link to="/check-certificate">Cómo se verifica</Link>
          </div>
        </aside>
      </div>
    </>
  );
}

/**
 * Emitir el certificado desde la propia ficha del curso.
 *
 * Va aquí porque es donde el alumno acaba de leer «has completado el curso»: mandarlo a otra
 * pantalla a buscar el botón es pedirle que adivine dónde está. El mismo botón existe en la
 * cuenta, para cuando vuelve días después.
 *
 * Al emitirlo se recarga la ficha en vez de guardar el código en estado local: el certificado
 * cambia también el bloque lateral y el resto de la página, y con dos fuentes de verdad una de
 * las dos acabaría enseñando algo distinto.
 */
function IssueCertificate({ courseSlug }: { courseSlug: string }) {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  async function issue() {
    setBusy(true);
    setProblem(null);

    try {
      await api.post(`/me/certificates/courses/${courseSlug}`);
      window.location.reload();
    } catch (caught) {
      setProblem(
        caught instanceof ApiError ? caught.message : 'No hemos podido emitir el certificado.',
      );
      setBusy(false);
    }
  }

  return (
    <>
      <button type="button" className="btn btn--accent" disabled={busy} onClick={() => void issue()}>
        {busy ? 'Emitiendo…' : 'Emitir mi certificado'}
      </button>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}
    </>
  );
}

function EnrolmentCta({ course }: { course: CourseDetail }) {
  const { user } = useAuth();
  const progress = course.progress;

  if (progress && progress.percentComplete > 0) {
    return (
      <>
        <h2 className="course-hero__cta-title">Continúa donde lo dejaste</h2>
        <ProgressBar percent={progress.percentComplete} label={`Progreso en ${course.title}`} />
        <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
          {progress.completedLessons} de {progress.totalRequiredLessons} clases obligatorias.
        </p>

        {progress.nextLessonSlug ? (
          <Link to={`/aprender/${course.slug}/${progress.nextLessonSlug}`} className="btn btn--accent">
            Seguir con «{progress.nextLessonTitle}»
          </Link>
        ) : progress.isComplete ? (
          <p>Has completado el curso.</p>
        ) : (
          /*
            Sin siguiente clase pero sin terminar el curso solo puede significar una cosa: se
            han acabado las clases de muestra. Decir "has completado el curso" aquí sería falso
            y además dejaría al alumno sin saber cómo seguir.
          */
          <>
            <p>Has visto todas las clases de muestra de este curso.</p>
            <Link to="/precios" className="btn btn--accent">
              Ver planes para seguir
            </Link>
          </>
        )}

        {progress.certificateCode ? (
          <div className="row">
            <a
              className="btn btn--accent"
              href={`/api/certificates/${progress.certificateCode}/pdf`}
            >
              Descargar certificado
            </a>
            <Link to={`/check-certificate/${progress.certificateCode}`} className="btn btn--ghost">
              Ver mi certificado
            </Link>
          </div>
        ) : (
          progress.canIssueCertificate && <IssueCertificate courseSlug={course.slug} />
        )}
      </>
    );
  }

  const firstPreview = course.sections
    .flatMap((section) => section.lessons)
    .find((lesson) => lesson.isFreePreview);

  return (
    <>
      <h2 className="course-hero__cta-title">Acceso</h2>
      <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
        Este curso está incluido en cualquier plan de suscripción.
      </p>

      <Link to="/precios" className="btn btn--accent">
        Ver planes
      </Link>

      {firstPreview && (
        <Link to={`/aprender/${course.slug}/${firstPreview.slug}`} className="btn btn--ghost">
          Ver una clase gratis
        </Link>
      )}

      {!user && (
        <p className="muted" style={{ fontSize: 'var(--text-xs)', margin: 0 }}>
          ¿Ya tienes cuenta? <Link to="/login">Entra</Link>.
        </p>
      )}
    </>
  );
}

function WaitlistForm({ courseSlug }: { courseSlug: string }) {
  const [email, setEmail] = useState('');
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>('idle');
  const [message, setMessage] = useState('');

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setState('sending');

    try {
      await api.post(`/courses/${courseSlug}/waitlist`, { email });
      setState('done');
    } catch (caught) {
      setState('error');
      setMessage(caught instanceof ApiError ? caught.message : 'No hemos podido apuntarte.');
    }
  }

  if (state === 'done') {
    return (
      <div className="alert alert--success" role="status">
        Apuntado. Te avisamos en cuanto salga, y solo para eso.
      </div>
    );
  }

  return (
    <form onSubmit={submit}>
      <h2 className="course-hero__cta-title">Avísame cuando salga</h2>
      <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
        Un único correo cuando el curso se publique. Nada más.
      </p>

      {state === 'error' && <ErrorMessage>{message}</ErrorMessage>}

      <div className="field">
        <label htmlFor="waitlist-email">Tu email</label>
        <input
          id="waitlist-email"
          type="email"
          required
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />
      </div>

      <button type="submit" className="btn btn--primary" disabled={state === 'sending'}>
        {state === 'sending' ? 'Apuntando…' : 'Avisadme'}
      </button>
    </form>
  );
}

function levelLabel(level: string): string {
  switch (level) {
    case 'intro':
      return 'Nivel introducción';
    case 'intermediate':
      return 'Nivel intermedio';
    default:
      return 'Nivel avanzado';
  }
}

/**
 * Icono por tipo de clase. Emoji no: cada sistema lo dibuja a su manera y en un temario de
 * 321 filas la columna dejaba de alinearse. Estos comparten trazo y caja con el resto.
 */
function TypeIcon({ type }: { type: string }) {
  switch (type) {
    case 'lab':
      return <IconFlask size={16} />;
    case 'quiz':
      return <IconHelp size={16} />;
    case 'video':
      return <IconPlay size={16} />;
    case 'download':
      return <IconDownload size={16} />;
    default:
      return <IconLayers size={16} />;
  }
}
