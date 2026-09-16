/**
 * Cliente HTTP de la academia.
 *
 * Dos decisiones que conviene no deshacer sin pensarlo:
 *
 * 1. El access token vive en memoria, no en localStorage. Un XSS podría leer localStorage;
 *    de la memoria del módulo se lo lleva igual, pero al menos no sobrevive a la pestaña.
 *    El refresh token está en una cookie HttpOnly y JavaScript no lo ve nunca.
 * 2. Un 401 dispara UN solo intento de refresh, compartido entre todas las peticiones en
 *    vuelo. Sin eso, cargar una página con cinco fetch produce cinco refresh en paralelo y
 *    la rotación de tokens invalida la familia entera por reutilización.
 */

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

let accessToken: string | null = null;
let refreshInFlight: Promise<boolean> | null = null;
const listeners = new Set<(token: string | null) => void>();

export function setAccessToken(token: string | null): void {
  accessToken = token;
  listeners.forEach((listener) => listener(token));
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function onTokenChange(listener: (token: string | null) => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/**
 * La cookie de refresco es HttpOnly y JavaScript no puede verla, así que el servidor deja
 * junto a ella una marca legible que solo dice "hay sesión". Sirve para no pedir una
 * renovación que sabemos que va a fallar: sin esto, todo visitante anónimo produce un 401
 * en cada carga, que en la consola parece un fallo de la aplicación y tapa los de verdad.
 *
 * Es una pista, no una credencial: si miente, el 401 vuelve y se limpia sola.
 */
const SESSION_HINT_COOKIE = 'ink_session';

function hasSessionHint(): boolean {
  return document.cookie
    .split(';')
    .some((cookie) => cookie.trim().startsWith(`${SESSION_HINT_COOKIE}=`));
}

async function refreshAccessToken(): Promise<boolean> {
  // Coalesce: la primera llamada crea la promesa y el resto la esperan.
  refreshInFlight ??= (async () => {
    try {
      const response = await fetch(`${BASE}/auth/refresh`, {
        method: 'POST',
        credentials: 'include',
      });

      if (!response.ok) {
        setAccessToken(null);
        return false;
      }

      const body = (await response.json()) as { accessToken: string };
      setAccessToken(body.accessToken);
      return true;
    } catch {
      setAccessToken(null);
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

type RequestOptions = {
  method?: string;
  body?: unknown;
  /** Evita el reintento tras refrescar; lo usa el propio refresh para no recursar. */
  skipRefresh?: boolean;
  signal?: AbortSignal;
};

async function send(path: string, options: RequestOptions = {}): Promise<Response> {
  const headers: Record<string, string> = {};

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }

  return fetch(`${BASE}${path}`, {
    method: options.method ?? 'GET',
    headers,
    credentials: 'include',
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  });
}

async function toError(response: Response): Promise<ApiError> {
  let code = 'unknown';
  let message = 'Ha ocurrido un error. Inténtalo de nuevo.';

  try {
    const problem = (await response.json()) as { code?: string; detail?: string; title?: string };
    code = problem.code ?? code;
    message = problem.detail ?? problem.title ?? message;
  } catch {
    // Una respuesta sin cuerpo JSON (502 del proxy, por ejemplo) usa el mensaje genérico.
  }

  return new ApiError(response.status, code, message);
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options);

  if (response.status === 401 && !options.skipRefresh) {
    if (await refreshAccessToken()) {
      response = await send(path, { ...options, skipRefresh: true });
    }
  }

  if (!response.ok) {
    throw await toError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * Descarga un fichero de un endpoint que exige sesión.
 *
 * Un `<a href="/api/…">` NO sirve para esto: el navegador navega sin cabeceras propias, y el
 * token vive en memoria, no en una cookie. El servidor ve una petición anónima y responde 401,
 * que es lo que pasaba con la vista previa del certificado.
 *
 * Aquí la petición va por el mismo camino que las demás —con el token y con el reintento tras
 * renovar— y lo que se obtiene es el fichero.
 */
export async function requestBlob(path: string, options: RequestOptions = {}): Promise<Blob> {
  let response = await send(path, options);

  if (response.status === 401 && !options.skipRefresh) {
    if (await refreshAccessToken()) {
      response = await send(path, { ...options, skipRefresh: true });
    }
  }

  if (!response.ok) {
    throw await toError(response);
  }

  return response.blob();
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { signal }),
  blob: (path: string) => requestBlob(path),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
  del: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  /**
   * Intenta recuperar la sesión al cargar la app. Sin marca de sesión ni siquiera lo
   * intenta: un visitante anónimo no tiene nada que renovar.
   *
   * Solo se comprueba aquí. Un 401 en una petición normal sigue disparando el refresh sin
   * mirar la marca, porque en ese caso la app ya creía tener sesión y quien manda es el
   * servidor.
   */
  restoreSession: () => (hasSessionHint() ? refreshAccessToken() : Promise.resolve(false)),
  baseUrl: BASE,
};
