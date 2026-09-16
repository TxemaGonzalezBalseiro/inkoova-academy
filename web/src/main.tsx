import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles/global.css';
// Después de global.css a propósito: el modo cristal redefine tokens que el oscuro por
// preferencia del sistema también toca, y en un empate de especificidad gana el último.
import './styles/glass.css';

const container = document.getElementById('root');

if (!container) {
  throw new Error('Falta el nodo #root en index.html.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
