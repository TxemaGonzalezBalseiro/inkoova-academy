/**
 * Traducción de los errores que devuelve el rodeo por Google o Apple.
 *
 * Vive aparte del componente porque el mensaje lo pinta la pantalla de acceso y el botón lo
 * pinta otro componente: un fichero que exporta a la vez un componente y una función rompe el
 * refresco en caliente de Vite.
 *
 * La API redirige con el **código** del error, no con el texto. Así el motivo no acaba escrito
 * en la barra de direcciones, en el historial ni en los registros del servidor, y el idioma lo
 * decide la SPA, que es quien sabe en cuál está la pantalla.
 */
const MESSAGES: Record<string, string> = {
  'auth.external_failed':
    'No hemos podido completar el acceso con ese proveedor. Inténtalo de nuevo.',
  'auth.external_no_subject': 'El proveedor no ha devuelto una identidad válida.',
  'auth.external_no_email':
    'Tu proveedor no ha compartido un correo, y sin él no podemos crear la cuenta. Prueba con email y contraseña.',
  'auth.external_email_unverified':
    'Ya hay una cuenta con ese correo y el proveedor no lo ha verificado. Entra con tu contraseña y enlaza la cuenta desde tu perfil.',
  'auth.provider_unknown': 'Ese proveedor no está disponible.',
  'account.email_taken': 'Ya existe una cuenta con ese email.',
};

export function externalErrorMessage(code: string | null): string | null {
  if (!code) {
    return null;
  }

  return MESSAGES[code] ?? 'No hemos podido completar el acceso con ese proveedor.';
}
