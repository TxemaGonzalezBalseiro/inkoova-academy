import { api } from './api';

/**
 * Abre un fichero que exige sesión.
 *
 * Un `<a href="/api/…">` no vale para esto y es un error fácil de cometer, porque *parece* que
 * funciona: el navegador navega sin la cabecera `Authorization`, y el token de esta plataforma
 * vive en memoria y no en una cookie. El servidor ve una petición anónima y responde 401, así
 * que en lugar del PDF sale la pantalla de error del navegador.
 *
 * Lo que sí funciona: pedirlo con el cliente —que pone el token y reintenta tras renovar— y
 * abrir el resultado como blob.
 *
 * Ojo con el orden: la pestaña se abre EN EL CLIC, antes de esperar nada. Si se abriera después
 * del `await`, el navegador no la relacionaría con una interacción y la bloquearía como
 * emergente.
 */
export async function openAuthenticatedFile(path: string, fileName: string): Promise<void> {
  const tab = window.open('', '_blank', 'noopener');

  try {
    const file = await api.blob(path);
    const url = URL.createObjectURL(file);

    if (tab) {
      tab.location.href = url;
    } else {
      // Emergentes bloqueadas: se descarga. Es peor que verlo, pero mejor que no dar nada.
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
    }

    // Se libera tarde: revocarlo enseguida deja la pestaña recién abierta sin nada que pintar.
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
  } catch (error) {
    // La pestaña en blanco se cierra: dejarla abierta y vacía parece que la aplicación se ha
    // colgado. Quien llama decide qué mensaje enseñar.
    tab?.close();
    throw error;
  }
}
