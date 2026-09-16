import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErrorMessage, Spinner } from '../components/common';
import { formatMoney } from '../lib/format';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { api, ApiError } from '../lib/api';
import { useAuth } from '../lib/useAuth';
import type { Plan } from '../lib/types';
import './landing.css';

/**
 * Qué da el plan, en una o varias líneas, a partir de lo que incluye de verdad.
 *
 * «Todos los cursos» se dice en una línea y no enumerando los seis: la promesa que se compra es
 * el catálogo entero, presente y futuro, y una lista de seis títulos la contaría mal el día que
 * salga el séptimo. Cuando la selección es cerrada sí se enumeran, porque ahí el título concreto
 * ES lo que se compra y no decirlo obligaría a irse a otra página a averiguarlo.
 */
function planIncludes(plan: Plan): string[] {
  const lineas: string[] = [];

  if (plan.includesAllCourses) {
    lineas.push('Todos los cursos publicados, y los que salgan');
  }

  const cursos = plan.includedProducts.filter((p) => p.kind === 'course');
  if (!plan.includesAllCourses && cursos.length > 0) {
    lineas.push(...cursos.map((c) => c.title));
  }

  if (plan.includesAllPacks) {
    lineas.push('Todos los packs sectoriales');
  }

  const packs = plan.includedProducts.filter((p) => p.kind === 'pack');
  if (!plan.includesAllPacks && packs.length > 0) {
    lineas.push(...packs.map((p) => `Pack: ${p.title}`));
  }

  return lineas;
}

const INTERVAL_LABEL: Record<Plan['interval'], string> = {
  monthly: 'al mes',
  quarterly: 'cada 3 meses',
  biannual: 'cada 6 meses',
  yearly: 'al año',
  lifetime: 'pago único',
};

export function PricingPage() {
  useDocumentTitle('Planes y precios');

  return (
    <div className="container section">
      <header className="section-header">
        <h1>Planes</h1>
        <p className="muted">
          Todos los planes dan acceso a los cursos publicados. Los que incluyen packs traen además
          las plantillas sectoriales y sus actualizaciones.
        </p>
      </header>

      <PricingTable />

      <section style={{ marginTop: 'var(--space-16)' }}>
        <h2>Preguntas de compra</h2>
        <dl className="faq-list">
          <dt>¿Puedo cancelar cuando quiera?</dt>
          <dd>
            Sí. Cancelas desde tu cuenta y conservas el acceso hasta el final del periodo que ya
            tenías pagado. No hay permanencia.
          </dd>

          <dt>¿Y el derecho de desistimiento?</dt>
          <dd>
            Tienes 14 días naturales. La excepción legal es el contenido digital ya empezado: si
            accedes al curso durante ese plazo y nos pides el reembolso, se descuenta la parte
            consumida. Está detallado en los <a href="/legal/terminos">términos</a>.
          </dd>

          <dt>¿Los precios llevan IVA?</dt>
          <dd>
            Sí, los precios mostrados son finales para consumidores en la UE. Si compras como
            empresa con VAT intracomunitario válido, se aplica la inversión del sujeto pasivo en el
            checkout.
          </dd>

          <dt>¿Recibo factura?</dt>
          <dd>
            Sí, se emite automáticamente por cada pago y la tienes en{' '}
            <a href="/cuenta/facturas">tu cuenta</a>.
          </dd>
        </dl>
      </section>
    </div>
  );
}

export function PricingTable() {
  const { data: plans, error, loading } = useApi<Plan[]>('/plans');
  const { user } = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState<string | null>(null);
  const [checkoutError, setCheckoutError] = useState<string | null>(null);

  async function subscribe(planCode: string) {
    if (!user) {
      // Sin cuenta no hay checkout: se vuelve aquí después de registrarse.
      navigate('/registro', { state: { from: '/precios' } });
      return;
    }

    setBusy(planCode);
    setCheckoutError(null);

    try {
      const { url } = await api.post<{ url: string }>(`/checkout/plans/${planCode}`);
      window.location.assign(url);
    } catch (caught) {
      setCheckoutError(
        caught instanceof ApiError ? caught.message : 'No hemos podido iniciar el pago.',
      );
      setBusy(null);
    }
  }

  if (loading) {
    return <Spinner label="Cargando planes…" />;
  }

  if (error || !plans) {
    return <ErrorMessage>No hemos podido cargar los planes. Recarga la página.</ErrorMessage>;
  }

  // El plan destacado es el anual: el que mejor relación precio/valor tiene y el primero
  // que incluye packs.
  const highlighted = plans.find((plan) => plan.interval === 'yearly')?.code;

  return (
    <>
      {checkoutError && <ErrorMessage>{checkoutError}</ErrorMessage>}

      <div className="pricing-grid">
        {plans.map((plan) => (
          <article
            key={plan.code}
            className={`plan-card ${plan.code === highlighted ? 'plan-card--highlight' : ''}`}
          >
            <h3 className="plan-card__name">{plan.name}</h3>

            <p style={{ margin: 0 }}>
              <span className="plan-card__price">{formatMoney(plan.price, plan.currency)}</span>{' '}
              <span className="plan-card__interval">{INTERVAL_LABEL[plan.interval]}</span>
            </p>

            {/*
              Lo que da el plan sale del propio plan, no de una lista escrita a mano. Antes se
              tecleaba en «Qué incluye» y se desincronizaba del acceso real: la tarjeta prometía
              un curso que el plan no daba, o callaba uno que sí. Ahora el texto libre queda solo
              para lo que no es un producto —Discord, sesiones, soporte—, debajo.
            */}
            <ul>
              {planIncludes(plan).map((linea) => (
                <li key={linea}>{linea}</li>
              ))}

              {plan.benefits.map((benefit) => (
                <li key={benefit}>{benefit}</li>
              ))}
            </ul>

            <button
              type="button"
              className={plan.code === highlighted ? 'btn btn--accent' : 'btn btn--primary'}
              disabled={busy !== null}
              onClick={() => void subscribe(plan.code)}
            >
              {busy === plan.code ? 'Abriendo pago…' : 'Elegir plan'}
            </button>
          </article>
        ))}
      </div>
    </>
  );
}
