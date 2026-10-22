import js from "@eslint/js";
import pluginQuery from "@tanstack/eslint-plugin-query";
import jsxA11y from "eslint-plugin-jsx-a11y";
import reactHooks from "eslint-plugin-react-hooks";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist", "node_modules", "test-results", "playwright-report", "e2e/screenshots"] },
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      ...tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      jsxA11y.flatConfigs.recommended,
      ...pluginQuery.configs["flat/recommended"],
    ],
    languageOptions: { ecmaVersion: 2023, globals: globals.browser },
    rules: {
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }],
    },
  },
  {
    // shadcn/ui sources are vendored as generated (web/components.json); keep them diffable against upstream
    files: ["src/components/ui/**"],
    rules: { "jsx-a11y/heading-has-content": "off", "jsx-a11y/anchor-has-content": "off" },
  },
  {
    files: ["e2e/**", "*.config.ts", "eslint.config.js"],
    languageOptions: { globals: globals.node },
  },
);
