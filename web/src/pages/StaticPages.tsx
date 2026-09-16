import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useDocumentTitle } from '../hooks/useApi';
import './course-detail.css';

export function FaqPage() {
  useDocumentTitle('Preguntas frecuentes');

  return (
    <div className="container section" style={{ maxWidth: 720 }}>
      <header className="section-header">
        <h1>Preguntas frecuentes</h1>
      </header>

      <dl className="faq-list">
        <dt>¿Para quién es esto?</dt>
        <dd>
          Para quien ya construye software y lleva meses trasteando con APIs de LLM. Si nunca has
          llamado a una, el curso te irá demasiado rápido: haz el test de nivel y él te lo dirá sin
          rodeos.
        </dd>

        <dt>¿Hace falta saber machine learning?</dt>
        <dd>
          No. No hay gradientes ni fine-tuning. Se trata el modelo como una caja con una interfaz
          conocida y se construye sistema alrededor.
        </dd>

        <dt>¿Los cursos son en directo?</dt>
        <dd>
          No. Son de autoestudio, con material escrito denso y laboratorios. Los planes con
          comunidad incluyen sesiones grupales en directo para dudas.
        </dd>

        <dt>¿Cuánto tiempo necesito?</dt>
        <dd>
          Depende del curso. Cada ficha indica las horas estimadas de material. Los laboratorios
          suelen llevar entre 45 y 60 minutos cada uno.
        </dd>

        <dt>¿El certificado sirve para algo?</dt>
        <dd>
          Es verificable públicamente con un código, sin cuenta, y se puede añadir al perfil de
          LinkedIn. No es una titulación oficial y no lo presentamos como tal.
        </dd>

        <dt>¿Y si el contenido se queda desactualizado?</dt>
        <dd>
          Los cursos se actualizan y las actualizaciones están incluidas mientras tengas acceso. En
          los packs de normativa, cada fichero lleva su fecha de verificación en portada.
        </dd>

        <dt>¿Puedo usar esto en mi empresa?</dt>
        <dd>
          La suscripción es individual. Para equipos hay formación in-company:{' '}
          <Link to="/empresas">cuéntanos qué necesitáis</Link>.
        </dd>
      </dl>
    </div>
  );
}

export function SupportPage() {
  useDocumentTitle('Soporte');

  const [sent, setSent] = useState(false);

  return (
    <div className="container section" style={{ maxWidth: 640 }}>
      <header className="section-header">
        <h1>Soporte</h1>
        <p>
          Dudas de acceso, facturación o contenido. Respondemos en días laborables, normalmente
          en menos de 24 horas.
        </p>
      </header>

      {sent ? (
        <div className="alert alert--success" role="status">
          Mensaje enviado. Te contestamos al email desde el que escribes.
        </div>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            const form = event.currentTarget;
            const asunto = (form.elements.namedItem('asunto') as HTMLInputElement).value;
            const mensaje = (form.elements.namedItem('mensaje') as HTMLTextAreaElement).value;

            /*
             * TODO(T-05): sustituir por POST /api/support cuando exista la bandeja. Hasta
             * entonces se abre el cliente de correo: es honesto y funciona, en vez de
             * simular un envío que no llega a ninguna parte.
             */
            window.location.href = `mailto:soporte@inkoova.com?subject=${encodeURIComponent(
              asunto,
            )}&body=${encodeURIComponent(mensaje)}`;

            setSent(true);
          }}
        >
          <div className="field">
            <label htmlFor="asunto">Asunto</label>
            <input id="asunto" name="asunto" type="text" required />
          </div>

          <div className="field">
            <label htmlFor="mensaje">Mensaje</label>
            <textarea id="mensaje" name="mensaje" rows={7} required />
            <span className="hint">
              Si es un problema de acceso, dinos el curso y qué ves exactamente.
            </span>
          </div>

          <button type="submit" className="btn btn--primary">
            Enviar
          </button>
        </form>
      )}

      <p className="muted" style={{ marginTop: 'var(--space-8)', fontSize: 'var(--text-sm)' }}>
        También puedes escribir directamente a <a href="mailto:soporte@inkoova.com">soporte@inkoova.com</a>.
      </p>
    </div>
  );
}
