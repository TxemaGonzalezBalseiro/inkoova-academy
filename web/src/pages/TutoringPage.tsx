import { Link } from 'react-router-dom';
import { EmptyState, ErrorMessage, ProgressBar, Spinner } from '../components/common';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import './tutoring.css';

/**
 * Las tutorías del alumno: qué compró, cuántas ha consumido y cuántas le quedan.
 *
 * El titular cuenta solo el tiempo que de verdad se puede pedir. Una bolsa caducada con horas
 * sin gastar sigue en la lista —el alumno tiene derecho a ver que las perdió— pero no suma,
 * porque un saldo que incluye tiempo inutilizable es una promesa que no se puede cumplir.
 */
type TutoringSession = {
  id: string;
  minutes: number;
  occurredAt: string;
  topic: string;
  notes: string;
  tutorId: string | null;
  /** Quién la dio. Nulo en las que dio la casa y en las apuntadas antes de haber profesores. */
  tutorName: string | null;
};

type TutoringGrant = {
  id: string;
  packageName: string;
  minutesTotal: number;
  minutesUsed: number;
  minutesRemaining: number;
  source: string;
  grantedAt: string;
  /** 'fixed', 'subscription' o 'never'. */
  expiryMode: string;
  expiresAt: string | null;
  isExpired: boolean;
  isRevoked: boolean;
  revokedReason: string | null;
  note: string;
  sessions: TutoringSession[];
  appointments: TutoringAppointment[];
};

/**
 * Una tutoría convocada y todavía sin dar.
 *
 * No ha descontado saldo: eso pasa cuando se da. Aparece aquí para que el alumno vea lo que
 * tiene citado sin tener que buscarlo en su correo.
 */
type TutoringAppointment = {
  id: string;
  tutorName: string | null;
  startsAt: string;
  minutes: number;
  topic: string;
  location: string;
  /** 'scheduled', 'done' o 'cancelled'. */
  status: string;
};

type TutoringPackage = {
  id: string;
  slug: string;
  name: string;
  description: string;
  minutes: number;
  priceCents: number;
};

type MyTutoring = {
  minutesRemaining: number;
  minutesTotal: number;
  minutesUsed: number;
  nextExpiry: string | null;
  grants: TutoringGrant[];
  offer: TutoringPackage[];
  /** Si su plan le da acceso ahora. Es lo que explica un saldo parado. */
  hasPlanAccess: boolean;
  planValidUntil: string | null;
};

const formatMinutes = (minutes: number): string => {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  if (hours === 0) return `${rest} min`;
  if (rest === 0) return `${hours} h`;
  return `${hours} h ${rest} min`;
};

const euros = (cents: number) =>
  (cents / 100).toLocaleString('es-ES', { style: 'currency', currency: 'EUR' });

const longDate = (iso: string) =>
  new Date(iso).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' });

/** Con la hora, para una cita: sin ella, «14 de septiembre» no le dice a nadie cuándo conectarse. */
const longMoment = (iso: string) =>
  new Date(iso).toLocaleString('es-ES', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    hour: '2-digit',
    minute: '2-digit',
  });

export function TutoringPage() {
  useDocumentTitle('Mis tutorías');
  const tutoring = useApi<MyTutoring>('/me/tutoring', []);

  if (tutoring.loading) {
    return <Spinner label="Cargando tus tutorías…" />;
  }

  if (tutoring.error) {
    return (
      <div className="container section">
        <ErrorMessage>{tutoring.error.message}</ErrorMessage>
      </div>
    );
  }

  const data = tutoring.data;
  if (!data) {
    return null;
  }

  return (
    <div className="container section tutoring">
      <header className="section-header">
        <h1>Mis tutorías</h1>
        <p className="muted">
          El tiempo de tutoría que tienes contratado, en qué se ha ido y qué te queda.
        </p>
      </header>

      {data.grants.length === 0 ? (
        <EmptyState title="Todavía no tienes tutorías">
          <p className="muted">
            Las tutorías son sesiones uno a uno sobre lo que estés construyendo. Se compran por
            horas y se gastan cuando las pides.
          </p>
          {data.offer.length > 0 && <Offer packages={data.offer} />}
        </EmptyState>
      ) : (
        <>
          <section className="tutoring__summary">
            <div className="tutoring__figure">
              <span className="tutoring__figure-value">{formatMinutes(data.minutesRemaining)}</span>
              <span className="muted">disponibles</span>
            </div>

            <div className="tutoring__figure">
              <span className="tutoring__figure-value">{formatMinutes(data.minutesUsed)}</span>
              <span className="muted">consumidas</span>
            </div>

            <div className="tutoring__figure">
              <span className="tutoring__figure-value">{formatMinutes(data.minutesTotal)}</span>
              <span className="muted">contratadas</span>
            </div>
          </section>

          {!data.hasPlanAccess && data.minutesTotal > 0 && (
            <p className="alert alert--info">
              Tus horas van con la suscripción y ahora mismo no está activa, así que no se pueden
              pedir. Al renovar vuelven a estar disponibles: no se pierde lo que no has gastado
              mientras la recuperes.
            </p>
          )}

          {data.hasPlanAccess && data.nextExpiry && (
            <p className="alert alert--info">
              Tienes horas hasta el {longDate(data.nextExpiry)}, que es hasta donde llega tu
              suscripción. Se amplía sola cada vez que renuevas.
            </p>
          )}

          <section>
            <h2>Lo que has comprado</h2>

            {data.grants.map((grant) => (
              <GrantCard key={grant.id} grant={grant} />
            ))}
          </section>

          {data.offer.length > 0 && (
            <section>
              <h2>Ampliar</h2>
              <Offer packages={data.offer} />
            </section>
          )}
        </>
      )}
    </div>
  );
}

