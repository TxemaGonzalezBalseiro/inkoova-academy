import { useId } from 'react';
import type { ComponentType, SVGProps } from 'react';
import { useTheme } from '../lib/useTheme';
import type { ResolvedTheme, ThemePreference } from '../lib/theme';
import { IconDisplay, IconMoon, IconSparkle, IconSun } from './icons';
import './theme-toggle.css';

type Option = {
  value: ThemePreference;
  label: string;
  Icon: ComponentType<SVGProps<SVGSVGElement> & { size?: number }>;
};

const OPTIONS: readonly Option[] = [
  { value: 'light', label: 'Claro', Icon: IconSun },
  { value: 'dark', label: 'Oscuro', Icon: IconMoon },
  { value: 'glass', label: 'Cristal', Icon: IconSparkle },
  { value: 'auto', label: 'Automático', Icon: IconDisplay },
];

const RESOLVED_LABEL: Record<ResolvedTheme, string> = {
  light: 'claro',
  dark: 'oscuro',
  glass: 'cristal',
};

/**
 * Selector de tema de la plataforma.
 *
 * Se construye sobre radios nativos ocultos, no sobre botones con `aria-pressed`: así el
 * navegador da gratis lo que costaría replicar a mano y sale peor —el grupo se anuncia como
 * grupo, las flechas lo recorren, el foco entra por la opción marcada y el estado marcado lo
 * dice el propio control. Los iconos son solo la piel.
 *
 * El tema del contenido de curso que va dentro del iframe del player es otro sistema, con su
 * propio selector y su propia clave. Este control no lo toca.
 */
export function ThemeToggle() {
  const { preference, resolved, setPreference } = useTheme();
  const groupName = useId();

  const active = OPTIONS.find((option) => option.value === preference) ?? OPTIONS[3];

  return (
    <fieldset className="theme-toggle">
      <legend className="sr-only">Tema de la interfaz</legend>

      {OPTIONS.map(({ value, label, Icon }) => (
        <label
          key={value}
          className="theme-toggle__option"
          /* El título es para el ratón; el nombre accesible lo pone el texto oculto. */
          title={label}
        >
          <input
            className="sr-only"
            type="radio"
            name={groupName}
            value={value}
            checked={preference === value}
            onChange={() => setPreference(value)}
          />
          <Icon size={16} />
          <span className="sr-only">{label}</span>
        </label>
      ))}

      {/*
        El radio ya anuncia «seleccionado» al moverse por el grupo, pero en automático eso no
        dice qué se está viendo. Esta línea lo cierra y solo cambia cuando cambia el tema.
      */}
      <span className="sr-only" role="status">
        {active.value === 'auto'
          ? `Tema automático: el sistema está en ${RESOLVED_LABEL[resolved]}.`
          : `Tema ${RESOLVED_LABEL[resolved]}.`}
      </span>
    </fieldset>
  );
}
