import { ErrorMessage, Spinner } from '../../components/common';
import { formatMoney } from '../../lib/format';
import { useApi } from '../../hooks/useApi';

type Metrics = {
  courses: {
    slug: string;
    title: string;
    lessons: number;
    dropOffPoints: { lessonId: string; lessonTitle: string | null; count: number }[];
  }[];
  mrr: number;
  activeSubscriptions: number;
  pastDueSubscriptions: number;
  churn: number | null;
};

export function AdminMetrics() {
  const { data, error, loading } = useApi<Metrics>('/admin/metrics');

  if (loading) {
    return <Spinner />;
  }

  if (error || !data) {
    return <ErrorMessage>No hemos podido cargar las métricas.</ErrorMessage>;
  }

  return (
    <section>
      <h2>Métricas</h2>

      <dl className="subscription__summary">
        <div>
          <dt>MRR</dt>
          <dd>{formatMoney(data.mrr, 'EUR')}</dd>
        </div>
        <div>
          <dt>Suscripciones activas</dt>
          <dd>{data.activeSubscriptions}</dd>
        </div>
        <div>
          <dt>Con pago pendiente</dt>
          <dd>{data.pastDueSubscriptions}</dd>
        </div>
        <div>
          <dt>Churn</dt>
          <dd>
            {data.churn === null ? (
              <span className="muted" style={{ fontSize: 'var(--text-sm)' }}>
                Sin datos aún
              </span>
            ) : (
              `${data.churn} %`
            )}
          </dd>
        </div>
      </dl>

      <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
        El MRR normaliza cada plan a su valor mensual. Los pagos únicos y el acceso vitalicio no
        cuentan como ingreso recurrente. El churn necesita un mes de histórico: aparecerá en cuanto
        lo haya.
      </p>

      <h3>Dónde se abandona cada curso</h3>
      <p className="muted">
        Lecciones abiertas y nunca completadas. Es la señal más directa de dónde se atasca la
        gente.
      </p>

      {data.courses.map((course) => (
        <div key={course.slug} className="admin__panel card">
          <h4>{course.title}</h4>

          {course.dropOffPoints.length === 0 ? (
            <p className="muted">Sin datos de abandono todavía.</p>
          ) : (
            <ol className="admin__dropoffs">
              {course.dropOffPoints.map((point) => (
                <li key={point.lessonId}>
                  <span>{point.lessonTitle ?? point.lessonId}</span>
                  <strong>{point.count}</strong>
                </li>
              ))}
            </ol>
          )}
        </div>
      ))}
    </section>
  );
}
