import { useCallback, useEffect, useState } from 'react';
import {
  applyTheme,
  readStoredTheme,
  storeTheme,
  systemTheme,
  watchSystemTheme,
  THEME_STORAGE_KEY,
  type ResolvedTheme,
  type ThemePreference,
} from './theme';

export type ThemeState = {
  /** Lo que la persona ha elegido: claro, oscuro o automático. */
  preference: ThemePreference;
  /** Lo que se está pintando ahora mismo. En automático depende del sistema. */
  resolved: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
};

/**
 * Estado del tema. Vive en su propio módulo (no junto al componente) porque un fichero que
 * exporta a la vez un hook y un componente rompe el fast refresh.
 *
 * No hay contexto: el único consumidor es el selector de la cabecera. Lo que de verdad
 * comparte el estado con el resto de la app es el atributo `data-theme` del `<html>`, que es
 * de donde cuelgan los tokens.
 */
export function useTheme(): ThemeState {
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredTheme);
  const [system, setSystem] = useState<ResolvedTheme>(systemTheme);

  /*
   * El script inline de `index.html` ya dejó pintado el tema correcto antes del primer
   * render; este efecto solo tiene trabajo cuando la preferencia cambia después.
   */
  useEffect(() => {
    applyTheme(preference);
  }, [preference]);

  /*
   * En automático el sistema puede cambiar con la página abierta (el modo nocturno del SO
   * salta a una hora). El CSS ya reacciona solo, pero el selector tiene que poder decir qué
   * se está viendo.
   */
  useEffect(() => watchSystemTheme(setSystem), []);

  /*
   * El tema es de la persona, no de la pestaña: si lo cambia en una, las demás se enteran.
   * `storage` solo dispara en las OTRAS pestañas, así que no hay bucle con el estado local.
   */
  useEffect(() => {
    function handleStorage(event: StorageEvent) {
      if (event.key !== null && event.key !== THEME_STORAGE_KEY) {
        return;
      }

      setPreferenceState(readStoredTheme());
    }

    window.addEventListener('storage', handleStorage);

    return () => window.removeEventListener('storage', handleStorage);
  }, []);

  const setPreference = useCallback((next: ThemePreference) => {
    storeTheme(next);
    setPreferenceState(next);
  }, []);

  return {
    preference,
    resolved: preference === 'auto' ? system : preference,
    setPreference,
  };
}
