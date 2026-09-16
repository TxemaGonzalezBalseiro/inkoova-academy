/**
 * Portada generada para un curso que no tiene imagen propia.
 *
 * Antes eran las dos primeras letras del título sobre un degradado idéntico para todos, así
 * que en el catálogo salían tres tarjetas «PR» y dos «AG» y no se distinguía una de otra.
 *
 * Cada curso recibe ahora una figura distinta —nodos y aristas, capas, escudo, retícula— con
 * su propia paleta, derivadas del slug. Es SVG inline: sin peticiones, sin binarios en el
 * repositorio y nítido a cualquier tamaño. Cuando un curso tenga imagen de verdad, la imagen
 * manda y esto no se usa.
 */
type Palette = { from: string; to: string; accent: string };

const PALETTES: Palette[] = [
  { from: '#1e3a8a', to: '#db2777', accent: '#93c5fd' }, // marca
  { from: '#0f172a', to: '#0891b2', accent: '#67e8f9' }, // teal profundo
  { from: '#4c1d95', to: '#db2777', accent: '#f0abfc' }, // violeta
  { from: '#0c4a6e', to: '#1e3a8a', accent: '#7dd3fc' }, // azul
  { from: '#134e4a', to: '#0891b2', accent: '#5eead4' }, // verde azulado
  { from: '#7c2d12', to: '#db2777', accent: '#fdba74' }, // ámbar
];

/**
 * Hash estable del slug. El mismo curso enseña siempre la misma portada, en cualquier
 * navegador y sin guardar nada: si cambiara entre visitas, el catálogo parecería otro.
 */
function hashOf(slug: string): number {
  let hash = 0;

  for (let index = 0; index < slug.length; index++) {
    hash = (hash * 31 + slug.charCodeAt(index)) | 0;
  }

  return Math.abs(hash);
}

/** Nodos conectados: agentes. */
function Nodes({ accent }: { accent: string }) {
  const points = [
    [60, 70], [150, 45], [235, 80], [95, 140], [190, 135], [130, 195],
  ];

  return (
    <g>
      {[[0, 1], [1, 2], [0, 3], [1, 4], [2, 4], [3, 4], [3, 5], [4, 5]].map(([a, b], i) => (
        <line
          key={i}
          x1={points[a][0]}
          y1={points[a][1]}
          x2={points[b][0]}
          y2={points[b][1]}
          stroke={accent}
          strokeWidth="1.5"
          opacity="0.5"
        />
      ))}
      {points.map(([x, y], i) => (
        <circle key={i} cx={x} cy={y} r={i === 4 ? 13 : 8} fill={accent} opacity={i === 4 ? 0.95 : 0.75} />
      ))}
    </g>
  );
}

/** Capas apiladas: arquitectura y producción. */
function Layers({ accent }: { accent: string }) {
  return (
    <g stroke={accent} strokeWidth="2" fill="none">
      {[0, 1, 2].map((i) => (
        <g key={i} opacity={0.85 - i * 0.2}>
          <path d={`M60 ${80 + i * 45} L150 ${52 + i * 45} L240 ${80 + i * 45} L150 ${108 + i * 45} Z`} />
        </g>
      ))}
      <path d="M150 108 L150 198" strokeDasharray="4 5" opacity="0.5" />
    </g>
  );
}

/** Escudo con marca de comprobación: gobernanza y cumplimiento. */
function Shield({ accent }: { accent: string }) {
  return (
    <g stroke={accent} fill="none" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M150 42 L228 76 L228 128 C228 172 192 202 150 218 C108 202 72 172 72 128 L72 76 Z" opacity="0.9" />
      <path d="M118 128 L142 152 L186 104" strokeWidth="4" />
    </g>
  );
}

/** Llaves y cursor: prompts como texto y como código. */
function Brackets({ accent }: { accent: string }) {
  return (
    <g stroke={accent} fill="none" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round">
      <path d="M112 62 C86 62 86 108 66 130 C86 152 86 198 112 198" opacity="0.9" />
      <path d="M188 62 C214 62 214 108 234 130 C214 152 214 198 188 198" opacity="0.9" />
      <line x1="150" y1="96" x2="150" y2="164" strokeWidth="6" opacity="0.85" />
    </g>
  );
}

/** Escalones: un recorrido de fundamentos. */
function Steps({ accent }: { accent: string }) {
  return (
    <g fill={accent}>
      {[0, 1, 2, 3].map((i) => (
        <rect
          key={i}
          x={62 + i * 46}
          y={190 - i * 34}
          width="38"
          height={16 + i * 34}
          rx="4"
          opacity={0.45 + i * 0.16}
        />
      ))}
    </g>
  );
}

const SHAPES = [Nodes, Layers, Shield, Brackets, Steps];

export function CourseCover({ slug, title }: { slug: string; title: string }) {
  const hash = hashOf(slug);
  const palette = PALETTES[hash % PALETTES.length];
  const Shape = SHAPES[hash % SHAPES.length];
  const gradientId = `cover-${slug}`;

  return (
    <svg
      viewBox="0 0 300 240"
      preserveAspectRatio="xMidYMid slice"
      className="course-cover"
      role="img"
      aria-label={`Portada de ${title}`}
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor={palette.from} />
          <stop offset="100%" stopColor={palette.to} />
        </linearGradient>

        <pattern id={`${gradientId}-grid`} width="24" height="24" patternUnits="userSpaceOnUse">
          <path d="M24 0 L0 0 0 24" fill="none" stroke="#fff" strokeWidth="1" opacity="0.08" />
        </pattern>
      </defs>

      <rect width="300" height="240" fill={`url(#${gradientId})`} />
      <rect width="300" height="240" fill={`url(#${gradientId}-grid)`} />

      <Shape accent={palette.accent} />
    </svg>
  );
}
