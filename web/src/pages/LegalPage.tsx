import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { EmptyState } from '../components/common';
import { useDocumentTitle } from '../hooks/useApi';
import { useBranding, type Branding } from '../lib/useBranding';
import './course-detail.css';

/** Versión de las políticas. Debe coincidir con PolicyVersion en la API (T-14). */
const POLICY_VERSION = '2026-08-30';

/**
 * Textos legales (T-14).
 *
 * Los datos identificativos —razón social, NIF, domicilio, contacto— salen de la marca
 * configurada en Administración, no del código. Antes estaban escritos aquí como `TODO(T-14)`:
 * no se podían rellenar sin desplegar y, con varias marcas, no podían ser distintos, cuando el
 * titular de cada sitio puede ser una sociedad distinta o una persona física.
 *
 * Lo que no ha cambiado: un dato que falta se SIGUE viendo. Inventar un NIF en un aviso legal
 * es una infracción del artículo 10 de la LSSI, no un hueco de maquetación.
 */
export function LegalPage() {
  const { document: slug = '' } = useParams();
  const branding = useBranding();
  const documents = buildDocuments(branding);
  const entry = documents[slug];

  useDocumentTitle(entry?.title ?? 'Legal');

  if (!entry) {
    return (
      <div className="container section">
        <EmptyState title="Ese documento no existe">
          <Link to="/legal/aviso-legal">Ver el aviso legal</Link>
        </EmptyState>
      </div>
    );
  }

  return (
    <div className="container section" style={{ maxWidth: 760 }}>
      <header className="section-header">
        <h1>{entry.title}</h1>
        <p>Versión {POLICY_VERSION}</p>
      </header>

      {entry.body}

      <nav className="row" style={{ marginTop: 'var(--space-12)' }} aria-label="Otros documentos legales">
        {Object.entries(documents)
          .filter(([key]) => key !== slug)
          .map(([key, value]) => (
            <Link key={key} to={`/legal/${key}`} className="btn btn--ghost btn--sm">
              {value.title}
            </Link>
          ))}
      </nav>
    </div>
  );
}

/**
 * El aviso de datos pendientes. Solo aparece si de verdad falta alguno, y dice CUÁLES: un
 * «faltan datos» genérico obliga a ir a compararlos uno a uno.
 *
 * La lista la calcula el servidor. Aquí no se decide qué es obligatorio: esa regla es del
 * dominio, y tenerla en dos sitios acaba con el panel diciendo que está completo y la página
 * diciendo que no.
 */
function Identification({ missing }: { missing: string[] }) {
  if (missing.length === 0) {
    return null;
  }

  return (
    <div className="alert alert--info">
      <strong>Faltan datos identificativos: {missing.join(', ')}.</strong> El artículo 10 de la
      LSSI los exige y no se pueden aproximar. Se rellenan en Administración ▸ Marcas.
    </div>
  );
}

/** Un dato del titular, o un hueco marcado si todavía no está puesto. */
function Dato({ value, label }: { value: string | null; label: string }) {
  return value ? <>{value}</> : <em>[pendiente: {label}]</em>;
}

