# Protocolo player ↔ contenido

Contrato entre la plataforma y el HTML importado (ADR-008). El contenido se sirve dentro de
un `iframe` con `sandbox`, así que **no comparte cookies, `localStorage` ni sesión** con la
plataforma: todo lo que necesite comunicar viaja por `postMessage`.

Versión actual: **1**. Un cambio incompatible sube el número y la plataforma sigue aceptando
la versión anterior durante al menos un ciclo de importación.

## Del contenido a la plataforma

El contenido envía mensajes con `window.parent.postMessage(mensaje, '*')`.
El `'*'` es aceptable aquí porque el mensaje no contiene nada sensible; la plataforma
verifica el origen al recibir.

| `type` | Cuándo | Campos |
|---|---|---|
| `inkoova:ready` | Al terminar de montar el visor | `version` |
| `inkoova:position` | Al cambiar de slide | `version`, `positionRef` (string ≤ 200) |
| `inkoova:completed` | Al llegar a la última slide | `version` |
| `inkoova:height` | Cuando cambia la altura del documento | `version`, `height` (px) |

```js
// En el contenido, al llegar al final del bloque:
window.parent.postMessage({ type: 'inkoova:completed', version: 1 }, '*');
```

## De la plataforma al contenido

| `type` | Efecto |
|---|---|
| `inkoova:restore` | Pide al visor ir a `positionRef` (la última posición guardada) |
| `inkoova:theme` | Fija el tema: `{ theme: 'light' \| 'dark' }` |

## Reglas

1. **La plataforma valida el origen** de cada mensaje recibido: solo acepta los que vienen
   del `contentWindow` del iframe que ella misma creó.
2. **`positionRef` es opaco.** La plataforma lo guarda y lo devuelve sin interpretarlo, así
   que el contenido puede cambiar su formato sin migraciones de base de datos.
3. **Completar es idempotente.** Reenviar `inkoova:completed` no altera la fecha original.
4. **El contenido sigue funcionando solo.** Si no hay `window.parent` distinto de `window`,
   los `postMessage` no van a ninguna parte y el HTML se comporta como siempre. Ese es el
   criterio de aceptación de T-07: el flujo autodidacta por doble clic no se rompe.

## Fragmento a añadir al generador de contenido

Este bloque es todo lo que `content.py` necesita emitir para hablar con el player:

```js
(function () {
  const IN_PLATFORM = window.parent !== window;
  if (!IN_PLATFORM) return; // Doble clic: nada que hacer.

  const send = (type, extra) =>
    window.parent.postMessage({ type, version: 1, ...extra }, '*');

  send('inkoova:ready');

  window.addEventListener('hashchange', () =>
    send('inkoova:position', { positionRef: location.hash.slice(1) }),
  );

  window.addEventListener('message', (event) => {
    const data = event.data;
    if (!data || typeof data !== 'object') return;

    if (data.type === 'inkoova:restore' && data.positionRef) {
      location.hash = data.positionRef;
    }

    if (data.type === 'inkoova:theme') {
      document.documentElement.dataset.theme = data.theme;
    }
  });
})();
```

> TODO(T-07): añadir este fragmento a la plantilla de `content.py` para que los bloques
> generados a partir de ahora lo traigan de serie. Los bloques ya generados funcionan sin
> él: el player marca la lección como completada con el botón manual.
