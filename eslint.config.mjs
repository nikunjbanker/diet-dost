/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

export default [
  {
    ignores: [
      "**/bin/**",
      "**/obj/**",
      "**/node_modules/**",
      "**/dist/**",
      "**/publish/**",
      "**/*.min.js",
      "artifacts/**",
      "docs/.kilo/**",
      ".agents/**",
      "scratch/**",
      "TestResults/**"
    ]
  },
  {
    files: ["src/Nutrition.WebGateway/wwwroot/js/**/*.js"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        window: "readonly",
        document: "readonly",
        navigator: "readonly",
        location: "readonly",
        localStorage: "readonly",
        sessionStorage: "readonly",
        console: "readonly",
        fetch: "readonly",
        setTimeout: "readonly",
        clearTimeout: "readonly",
        setInterval: "readonly",
        clearInterval: "readonly",
        Event: "readonly",
        CustomEvent: "readonly",
        HTMLElement: "readonly",
        customElements: "readonly",
        FormData: "readonly",
        URL: "readonly",
        URLSearchParams: "readonly",
        Blob: "readonly",
        File: "readonly",
        FileReader: "readonly",
        Image: "readonly",
        performance: "readonly",
        requestAnimationFrame: "readonly",
        alert: "readonly",
        confirm: "readonly",
        Response: "readonly",
        Headers: "readonly"
      }
    },
    rules: {
      "no-unused-vars": ["error", { "argsIgnorePattern": "^_", "varsIgnorePattern": "^_", "caughtErrors": "none" }],
      "no-console": "off",
      "no-undef": "error"
    }
  },
  {
    files: ["src/Nutrition.WebGateway/wwwroot/sw.js"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        self: "readonly",
        caches: "readonly",
        Response: "readonly",
        Request: "readonly",
        fetch: "readonly",
        console: "readonly"
      }
    },
    rules: {
      "no-unused-vars": ["error", { "argsIgnorePattern": "^_", "varsIgnorePattern": "^_", "caughtErrors": "none" }],
      "no-console": "off",
      "no-undef": "error"
    }
  },
  {
    files: ["tests/**/*.mjs", "tests/**/*.js", "scripts/**/*.mjs", "scripts/**/*.js", "eslint.config.mjs"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        process: "readonly",
        console: "readonly",
        Buffer: "readonly",
        setTimeout: "readonly",
        clearTimeout: "readonly",
        setInterval: "readonly",
        clearInterval: "readonly",
        fetch: "readonly",
        URL: "readonly",
        URLSearchParams: "readonly",
        WebSocket: "readonly",
        AbortController: "readonly"
      }
    },
    rules: {
      "no-unused-vars": ["error", { "argsIgnorePattern": "^_", "varsIgnorePattern": "^_", "caughtErrors": "none" }],
      "no-console": "off",
      "no-undef": "error"
    }
  }
];
