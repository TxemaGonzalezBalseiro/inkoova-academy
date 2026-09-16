import { Fragment, type JSX } from 'react';

/**
 * Enunciados con el poco marcado que traen: negrita, cursiva y código.
 *
 * Antes esto era `dangerouslySetInnerHTML` sobre el texto de la pregunta, que es lo único del
 * frontal que metía HTML de la base de datos en el origen principal —fuera del iframe del
 * player, que sí está aislado a propósito—. `AboutPage` y `AdminIdentities` documentan que no
 * se hace eso; el quiz era la excepción, y una excepción a una regla de seguridad solo hace
 * falta que alguien la encuentre.
 *
 * El marcado que hay en los manifests son tres etiquetas sin atributos:
 * `<strong>`, `<em>` y `<code>`. Se reconocen esas y nada más. Lo que no encaje sale como
 * texto literal, así que un enunciado con `<img onerror=...>` se lee, no se ejecuta.
 *
 * No es un sanitizador: es un reconocedor con lista blanca. La diferencia importa, porque un
 * sanitizador quita lo peligroso de un árbol HTML —y se le escapan cosas— mientras que esto
 * nunca construye ese árbol.
 */

const TAGS = { strong: 'strong', em: 'em', code: 'code' } as const;

type Tag = keyof typeof TAGS;

// Apertura o cierre de una de las tres, sin atributos. Cualquier otro '<' no casa y viaja
// como carácter normal.
const TOKEN = /<(\/?)(strong|em|code)>/gi;

export function RichText({ text, className }: { text: string; className?: string }): JSX.Element {
  return <span className={className}>{parse(text)}</span>;
}

function parse(text: string): JSX.Element[] {
  const out: JSX.Element[] = [];

  // Pila de etiquetas abiertas. Sin ella, `<strong>a<em>b</em>c</strong>` perdería el anidado.
  const open: { tag: Tag; children: JSX.Element[] }[] = [];
  const push = (node: JSX.Element) => (open.at(-1)?.children ?? out).push(node);

  let last = 0;
  let key = 0;

  TOKEN.lastIndex = 0;

  for (let match = TOKEN.exec(text); match !== null; match = TOKEN.exec(text)) {
    if (match.index > last) {
      push(<Fragment key={key++}>{text.slice(last, match.index)}</Fragment>);
    }

    last = match.index + match[0].length;

    const tag = match[2].toLowerCase() as Tag;

    if (!match[1]) {
      open.push({ tag, children: [] });
      continue;
    }

    // Un cierre sin su apertura, o que cierra otra cosa, se queda como texto: es contenido
    // mal escrito, y tragárselo en silencio esconde la errata a quien la tenga que arreglar.
    if (open.at(-1)?.tag !== tag) {
      push(<Fragment key={key++}>{match[0]}</Fragment>);
      continue;
    }

    const done = open.pop()!;
    const Element = TAGS[done.tag];

    push(<Element key={key++}>{done.children}</Element>);
  }

  if (last < text.length) {
    push(<Fragment key={key++}>{text.slice(last)}</Fragment>);
  }

  // Etiquetas que se quedaron abiertas: se vuelca su contenido sin envolver, para no perder
  // texto por una etiqueta sin cerrar.
  while (open.length > 0) {
    const dangling = open.pop()!;
    (open.at(-1)?.children ?? out).push(<Fragment key={key++}>{dangling.children}</Fragment>);
  }

  return out;
}