function buildDocuments(b: Branding): Record<string, { title: string; body: ReactNode }> {
  const legal = b.legal;
  const identification = <Identification missing={legal.missing} />;
  const titular = legal.legalName ?? b.academyName;

  return {
  'aviso-legal': {
    title: 'Aviso legal',
    body: (
      <>
        {identification}

        <h2>Titular del sitio</h2>
        <p>
          Este sitio web es titularidad de {titular}, con domicilio en{' '}
          <Dato value={legal.address} label="domicilio" /> y NIF{' '}
          <Dato value={legal.taxId} label="NIF" />.
          {legal.registryDetails && <> {legal.registryDetails}.</>} Puedes contactar en{' '}
          {legal.email ? (
            <a href={`mailto:${legal.email}`}>{legal.email}</a>
          ) : (
            <Dato value={null} label="correo de contacto" />
          )}
          .
        </p>

        <h2>Objeto</h2>
        <p>
          Inkoova Academy ofrece formación en línea sobre ingeniería de sistemas con modelos de
          lenguaje y sobre el marco normativo europeo aplicable. El acceso a determinados
          contenidos requiere registro y suscripción.
        </p>

        <h2>Propiedad intelectual</h2>
        <p>
          Los contenidos del sitio —textos, materiales de curso, plantillas, código de ejemplo y
          diseño— son titularidad de Inkoova o se usan con autorización. La suscripción concede una
          licencia personal, intransferible y no exclusiva para el uso individual del material. No
          autoriza a redistribuirlo, revenderlo ni a usarlo para impartir formación a terceros.
        </p>
        <p>
          El código de los laboratorios se publica bajo licencia abierta cuando así se indica en el
          propio repositorio; en ese caso prevalece la licencia del repositorio.
        </p>

        <h2>Responsabilidad</h2>
        <p>
          El material tiene finalidad formativa. Las referencias normativas se acompañan de su
          artículo y de la fecha en que se verificaron, pero la normativa cambia:{' '}
          <strong>nada de lo publicado aquí constituye asesoramiento jurídico</strong>. Antes de
          aplicar cualquier criterio a un caso real, consúltalo con tu asesoría.
        </p>

        <h2>Enlaces</h2>
        <p>
          El sitio puede enlazar a recursos de terceros. Inkoova no controla su contenido ni asume
          responsabilidad sobre él.
        </p>

        <h2>Ley aplicable</h2>
        <p>
          Esta relación se rige por la legislación española. Para consumidores, la competencia
          judicial es la que determine la normativa de consumo aplicable.
        </p>
      </>
    ),
  },

  privacidad: {
    title: 'Política de privacidad',
    body: (
      <>
        {identification}

        <h2>Responsable del tratamiento</h2>
        <p>
          {titular}, NIF <Dato value={legal.taxId} label="NIF" />, domicilio en{' '}
          <Dato value={legal.address} label="domicilio" />. Contacto en materia de protección de
          datos:{' '}
          {legal.email ? (
            <a href={`mailto:${legal.email}`}>{legal.email}</a>
          ) : (
            <Dato value={null} label="correo de contacto" />
          )}
          .
        </p>

        <h2>Qué datos tratamos y para qué</h2>
        <dl className="faq-list">
          <dt>Cuenta y perfil (email, nombre)</dt>
          <dd>
            Para darte acceso, identificarte y emitir certificados a tu nombre. Base legal:
            ejecución del contrato (art. 6.1.b RGPD). Conservación: mientras la cuenta exista.
          </dd>

          <dt>Progreso, resultados de tests y certificados</dt>
          <dd>
            Para que la plataforma sepa dónde lo dejaste y pueda emitir y verificar certificados.
            Base legal: ejecución del contrato. Conservación: mientras la cuenta exista; los
            certificados emitidos se conservan para poder seguir verificándolos, sin datos
            personales una vez eliminada la cuenta.
          </dd>

          <dt>Datos de facturación</dt>
          <dd>
            Importes, fechas, país e identificador fiscal cuando compras como empresa. Base legal:
            obligación legal (art. 6.1.c RGPD). Conservación: la que exige la normativa fiscal.{' '}
            <strong>Los datos de tu tarjeta no llegan a nuestros servidores</strong>: los trata
            Stripe directamente.
          </dd>

          <dt>Registro de consentimiento de cookies</dt>
          <dd>
            Tu decisión y la versión de la política aceptada, para poder acreditarla. Base legal:
            obligación legal derivada del propio consentimiento.
          </dd>

          <dt>Datos de afiliado (si te das de alta como tal)</dt>
          <dd>
            NIF, país e IBAN, necesarios para liquidar comisiones y emitir o recibir facturas. Base
            legal: ejecución del contrato y obligación legal.
          </dd>
        </dl>

        <h2>Encargados y transferencias</h2>
        <p>
          Trabajamos con proveedores que actúan como encargados del tratamiento:{' '}
          <em>TODO(T-14): listar el proveedor de pagos (Stripe), el de correo transaccional y el
          de alojamiento con sus respectivas garantías de transferencia internacional</em>. Todos
          ellos están sujetos a contrato de encargo conforme al art. 28 RGPD.
        </p>

        <h2>Tus derechos</h2>
        <p>
          Puedes ejercer los derechos de acceso, rectificación, supresión, oposición, limitación y
          portabilidad. Los dos más habituales están automatizados en{' '}
          <Link to="/cuenta/datos">tu cuenta</Link>: descargar tus datos en JSON y eliminar la
          cuenta. Para el resto, escribe a{' '}
          <a href="mailto:privacidad@inkoova.com">privacidad@inkoova.com</a>.
        </p>
        <p>
          Si consideras que no hemos atendido bien tu solicitud, puedes reclamar ante la Agencia
          Española de Protección de Datos (
          <a href="https://www.aepd.es" rel="noreferrer noopener" target="_blank">
            aepd.es
          </a>
          ).
        </p>

        <h2>Decisiones automatizadas</h2>
        <p>
          No tomamos decisiones automatizadas con efectos jurídicos sobre ti. El resultado del test
          de nivel es una recomendación orientativa: no condiciona tu acceso a ningún curso.
        </p>
      </>
    ),
  },

  cookies: {
    title: 'Política de cookies',
    body: (
      <>
        <h2>Qué usamos</h2>
        <p>
          Usamos el mínimo posible. No hay cookies publicitarias ni de redes sociales, y ninguna
          cookie no esencial se instala antes de que la aceptes.
        </p>

        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Cookie</th>
                <th>Finalidad</th>
                <th>Duración</th>
                <th>Tipo</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>
                  <code>ink_rt</code>
                </td>
                <td>Mantener la sesión iniciada sin pedirte la contraseña cada 15 minutos.</td>
                <td>30 días</td>
                <td>Técnica necesaria</td>
              </tr>
              <tr>
                <td>
                  <code>ink_vid</code>
                </td>
                <td>
                  Identificador aleatorio de visita. Permite recuperar un test hecho antes de
                  registrarte y atribuir una recomendación de afiliado.
                </td>
                <td>30 días</td>
                <td>Técnica necesaria</td>
              </tr>
            </tbody>
          </table>
        </div>

        <h2>Analítica</h2>
        <p>
          Si aceptas la analítica, cargamos una herramienta que mide páginas vistas de forma
          agregada y <strong>sin cookies ni identificadores persistentes</strong>. Aun así te lo
          preguntamos antes, porque preferimos preguntar de más.
        </p>
        <p>
          Puedes cambiar de opinión cuando quieras borrando las cookies de este sitio desde tu
          navegador: el banner volverá a aparecer.
        </p>

        <h2>Cookies de terceros</h2>
        <p>
          Durante el pago, Stripe instala sus propias cookies en su dominio para prevención de
          fraude. Ese tratamiento se rige por la política de Stripe.
        </p>
      </>
    ),
  },

  terminos: {
    title: 'Términos y condiciones',
    body: (
      <>
        {identification}

        <h2>1. Objeto</h2>
        <p>
          Estos términos regulan la contratación de suscripciones y de productos digitales en
          Inkoova Academy.
        </p>

        <h2>2. Registro</h2>
        <p>
          Necesitas una cuenta y ser mayor de edad. Los datos que facilites deben ser veraces; la
          cuenta es personal e intransferible. Compartir credenciales es motivo de suspensión.
        </p>

        <h2>3. Precios y pago</h2>
        <p>
          Los precios mostrados incluyen los impuestos aplicables para consumidores en la UE. El
          pago se procesa mediante Stripe. La suscripción se renueva automáticamente al final de
          cada periodo hasta que la canceles.
        </p>

        <h2>4. Cancelación</h2>
        <p>
          Puedes cancelar en cualquier momento desde tu cuenta. La cancelación surte efecto al
          final del periodo ya pagado: conservas el acceso hasta esa fecha y no se te vuelve a
          cobrar. No hay permanencia.
        </p>

        <h2>5. Derecho de desistimiento</h2>
        <p>
          Como consumidor dispones de <strong>14 días naturales</strong> para desistir sin
          justificación (arts. 102 y siguientes del TRLGDCU).
        </p>
        <p>
          <strong>Excepción por contenido digital.</strong> Al contratar solicitas expresamente que
          el acceso al contenido comience de inmediato y reconoces que, una vez iniciada la
          ejecución con tu consentimiento previo, pierdes el derecho de desistimiento sobre el
          contenido ya suministrado (art. 103.m TRLGDCU). En la práctica: si no has abierto ninguna
          lección, se reembolsa el importe íntegro; si has empezado, se descuenta la parte
          consumida.
        </p>
        <p>
          Para desistir basta con escribir a <a href="mailto:hola@inkoova.com">hola@inkoova.com</a>{' '}
          indicando tu pedido.
        </p>

        <h2>6. Licencia de uso del material</h2>
        <p>
          Se concede una licencia personal, no exclusiva e intransferible para uso individual.
          Queda excluida la redistribución, la reventa y su uso para impartir formación a terceros.
        </p>

        <h2>7. Certificados</h2>
        <p>
          Se emiten al completar el 100 % de las clases obligatorias de un curso. Acreditan haber
          completado la formación, no constituyen titulación oficial. Inkoova puede revocar un
          certificado obtenido de forma fraudulenta.
        </p>

        <h2>8. Disponibilidad</h2>
        <p>
          Nos comprometemos a mantener el servicio disponible con las interrupciones habituales por
          mantenimiento. Si una incidencia prolongada impidiese el acceso, se compensará con la
          ampliación del periodo de suscripción.
        </p>

        <h2>9. Modificaciones</h2>
        <p>
          Podemos actualizar estos términos. Los cambios se comunican con antelación razonable y,
          si afectan a condiciones económicas de una suscripción en curso, se aplican a partir de
          la siguiente renovación.
        </p>

        <h2>10. Resolución de conflictos</h2>
        <p>
          Legislación española. Como consumidor puedes acudir a la plataforma europea de
          resolución de litigios en línea o a los organismos de consumo de tu comunidad autónoma.
        </p>
      </>
    ),
  },
  };
}
