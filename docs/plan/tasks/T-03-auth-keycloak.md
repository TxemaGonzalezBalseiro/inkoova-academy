# T-03 · Autenticación y roles (ASP.NET Core Identity)

## Alcance
- ASP.NET Core Identity sobre la misma BD Postgres (tablas vía migración, acceso Dapper). Sin Keycloak: coste cero y una pieza menos.
- Registro con verificación de email, login, recuperación de contraseña, opcional Google/GitHub social login.
- Roles: `student` (default), `admin`.
- API: validación JWT, mapeo `sub` → `User` (alta lazy en primer acceso).
- Frontend: `AuthProvider`, guardas de ruta, páginas `/login`, `/registro`, `/cuenta`.
- Sincronizar email/displayName en cambios de perfil.

## Fuera de alcance
Planes y entitlements (T-04). MFA.

## Criterios de aceptación
- Flujo completo registro → email → login → `/api/me` con Playwright contra compose local.
- Endpoint admin devuelve 403 a `student`.
- Tokens con vida ≤ 15 min y refresh silencioso funcionando.

## Dependencias
T-00, T-02.
