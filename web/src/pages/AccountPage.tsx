import { useState } from 'react';
import { Link } from 'react-router-dom';
import { CourseCard, EmptyState, ErrorMessage, Spinner } from '../components/common';
import { formatDate } from '../lib/format';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import type { CourseCard as CourseCardType } from '../lib/types';
import './account.css';

type MyCourse = {
  course: CourseCardType;
  courseId: string;
  percentComplete: number;
  /** Falso cuando el alumno solo ha entrado por las clases de muestra, sin comprarlo. */
  hasFullAccess: boolean;
  /** El certificado ya emitido de este curso, si lo hay. */
  certificateCode: string | null;
  /** Cierto cuando está al 100 %, es suyo del todo y todavía no tiene certificado. */
  canIssueCertificate: boolean;
};

/** Un pack sectorial al que el alumno tiene acceso. No tiene progreso: se descarga. */
type MyPack = {
  slug: string;
  title: string;
  sector: string;
  version: string;
  /** Cuándo se verificaron sus fuentes normativas. Es lo que dice si sigue vigente. */
  regulatoryCheckDate: string | null;
  files: number;
  totalSizeInBytes: number;
};

type MyCertificate = {
  code: string;
  subject: string;
  issuedOn: string;
  isValid: boolean;
  shareUrl: string;
};

type QuizAttempt = {
  quizSlug: string;
  score: number;
  total: number;
  verdict: string;
  takenAt: string;
};

