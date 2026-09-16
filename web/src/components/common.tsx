import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import type { CourseCard as CourseCardType } from '../lib/types';
import { CourseCover } from './CourseCover';
import { IconClock, IconLayers, IconLock, IconSignal } from './icons';
import './common.css';

/** Nivel en castellano. Lo que llega de la API es el valor del dominio, no una etiqueta. */
const LEVEL_LABEL: Record<CourseCardType['level'], string> = {
  intro: 'Introductorio',
  intermediate: 'Intermedio',
  advanced: 'Avanzado',
};

export function Spinner({ label = 'Cargando…' }: { label?: string }) {
  return (
    <p className="loading" role="status" aria-live="polite">
      {label}
    </p>
  );
}

export function ErrorMessage({ children }: { children: ReactNode }) {
  return (
    <div className="alert alert--error" role="alert">
      {children}
    </div>
  );
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="empty-state">
      <h3>{title}</h3>
      {children}
    </div>
  );
}

export function ProgressBar({ percent, label }: { percent: number; label: string }) {
  return (
    <div className="progress">
      <div
        className="progress__track"
        role="progressbar"
        aria-valuenow={percent}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={label}
      >
        <div className="progress__fill" style={{ width: `${percent}%` }} />
      </div>
      <span className="progress__value">{percent}%</span>
    </div>
  );
}

/**
 * Tarjeta de curso del catálogo (T-05). Toda la tarjeta es un enlace, pero el título es el
 * texto accesible: un enlace cuyo nombre fuese la tarjeta entera se leería como un párrafo.
 */
export function CourseCard({ course, progressPercent }: { course: CourseCardType; progressPercent?: number }) {
  const comingSoon = course.status === 'comingsoon';

  return (
    <article className={`course-card ${comingSoon ? 'course-card--soon' : ''}`}>
      <div className="course-card__cover" aria-hidden="true">
        {course.coverImageUrl ? (
          <img src={course.coverImageUrl} alt="" loading="lazy" />
        ) : (
          <CourseCover slug={course.slug} title={course.title} />
        )}
      </div>

      <div className="course-card__body">
        <div className="row course-card__badges">
          {course.isNew && <span className="badge badge--new">Nuevo</span>}
          {course.isFeatured && <span className="badge badge--featured">Destacado</span>}
          {comingSoon && <span className="badge badge--soon">Próximamente</span>}
        </div>

        <h3 className="course-card__title">
          <Link to={`/curso/${course.slug}`}>{course.title}</Link>
        </h3>

        <p className="course-card__description">{course.shortDescription}</p>

        {/*
          Nivel, duración y volumen como chips: es lo que se compara de un vistazo entre
          tarjetas, y el nivel estaba en los datos sin llegar a mostrarse nunca.
        */}
        <div className="chip-row course-card__meta">
          <span className="chip chip--brand">
            <IconSignal />
            {LEVEL_LABEL[course.level]}
          </span>
          <span className="chip">
            <IconClock />
            {course.hours} h
          </span>
          <span className="chip">
            <IconLayers />
            {course.lessonCount} clases
          </span>
        </div>

        <div className="course-card__footer">
          {course.rating !== null ? (
            <span className="course-card__rating">
              ★ {course.rating.toFixed(1)}
              <span className="muted"> ({course.ratingCount})</span>
            </span>
          ) : (
            /* Sin valoraciones reales todavía; ver TODO(T-15) en la API. */
            <span className="muted course-card__rating">Sin valoraciones aún</span>
          )}

          {course.membersOnly && !comingSoon && (
            <span className="course-card__members">
              <IconLock size={13} />
              Sólo para miembros
            </span>
          )}
        </div>

        {progressPercent !== undefined && (
          <ProgressBar percent={progressPercent} label={`Progreso en ${course.title}`} />
        )}
      </div>
    </article>
  );
}

