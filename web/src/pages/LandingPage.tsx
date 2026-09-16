import { Link } from 'react-router-dom';
import { CourseCard, ErrorMessage, Spinner } from '../components/common';
import { PricingTable } from './PricingPage';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import type { CourseCard as CourseCardType } from '../lib/types';
import './landing.css';

export function LandingPage() {
  useDocumentTitle('Ingeniería de agentes y prompts para builders senior');

  const { data: courses, error, loading } = useApi<CourseCardType[]>('/courses');

  const published = courses?.filter((c) => c.status === 'published') ?? [];
  const upcoming = courses?.filter((c) => c.status === 'comingsoon') ?? [];
  const featured = published.find((c) => c.isNew) ?? published.find((c) => c.isFeatured);

  return (
    <>
      <section className="hero">
        <div className="container hero__inner">
          <p className="hero__eyebrow">Inkoova Academy</p>

          <h1>
            Ingeniería de agentes y prompts para <span className="hero__accent">builders senior</span> en
            entornos regulados
          </h1>

          <p className="hero__lead">
            No es prompt engineering 101 ni un tour por las novedades del mes. Es la disciplina de
            llevar sistemas con LLMs a producción: contexto, tools, evals, observabilidad,
            seguridad y la normativa europea que ya te aplica.
          </p>

          <div className="row hero__actions">
            <Link to="/precios" className="btn btn--accent">
              Ver planes
            </Link>
            <Link to="/cursos" className="btn btn--ghost">
              Explorar cursos
            </Link>
          </div>

          {/*
            Prueba social sin cifras inventadas. El plan (T-05) pide un TODO explícito hasta
            que la beta cerrada dé datos reales; poner "+2.000 alumnos" hoy sería mentir.
          */}
          <ul className="hero__proof">
            <li>
              <strong>{published.length || '—'}</strong>
              <span>cursos publicados</span>
            </li>
            <li>
              <strong>{published.reduce((total, course) => total + course.hours, 0) || '—'} h</strong>
              <span>de material</span>
            </li>
            <li>
              <strong>Agnóstico</strong>
              <span>de framework</span>
            </li>
            <li>
              <strong>Español</strong>
              <span>técnico, sin traducciones</span>
            </li>
          </ul>
        </div>
      </section>

      {featured && (
        <section className="section container">
          <div className="feature-banner">
            <div>
              <span className="badge badge--new">Nuevo curso</span>
              <h2 className="feature-banner__title">{featured.title}</h2>
              <p className="muted">{featured.shortDescription}</p>
              <p className="feature-banner__meta muted">
                {featured.hours} h · {featured.lessonCount} clases · {featured.sectionCount} secciones
              </p>
            </div>
            <Link to={`/curso/${featured.slug}`} className="btn btn--primary">
              Ver el temario
            </Link>
          </div>
        </section>
      )}

      <section className="section container" id="cursos">
        <header className="section-header">
          <h2>Nuestros cursos</h2>
          <p className="muted">
            Un programa de cuatro cursos que se puede recorrer entero o por partes.
          </p>
        </header>

        {loading && <Spinner />}
        {error && <ErrorMessage>No hemos podido cargar el catálogo. Recarga la página.</ErrorMessage>}

        <div className="grid grid--cards">
          {published.map((course) => (
            <CourseCard key={course.slug} course={course} />
          ))}
        </div>
      </section>

      {upcoming.length > 0 && (
        <section className="section section--muted">
          <div className="container">
            <header className="section-header">
              <h2>Próximos cursos</h2>
              <p className="muted">Están en producción. Puedes pedir aviso cuando salgan.</p>
            </header>

            <div className="grid grid--cards">
              {upcoming.map((course) => (
                <CourseCard key={course.slug} course={course} />
              ))}
            </div>
          </div>
        </section>
      )}

      <section className="section container">
        <header className="section-header">
          <h2>¿En qué te podemos ayudar?</h2>
        </header>

        <div className="grid help-grid">
          <Link to="/roadmap" className="help-card">
            <h3>Roadmap de aprendizaje</h3>
            <p className="muted">Por dónde empezar y en qué orden, según lo que ya sabes.</p>
          </Link>

          <Link to="/check-certificate" className="help-card">
            <h3>Verifica un certificado</h3>
            <p className="muted">Comprueba en segundos si un certificado es auténtico.</p>
          </Link>

          <Link to="/sobre-mi" className="help-card">
            <h3>Conoce al instructor</h3>
            <p className="muted">Quién está detrás y por qué enseña esto y no otra cosa.</p>
          </Link>

          <Link to="/soporte" className="help-card">
            <h3>Soporte</h3>
            <p className="muted">Dudas de acceso, facturación o contenido.</p>
          </Link>
        </div>
      </section>

      <section className="section container" id="precios">
        <header className="section-header">
          <h2>Planes</h2>
          <p className="muted">
            Suscripción para acceder a todo, o compra suelta si solo te interesa una pieza.
          </p>
        </header>

        <PricingTable />
      </section>
    </>
  );
}