export function AccountPage() {
  useDocumentTitle('Mi cuenta');

  const { user } = useAuth();
  const {
    data: courses,
    loading: coursesLoading,
    reload: reloadCourses,
  } = useApi<MyCourse[]>('/me/courses');
  const { data: certificates, reload: reloadCertificates } = useApi<MyCertificate[]>('/me/certificates');

  // Emitir el certificado cambia las dos listas: el curso deja de poder emitirlo y aparece
  // abajo. Recargar solo una dejaría la pantalla contándose dos historias distintas.
  const afterIssue = () => {
    reloadCourses();
    reloadCertificates();
  };
  const { data: attempts } = useApi<QuizAttempt[]>('/me/quiz-attempts');

  return (
    <div className="container section account">
      {/*
        Identidad primero: quién soy y con qué cuenta estoy dentro. Con varias cuentas (la
        personal y la de administración) el email era lo único que las distinguía, y estaba
        en gris pequeño debajo del saludo.
      */}
      <header className="account__identity">
        <span className="account__avatar" aria-hidden="true">
          {(user?.displayName ?? '?').trim().charAt(0).toUpperCase()}
        </span>

        <div>
          <h1>Hola, {user?.displayName}</h1>
          <p className="account__email">{user?.email}</p>
        </div>
      </header>

      {!user?.emailConfirmed && (
        <div className="alert alert--info">
          Tu email todavía no está confirmado. Revisa tu bandeja: algunas funciones lo requieren.
        </div>
      )}

      <nav className="account__nav" aria-label="Secciones de la cuenta">
        <Link to="/cuenta/suscripcion" className="btn btn--ghost btn--sm">
          Suscripción
        </Link>
        <Link to="/cuenta/facturas" className="btn btn--ghost btn--sm">
          Facturas
        </Link>
        <Link to="/cuenta/tutorias" className="btn btn--ghost btn--sm">
          Tutorías
        </Link>
        <Link to="/cuenta/datos" className="btn btn--ghost btn--sm">
          Mis datos
        </Link>
        {user?.isAffiliate && (
          <Link to="/afiliado" className="btn btn--ghost btn--sm">
            Panel de afiliado
          </Link>
        )}
      </nav>

      <section>
        <h2>Mis cursos</h2>

        {coursesLoading && <Spinner />}

        {!coursesLoading && (courses?.length ?? 0) === 0 && (
          <EmptyState title="Todavía no has empezado ningún curso">
            <p className="muted">
              El primer bloque de Fundamentos de IA Engineer se abre sin plan. Para el resto,
              elige uno.
            </p>
            <div className="row">
              <Link to="/cursos" className="btn btn--ghost">
                Ver cursos
              </Link>
              <Link to="/precios" className="btn btn--accent">
                Ver planes
              </Link>
            </div>
          </EmptyState>
        )}

        <div className="grid grid--cards">
          {courses?.map((entry) => (
            <div key={entry.course.slug} className="account__course">
              <CourseCard course={entry.course} progressPercent={entry.percentComplete} />

              {/*
                Un curso empezado por las clases de muestra sale aquí igual que uno comprado,
                porque el alumno lo está estudiando. Sin esta etiqueta, la pantalla daría a
                entender que lo tiene entero.
              */}
              {!entry.hasFullAccess && (
                <p className="account__course-note muted">
                  Estás viendo las clases de muestra.{' '}
                  <Link to="/precios">Consigue el curso completo</Link>.
                </p>
              )}

              <CourseCertificate entry={entry} onIssued={afterIssue} />
            </div>
          ))}
        </div>
      </section>

      <MyPacks />

      <section>
        <h2>Mis certificados</h2>

        {(certificates?.length ?? 0) === 0 ? (
          <p className="muted">
            Aún no tienes ninguno. Se emiten al completar el 100 % de las clases obligatorias de un
            curso.
          </p>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Formación</th>
                  <th>Código</th>
                  <th>Emitido</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {certificates?.map((certificate) => (
                  <tr key={certificate.code}>
                    <td>{certificate.subject}</td>
                    <td>
                      <code>{certificate.code}</code>
                    </td>
                    <td>{formatDate(certificate.issuedOn)}</td>
                    <td className="row">
                      <a
                        className="btn btn--ghost btn--sm"
                        href={`/api/certificates/${certificate.code}/pdf`}
                      >
                        PDF
                      </a>
                      {/*
                        LinkedIn y el resto de redes no muestran un PDF: hace falta una imagen.
                        Es el mismo certificado rasterizado, no un diseño aparte.
                      */}
                      <a
                        className="btn btn--ghost btn--sm"
                        href={`/api/certificates/${certificate.code}/imagen`}
                        download={`${certificate.code}.png`}
                      >
                        Imagen
                      </a>
                      <a
                        className="btn btn--ghost btn--sm"
                        href={linkedInShareUrl(certificate)}
                        target="_blank"
                        rel="noreferrer noopener"
                      >
                        LinkedIn
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section>
        <h2>Tests de nivel</h2>

        {(attempts?.length ?? 0) === 0 ? (
          <p className="muted">
            No has hecho ninguno todavía. Es la forma más rápida de saber por dónde empezar.
          </p>
        ) : (
          <ul className="account__attempts">
            {attempts?.map((attempt) => (
              <li key={`${attempt.quizSlug}-${attempt.takenAt}`}>
                <span>{formatDate(attempt.takenAt)}</span>
                <strong>
                  {attempt.score}/{attempt.total}
                </strong>
                <span className="muted">{verdictLabel(attempt.verdict)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

/**
 * URL de "Add to profile" de LinkedIn con los campos prellenados (T-10). El certificado no
 * caduca, así que no se envían campos de expiración.
 */
/**
 * El certificado de un curso, en la propia tarjeta del curso.
 *
 * Tres estados y ninguno más:
 *
 * - **Emitido** → enlaces para descargarlo. No hace falta bajar hasta la tabla de abajo.
 * - **Al 100 % y sin emitir** → botón para emitirlo. Antes esto solo ocurría solo, y si el
 *   proceso automático fallaba el alumno se quedaba sin certificado y sin forma de pedirlo.
 * - **Sin terminar** → nada. Un botón deshabilitado con un «te falta» sería ruido en cada
 *   tarjeta de cada curso empezado.
 */
function CourseCertificate({ entry, onIssued }: { entry: MyCourse; onIssued: () => void }) {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  if (entry.certificateCode) {
    return (
      <p className="account__course-note row">
        <a className="btn btn--ghost btn--sm" href={`/api/certificates/${entry.certificateCode}/pdf`}>
          Descargar certificado
        </a>
      </p>
    );
  }

  if (!entry.canIssueCertificate) {
    return null;
  }

  async function issue() {
    setBusy(true);
    setProblem(null);

    try {
      await api.post(`/me/certificates/courses/${entry.course.slug}`);
      onIssued();
    } catch (caught) {
      setProblem(
        caught instanceof ApiError ? caught.message : 'No hemos podido emitir el certificado.',
      );
      setBusy(false);
    }
  }

  return (
    <div className="account__course-note">
      <p className="muted">Has completado el curso. Ya puedes emitir tu certificado.</p>

      <button type="button" className="btn btn--accent btn--sm" disabled={busy} onClick={() => void issue()}>
        {busy ? 'Emitiendo…' : 'Emitir certificado'}
      </button>

      {problem && <ErrorMessage>{problem}</ErrorMessage>}
    </div>
  );
}

function linkedInShareUrl(certificate: MyCertificate): string {
  const issued = new Date(certificate.issuedOn);

  const params = new URLSearchParams({
    startTask: 'CERTIFICATION_NAME',
    name: certificate.subject,
    organizationName: 'Inkoova Academy',
    issueYear: String(issued.getFullYear()),
    issueMonth: String(issued.getMonth() + 1),
    certUrl: certificate.shareUrl,
    certId: certificate.code,
  });

  return `https://www.linkedin.com/profile/add?${params.toString()}`;
}

function verdictLabel(verdict: string): string {
  switch (verdict) {
    case 'ready':
      return 'Apto';
    case 'almost':
      return 'Casi';
    default:
      return 'Ven más tarde';
  }
}

/**
 * Los packs sectoriales a los que el alumno tiene acceso.
 *
 * En su propia sección y no mezclados con los cursos: un pack no se estudia ni tiene progreso,
 * se descarga y se aplica a un expediente. Mezclarlos obligaría a que cada tarjeta explicara
 * cuál de las dos cosas es.
 *
 * Si no tiene ninguno, la sección NO se pinta. Enseñar un hueco vacío en la cuenta de quien
 * tiene un plan que no los incluye es recordarle en cada visita lo que no ha comprado.
 */
function MyPacks() {
  const { data, loading } = useApi<MyPack[]>('/me/packs');

  if (loading || (data?.length ?? 0) === 0) {
    return null;
  }

  return (
    <section>
      <h2>Contenido adicional</h2>

      <p className="muted">
        Packs sectoriales incluidos en tu plan. Cada uno lleva su fecha de verificación
        normativa: es lo que dice si sigue vigente.
      </p>

      <div className="grid grid--cards">
        {data?.map((pack) => (
          <article key={pack.slug} className="card account__pack">
            <h3 className="account__pack-title">{pack.title}</h3>
            <p className="account__pack-sector">{pack.sector}</p>

            {/* Empuja para que el botón quede a la misma altura en todas las tarjetas. */}
            <dl className="account__pack-facts">
              <dt>Documentos</dt>
              <dd>
                {pack.files} · {Math.round(pack.totalSizeInBytes / 1024)} KB
              </dd>

              <dt>Versión</dt>
              <dd>{pack.version}</dd>

              {pack.regulatoryCheckDate && (
                <>
                  <dt>Verificado</dt>
                  <dd>{formatDate(pack.regulatoryCheckDate)}</dd>
                </>
              )}
            </dl>

            <Link to={`/pack/${pack.slug}`} className="btn btn--ghost">
              Descargar
            </Link>
          </article>
        ))}
      </div>
    </section>
  );
}

export function ErrorBoundaryFallback() {
  return <ErrorMessage>Algo ha fallado al cargar tu cuenta.</ErrorMessage>;
}