function GrantCard({ grant }: { grant: TutoringGrant }) {
  // «Caducada» sería mentira en una bolsa atada a la suscripción: el tiempo sigue ahí y vuelve
  // en cuanto se renueva. Lo que pasa es que ahora no se puede pedir, y eso se dice.
  const state = grant.isRevoked
    ? 'Revocada'
    : grant.isExpired
      ? grant.expiryMode === 'subscription'
        ? 'En pausa'
        : 'Caducada'
      : grant.minutesRemaining === 0
        ? 'Agotada'
        : 'Disponible';

  // Barra de consumo. El aria-label lleva la misma cifra que el texto: quien navega con
  // lector de pantalla no debe depender de leer el ancho de un div.
  const usedPercent = Math.round((grant.minutesUsed / grant.minutesTotal) * 100);

  // Solo lo que está por venir: las anuladas y las que ya se dieron no son una cita pendiente,
  // y las dadas ya salen en el histórico de abajo.
  const upcoming = grant.appointments.filter((a) => a.status === 'scheduled');

  return (
    <article className="card tutoring__grant">
      <header className="tutoring__grant-head">
        <div>
          <h3>{grant.packageName}</h3>
          <p className="muted">
            Contratada el {longDate(grant.grantedAt)}
            {expiryNote(grant)}
          </p>
        </div>

        <span className="badge">{state}</span>
      </header>

      <p className="tutoring__balance">
        <strong>{formatMinutes(grant.minutesRemaining)}</strong> de{' '}
        {formatMinutes(grant.minutesTotal)} · has usado {formatMinutes(grant.minutesUsed)}
      </p>

      <ProgressBar
        percent={usedPercent}
        label={`${formatMinutes(grant.minutesUsed)} consumidas de ${formatMinutes(grant.minutesTotal)}`}
      />

      {grant.revokedReason && <p className="muted">Motivo de la revocación: {grant.revokedReason}</p>}

      {upcoming.length > 0 && (
        <div className="tutoring__upcoming">
          <h4>Convocadas</h4>
          <ul className="tutoring__history-list">
            {upcoming.map((appointment) => (
              <li key={appointment.id} className="tutoring__history-item">
                <div>
                  <strong>{longMoment(appointment.startsAt)}</strong> ·{' '}
                  {formatMinutes(appointment.minutes)}
                  {appointment.tutorName && <> · con {appointment.tutorName}</>}
                  <br />
                  {appointment.topic}
                  {appointment.location && (
                    <p className="muted">{appointment.location}</p>
                  )}
                </div>
              </li>
            ))}
          </ul>
          <p className="muted">
            Estas horas siguen contando como disponibles: se descuentan cuando la tutoría se da.
          </p>
        </div>
      )}

      {grant.sessions.length > 0 && (
        <details className="tutoring__history">
          <summary>
            {grant.sessions.length === 1
              ? '1 tutoría realizada'
              : `${grant.sessions.length} tutorías realizadas`}
          </summary>

          <ul className="tutoring__history-list">
            {grant.sessions.map((session) => (
              <li key={session.id} className="tutoring__history-item">
                <div>
                  <strong>{longDate(session.occurredAt)}</strong> · {formatMinutes(session.minutes)}
                  {session.tutorName && <> · con {session.tutorName}</>}
                  <br />
                  {session.topic}
                  {session.notes && <p className="muted">{session.notes}</p>}
                </div>
              </li>
            ))}
          </ul>
        </details>
      )}
    </article>
  );
}

function Offer({ packages }: { packages: TutoringPackage[] }) {
  return (
    <div className="grid grid--cards">
      {packages.map((pack) => (
        <article key={pack.id} className="card tutoring__pack">
          <h3 className="tutoring__pack-title">{pack.name}</h3>
          <p className="tutoring__pack-hours">{formatMinutes(pack.minutes)}</p>

          {/* La descripción empuja: es lo que hace que el precio y el botón queden a la misma
              altura en las cuatro tarjetas aunque los textos midan distinto. */}
          <p className="tutoring__pack-text">{pack.description}</p>

          <p className="tutoring__price">{euros(pack.priceCents)}</p>

          {/*
            No hay botón de compra: las tutorías se contratan hablando, porque hay que cuadrar
            agenda antes de cobrar. Un botón que no cobra sería peor que no tenerlo.
          */}
          <Link to="/soporte" className="btn btn--accent">
            Solicitar
          </Link>
        </article>
      ))}
    </div>
  );
}

/**
 * La coletilla de caducidad de una bolsa.
 *
 * En las horas atadas a la suscripción no se dice «caduca el», que suena a que se pierden ese
 * día pase lo que pase: se dice de dónde sale la fecha, porque se mueve al renovar.
 */
function expiryNote(grant: TutoringGrant): string {
  if (grant.expiryMode === 'never') return '';

  if (grant.expiryMode === 'subscription') {
    return grant.expiresAt
      ? ` · disponibles mientras siga tu suscripción, ahora hasta el ${longDate(grant.expiresAt)}`
      : ' · disponibles mientras siga tu suscripción';
  }

  return grant.expiresAt ? ` · caduca el ${longDate(grant.expiresAt)}` : '';
}
