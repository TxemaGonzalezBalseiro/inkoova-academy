import { useEffect, useState } from 'react';
import { api } from './api';

/**
 * Identidad de la academia: nombre, lema, logo y correo de contacto, editables desde el panel.
 *
 * Se pide una sola vez por carga de página y se guarda en el módulo. La cabecera y el pie la
 * usan en cada navegación: sin la caché, cada cambio de ruta que remonte el layout dispararía
 * otra petición para pintar el mismo nombre.
 *
 * Los valores por defecto son los que estaban escritos a mano en el layout. Si la petición
 * falla —la API caída, un despliegue a medias— la cabecera sigue diciendo lo de siempre en vez
 * de quedarse vacía, que es la única forma de fallo aceptable para la marca.
 */
/**
 * Los datos del titular del sitio. Públicos por obligación: el artículo 10 de la LSSI exige
 * publicarlos, y el aviso legal los necesita sin sesión.
 *
 * `missing` lo calcula el servidor. No se recalcula aquí a propósito: la regla de qué dato es
 * obligatorio pertenece al dominio, y duplicarla en el navegador es garantizar que un día el
 * panel y la página digan cosas distintas sobre lo mismo.
 */
export type BrandingLegal = {
  legalName: string | null;
  taxId: string | null;
  address: string | null;
  registryDetails: string | null;
  email: string | null;
  linkedInUrl: string | null;
  companyUrl: string | null;
  missing: string[];
};

export type Branding = {
  academyName: string;
  academyTagline: string;
  logoUrl: string | null;
  supportEmail: string | null;
  publicDomain: string | null;
  legal: BrandingLegal;
};

const FALLBACK: Branding = {
  academyName: 'Inkoova Academy',
  academyTagline: 'Ingeniería de agentes y prompts para builders senior en entornos regulados.',
  logoUrl: null,
  supportEmail: null,
  publicDomain: null,
  // Si la API no responde no se inventa un titular: se deja vacío y la página lo dice.
  legal: {
    legalName: null,
    taxId: null,
    address: null,
    registryDetails: null,
    email: null,
    linkedInUrl: null,
    companyUrl: null,
    missing: [],
  },
};

let cached: Branding | null = null;
let inFlight: Promise<Branding> | null = null;

function load(): Promise<Branding> {
  if (cached) {
    return Promise.resolve(cached);
  }

  // Una sola petición compartida: dos componentes que monten a la vez no deben pedir dos veces.
  inFlight ??= api
    .get<Branding>('/branding')
    .then((result) => {
      cached = { ...FALLBACK, ...result };
      return cached;
    })
    .catch(() => FALLBACK)
    .finally(() => {
      inFlight = null;
    });

  return inFlight;
}

export function useBranding(): Branding {
  const [branding, setBranding] = useState<Branding>(cached ?? FALLBACK);

  useEffect(() => {
    let active = true;

    void load().then((result) => {
      if (active) {
        setBranding(result);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  return branding;
}

/**
 * Parte el nombre en "todo menos la última palabra" + "última palabra", que es como estaba
 * escrita la marca a mano ("Inkoova **Academy**"). Con un nombre de una sola palabra devuelve
 * el prefijo vacío y no se pierde nada.
 */
export function splitBrandName(name: string): { lead: string; tail: string } {
  const trimmed = name.trim();
  const cut = trimmed.lastIndexOf(' ');

  return cut === -1
    ? { lead: '', tail: trimmed }
    : { lead: trimmed.slice(0, cut), tail: trimmed.slice(cut + 1) };
}
