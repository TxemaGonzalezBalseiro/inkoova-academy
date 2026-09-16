import { useCallback, useEffect, useState } from 'react';
import { api, ApiError } from '../lib/api';

type Query<T> = {
  data: T | null;
  error: ApiError | null;
  loading: boolean;
  reload: () => void;
};

type State<T> = {
  data: T | null;
  error: ApiError | null;
  /** El path que produjo estos datos. Sirve para saber si el estado corresponde al path actual. */
  settledPath: string | null;
};

/**
 * Fetch declarativo para GET. Aborta al desmontar, que es lo que evita el clásico
 * "setState en un componente desmontado" al navegar rápido entre cursos.
 *
 * `loading` se deriva en vez de guardarse: es cierto mientras el path pedido no coincida con
 * el que produjo el estado actual. Así el efecto solo escribe estado desde callbacks
 * asíncronos, nunca de forma síncrona, y no hay renders en cascada.
 *
 * No hay caché ni deduplicación: las pantallas que la necesitan (el catálogo) ya se sirven
 * cacheadas desde la API, y añadir react-query por esto sería desproporcionado.
 */
export function useApi<T>(path: string | null, deps: unknown[] = []): Query<T> {
  const [state, setState] = useState<State<T>>({ data: null, error: null, settledPath: null });
  const [nonce, setNonce] = useState(0);

  const reload = useCallback(() => setNonce((value) => value + 1), []);

  useEffect(() => {
    if (path === null) {
      return;
    }

    const controller = new AbortController();

    api
      .get<T>(path, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) {
          setState({ data: result, error: null, settledPath: path });
        }
      })
      .catch((caught: unknown) => {
        if (controller.signal.aborted) {
          return;
        }

        setState({
          data: null,
          error:
            caught instanceof ApiError
              ? caught
              : new ApiError(0, 'network', 'No se ha podido contactar con el servidor.'),
          settledPath: path,
        });
      });

    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [path, nonce, ...deps]);

  const loading = path !== null && state.settledPath !== path;

  return {
    // Mientras carga un path nuevo no se devuelven los datos del anterior: evita que una
    // pantalla muestre el curso equivocado durante un instante.
    data: loading ? null : state.data,
    error: loading ? null : state.error,
    loading,
    reload,
  };
}

/** Marca el título de la pestaña. Cada página lo declara; el layout no adivina. */
export function useDocumentTitle(title: string): void {
  useEffect(() => {
    document.title = `${title} · Inkoova Academy`;
  }, [title]);
}
