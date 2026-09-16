import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api, setAccessToken } from './api';
import { AuthContext, type AuthState } from './auth-context';
import type { Me } from './types';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Me | null>(null);
  const [loading, setLoading] = useState(true);

  const loadUser = useCallback(async () => {
    try {
      setUser(await api.get<Me>('/me'));
    } catch {
      setUser(null);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    // El refresh token está en una cookie; si sigue vivo recuperamos la sesión sin que el
    // usuario vuelva a escribir nada.
    void (async () => {
      const restored = await api.restoreSession();

      if (cancelled) {
        return;
      }

      if (restored) {
        await loadUser();
      }

      if (!cancelled) {
        setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [loadUser]);

  const login = useCallback(
    async (email: string, password: string) => {
      const tokens = await api.post<{ accessToken: string }>('/auth/login', { email, password });
      setAccessToken(tokens.accessToken);
      await loadUser();
    },
    [loadUser],
  );

  const register = useCallback(async (email: string, password: string, displayName: string) => {
    await api.post('/auth/register', { email, password, displayName });
    // No se inicia sesión: la cuenta necesita confirmar el email primero (T-03).
  }, []);

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout');
    } finally {
      setAccessToken(null);
      setUser(null);
    }
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      loading,
      isAdmin: user?.roles.includes('admin') ?? false,
      login,
      register,
      logout,
      refreshUser: loadUser,
    }),
    [user, loading, login, register, logout, loadUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
