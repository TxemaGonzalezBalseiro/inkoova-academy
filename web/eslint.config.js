import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'playwright-report', 'test-results'] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],

      // Un catch que ignora el error a propósito es un patrón deliberado en esta base
      // (fallos de red que no deben romper la navegación); se exige nombrarlo con _.
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_', caughtErrorsIgnorePattern: '^_' },
      ],

      // El tipado de las respuestas de la API se declara a mano en lib/types.ts; un `any`
      // suelto ahí dentro rompería esa garantía.
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },
  {
    // Los tests de Playwright corren en Node, no en el navegador.
    files: ['tests/**/*.ts'],
    languageOptions: { globals: globals.node },
  },
);
