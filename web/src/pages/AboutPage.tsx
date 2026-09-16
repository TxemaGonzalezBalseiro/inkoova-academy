import { Link } from 'react-router-dom';
import { Spinner } from '../components/common';
import { useApi, useDocumentTitle } from '../hooks/useApi';
import { useBranding } from '../lib/useBranding';

/**
 * «Sobre mí», con el contenido que se edita en Identidad.
 *
 * Estaba escrita a mano en este mismo fichero: el nombre del instructor y su biografía dentro
 * del código, así que cambiar una frase pedía un despliegue.
 *
 * El cuerpo llega como TEXTO PLANO con un marcado mínimo y se pinta como nodos de React, nunca
 * con `dangerouslySetInnerHTML`. Es lo que impide que un texto guardado desde el panel —o desde
 * una sesión de administración robada— acabe ejecutando scripts en la cara de cualquier
 * visitante.
 */
type About = {
  name: string;
  headline: string;
  body: string;
  photoUrl: string;
};

export function AboutPage() {
  useDocumentTitle('Sobre el instructor');

  const { data: about, loading } = useApi<About>('/about');
  const branding = useBranding();

  if (loading) {
    return <Spinner label="Cargando…" />;
  }

  const name = about?.name?.trim() || branding.academyName;
  const hasContent = Boolean(about?.headline?.trim() || about?.body?.trim());

  return (
    <div className="container section" style={{ maxWidth: 720 }}>
      <header className="section-header">
        <h1>Sobre el instructor</h1>
      </header>

      {about?.photoUrl && (
        <img
          className="about__photo"
          src={about.photoUrl}
          alt=""
          width={128}
          height={128}
          loading="lazy"
        />
      )}

      <p className="hero__lead">
        <strong>{name}</strong>
        {about?.headline?.trim() ? `. ${about.headline.trim()}` : ''}
      </p>

      {hasContent ? (
        <Body text={about?.body ?? ''} />
      ) : (
        <p className="muted">
          Todavía no se ha escrito esta página. Se edita desde{' '}
          <Link to="/admin/identidad">Administración · Identidad</Link>.
        </p>
      )}
    </div>
  );
}

/**
 * Marcado mínimo, y a propósito mínimo: `## título`, `- punto` y párrafos separados por una
 * línea en blanco. No hay negritas, ni enlaces, ni HTML.
 *
 * La tentación aquí es aceptar Markdown entero y pintarlo con una librería. Eso significa
 * confiar en que el sanitizador de esa librería no tenga un fallo, en una página pública. Con
 * tres reglas y texto escapado por React no hay nada que sanear.
 */
function Body({ text }: { text: string }) {
  // Bloques separados por una línea vacía. `\r\n` incluido: un texto pegado desde Windows lo
  // trae, y sin contemplarlo cada párrafo saldría con un salto de carro al final.
  const blocks = text.split(/\r?\n\s*\r?\n/).filter((block) => block.trim().length > 0);

  return (
    <>
      {blocks.map((block, index) => {
        const lines = block.split(/\r?\n/).map((line) => line.trim());

        if (lines[0].startsWith('## ')) {
          return <h2 key={index}>{lines[0].slice(3)}</h2>;
        }

        if (lines.every((line) => line.startsWith('- '))) {
          return (
            <ul key={index}>
              {lines.map((line, item) => (
                <li key={item}>{line.slice(2)}</li>
              ))}
            </ul>
          );
        }

        return <p key={index}>{lines.join(' ')}</p>;
      })}
    </>
  );
}
