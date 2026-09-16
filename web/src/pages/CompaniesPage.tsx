import { useState } from 'react';
import { useDocumentTitle } from '../hooks/useApi';
import './course-detail.css';

/** Formación in-company (T-12). Captación por email; sin CRM todavía. */
export function CompaniesPage() {
  useDocumentTitle('Formación para empresas');

  const [sent, setSent] = useState(false);

  return (
    <div className="container section" style={{ maxWidth: 760 }}>
      {/* Misma cabecera que el resto de secciones del sitio, no un h1 suelto. */}
      <header className="section-header">
        <h1>Formación para equipos</h1>

        <p>
          El mismo programa, adaptado a vuestro caso real y a vuestra normativa. Sesiones en
          vivo, laboratorios sobre vuestro código y un expediente técnico que os sirva de
          plantilla.
        </p>
      </header>

      <section>
        <h2>Cómo funciona</h2>
        <ol className="steps">
          <li>
            <strong>Diagnóstico.</strong> Una sesión para ver qué estáis construyendo, en qué
            sector y con qué restricciones regulatorias.
          </li>
          <li>
            <strong>Propuesta.</strong> Qué bloques del programa aplican, cuáles sobran y qué hay
            que añadir. Con precio cerrado.
          </li>
          <li>
            <strong>Formación.</strong> Sesiones en vivo más el material de autoestudio, con
            acceso individual para cada persona del equipo.
          </li>
          <li>
            <strong>Seguimiento.</strong> Un mes de acompañamiento para las dudas que salen cuando
            se aplica de verdad.
          </li>
        </ol>
      </section>

      <section>
        <h2>Cuéntanos</h2>

        {sent ? (
          <div className="alert alert--success" role="status">
            Recibido. Te escribimos en un par de días laborables.
          </div>
        ) : (
          <form
            onSubmit={(event) => {
              event.preventDefault();
              const form = event.currentTarget;
              const get = (name: string) =>
                (form.elements.namedItem(name) as HTMLInputElement | HTMLTextAreaElement).value;

              // TODO(T-12): sustituir por POST /api/companies cuando exista la bandeja B2B.
              const body = [
                `Empresa: ${get('empresa')}`,
                `Contacto: ${get('contacto')}`,
                `Tamaño del equipo: ${get('equipo')}`,
                '',
                get('contexto'),
              ].join('\n');

              window.location.href = `mailto:empresas@inkoova.com?subject=${encodeURIComponent(
                `Formación in-company · ${get('empresa')}`,
              )}&body=${encodeURIComponent(body)}`;

              setSent(true);
            }}
          >
            <div className="field">
              <label htmlFor="empresa">Empresa</label>
              <input id="empresa" name="empresa" type="text" required />
            </div>

            <div className="field">
              <label htmlFor="contacto">Tu email</label>
              <input id="contacto" name="contacto" type="email" required autoComplete="email" />
            </div>

            <div className="field">
              <label htmlFor="equipo">Personas a formar</label>
              <input id="equipo" name="equipo" type="number" min={1} required />
            </div>

            <div className="field">
              <label htmlFor="contexto">Qué estáis construyendo</label>
              <textarea id="contexto" name="contexto" rows={6} required />
              <span className="hint">
                Sector, en qué punto estáis y qué os preocupa más: coste, fiabilidad o normativa.
              </span>
            </div>

            <button type="submit" className="btn btn--accent">
              Enviar consulta
            </button>
          </form>
        )}
      </section>
    </div>
  );
}
