# Frontend module standard

## Absolute filename and folder-name prohibition

Never create or introduce a new frontend module file or folder whose basename contains `todir` in any casing. This is absolute and has no component, application, or technical exception for new filesystem paths.

A frontend module exists only with its approved corresponding backend/module plan. Read parent governance and that plan first. Backend contracts and the approved business model remain authoritative: do not duplicate business truth in the frontend or invent a different domain model. Prefer the generated API contract/client when the baseline provides it.

Use no direct imports from another module's internals. Expose `public.ts` only when an approved UI integration surface is required. Make routes and actions permission-aware. Implement loading, empty, error, permission-denied and validation-feedback states; use optimistic updates only where correctness permits and maintain deliberate query invalidation/cache consistency.

The conceptual shape is:

```text
<module>/
├── api/
├── routes/
├── pages/
├── features/
├── components/
├── queries/
├── mutations/
├── schemas/
├── permissions/
├── i18n/
├── tests/
└── public.ts                 # only when needed
```

Create only folders required by the approved plan. Deliver accessible, responsive UI with relevant keyboard behavior, consistent forms, pagination/filter/search, localization, Arabic and RTL support, and measured client performance. The module UI is done only when its applicable contracts, permissions, states, accessibility, responsive/RTL behavior, cache correctness, frontend tests, and backend-plan alignment pass their exit criteria.
