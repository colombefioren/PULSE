# 3. TypeScript 6.0, not 7

Status: Accepted

Milestone: M0 - Foundations

## Decision and rationale

Vite scaffolds TypeScript 7 (the native compiler). `typescript-eslint` needs the compiler API and
supports `>=4.8.4 <6.1.0`, and the brief requires ESLint. TypeScript is pinned to `~6.0` until
`typescript-eslint` supports 7.
