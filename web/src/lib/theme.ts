/*
 * Preferencia de tema de la plataforma.
 *
 * Son tres estados y no dos a propósito: «automático» no es lo mismo que «claro» aunque el
 * sistema esté en claro. Quien elige automático está diciendo «sigue al sistema», y esa
 * decisión tiene que sobrevivir a que el sistema cambie por la noche.
 *
 * Ojo: el contenido de curso que el player mete en un iframe trae su propio selector y su
 * propia clave (`agent-engineering-v3-theme`). Son dos sistemas independientes por diseño,
 * así que aquí se usa una clave distinta y no se sincroniza nada con el iframe.
 */

/**
 * `glass` es un tercer aspecto, no un tercer color: sigue las pautas de interfaz de Apple
 * —materiales translúcidos para dar profundidad, radios amplios, tipografía del sistema— y
 * dentro de él sigue habiendo claro y oscuro, que decide el sistema operativo. Por eso no
 * sustituye a los otros dos: se elige *en vez* de fijar claro u oscuro a mano.
 */
export type ThemePreference = 'light' | 'dark' | 'glass' | 'auto';

/** Lo que acaba pintado: «automático» ya está resuelto contra la preferencia del sistema. */
export type ResolvedTheme = 'light' | 'dark' | 'glass';

/**
 * Duplicada literalmente en el script inline de `index.html`. Ese script corre antes de que
 * exista el bundle, así que no puede importar de aquí; si esta clave cambia, hay que
 * cambiarla también allí.
 */
export const THEME_STORAGE_KEY = 'inkoova-academy-theme';

export const THEME_PREFERENCES: readonly ThemePreference[] = ['light', 'dark', 'glass', 'auto'];

const DARK_QUERY = '(prefers-color-scheme: dark)';

function isThemePreference(value: unknown): value is ThemePreference {
  return value === 'light' || value === 'dark' || value === 'glass' || value === 'auto';
}

/**
 * El claro u oscuro que hay DEBAJO de un tema.
 *
 * Hace falta para lo que no entiende de cristal: el contenido de curso que va dentro del
 * iframe solo tiene dos aspectos, y mandarle «glass» lo dejaría en claro con la academia en
 * oscuro. En cristal manda el sistema, igual que en automático.
 */
export function baseAppearance(resolved: ResolvedTheme): 'light' | 'dark' {
  return resolved === 'glass' ? systemTheme() : resolved;
}

/**
 * Cualquier valor que no reconozcamos (clave manipulada, versión anterior) cae en
 * «automático», que es el estado sin compromiso.
 */
export function readStoredTheme(): ThemePreference {
  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY);

    return isThemePreference(stored) ? stored : 'auto';
  } catch {
    // Navegación privada o almacenamiento bloqueado por el navegador: no es un error que
    // deba romper la cabecera, simplemente no habrá memoria entre visitas.
    return 'auto';
  }
}

export function storeTheme(preference: ThemePreference): void {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // Igual que arriba: la elección se aplica en esta sesión aunque no se pueda guardar.
  }
}

/**
 * En «automático» se QUITA el atributo, no se pone `data-theme="auto"`. Los tokens oscuros
 * viven bajo `@media (prefers-color-scheme: dark)` con el guardián
 * `:root:not([data-theme='light'])`, y solo mandan si no hay un `data-theme` que los tape.
 */
export function applyTheme(preference: ThemePreference): void {
  const root = document.documentElement;

  if (preference === 'auto') {
    root.removeAttribute('data-theme');
    return;
  }

  root.setAttribute('data-theme', preference);
}

export function systemTheme(): 'light' | 'dark' {
  return window.matchMedia(DARK_QUERY).matches ? 'dark' : 'light';
}

export function watchSystemTheme(onChange: (theme: ResolvedTheme) => void): () => void {
  const query = window.matchMedia(DARK_QUERY);
  const handler = (event: MediaQueryListEvent) => onChange(event.matches ? 'dark' : 'light');

  query.addEventListener('change', handler);

  return () => query.removeEventListener('change', handler);
}
