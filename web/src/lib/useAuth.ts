import { useContext } from 'react';
import { AuthContext, type AuthState } from './auth-context';

/**
 * Vive en su propio módulo: si conviviera con el proveedor, el fichero exportaría un hook y
 * un componente y el fast refresh dejaría de funcionar en toda la app.
 */
export function useAuth(): AuthState {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth debe usarse dentro de <AuthProvider>.');
  }

  return context;
}
