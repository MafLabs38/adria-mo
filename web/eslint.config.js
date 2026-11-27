import js from '@eslint/js';
import astro from 'eslint-plugin-astro';
import sonarjs from 'eslint-plugin-sonarjs';
import unicorn from 'eslint-plugin-unicorn';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      'dist/**',
      '.astro/**',
      'coverage/**',
      'reports/**',
      'playwright-report/**',
      'test-results/**',
      '.stryker-tmp/**',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.strict,
  // The rules of the SonarQube server, in the editor and in `yarn lint`: SonarJS is its engine.
  sonarjs.configs.recommended,
  // Astro components, with the accessibility rules of jsx-a11y.
  ...astro.configs.recommended,
  ...astro.configs['jsx-a11y-recommended'],
  {
    files: ['**/*.{ts,mjs,js}'],
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
    plugins: { unicorn },
    rules: {
      // Sonar's S77xx rules come from this plugin: each line is the rule of the profile it stands for.
      'unicorn/no-negated-condition': 'error', // S7735
      'unicorn/no-useless-spread': 'error', // S7747
      'unicorn/prefer-at': 'error', // S7755
      'unicorn/prefer-global-this': 'error', // S7764
      'unicorn/prefer-string-raw': 'error', // S7780
      'unicorn/prefer-string-replace-all': 'error', // S7781
    },
  },
);
