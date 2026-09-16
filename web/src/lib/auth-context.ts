import { createContext } from 'react';
import type { Me } from './types';

export type AuthState = {
  user: Me | null;
  /** True hasta que se resuelve el intento de restaurar sesión al cargar. */
  loading: boolean;
  isAdmin: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
};

/**
 * El contexto vive aparte del proveedor: un fichero que exporta a la vez un componente y
 * un objeto no admite fast refresh, y este proveedor envuelve toda la aplicación.
 */
export const AuthContext = createContext<AuthState | null>(null);
