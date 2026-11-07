# TODIR Component Architecture & Engineering Patterns

This guide defines the authoritative component architecture for **TODIR ERP Frontend** (`frontend/`). Every pattern below matches the locked repository architecture in `frontend/AGENTS.md`.

---

## 1. Directory Structure & Module Ownership

The repository structure is strictly closed:

```text
frontend/
├── app/                  # Bootstrap, providers, router, AppShell, Oxford Navy Topbar
├── core/                 # httpClient, session, tenant context, permissions, queryCache
├── components/           # Single source of truth for ALL shared UI components & design system
│   ├── fields/           # MoneyDisplay, StatusBadge, Domain-specific badges
│   ├── forms/            # FormField, FormSection, FormActions, ValidationSummary
│   ├── formatting/       # quantity-formatter.ts, bidi-text.tsx
│   ├── grid/             # Responsive DataGrid, column definitions
│   ├── pages/            # Page archetypes: ListPage, RecordPage, DocumentPage, Surface
│   └── primitives/       # Button, Dialog, Select, ComboBox, Table, Pagination, Icons
├── modules/<module>/     # Autonomous vertical business slices (sales, crm, purchasing, accounting, etc.)
│   ├── pages/            # <entity>-list-page.tsx, <entity>-record-page.tsx
│   ├── features/         # Complex modals, multi-step dialogs, feature tabs
│   ├── components/       # Module-internal domain widgets (party-picker, account-picker)
│   ├── services/         # API adapters using core httpClient
│   ├── hooks/            # TanStack Query hooks using queryKeyFactory
│   ├── validation/       # Zod schemas matching backend command validation
│   ├── i18n/             # ar.ts and en.ts with 100% parity
│   ├── types.ts          # Module DTOs and contracts
│   ├── routes.tsx        # Sub-routes registration conforming to ModuleRouteRegistration
│   └── tests/            # Slice and domain invariant unit tests
└── tests/                # Global E2E Playwright tests and architecture checks
```

### Module Boundary Invariants:
1. Modules NEVER import from each other's internal folders. Cross-module communication uses explicit `public.ts` exports or shared contracts.
2. Modules NEVER build custom buttons, bespoke HTML tables, or custom card wrappers. Always import from `frontend/components/`.
3. `core/` never imports from `modules/`.

---

## 2. Codified Design Tokens & Styling (Zero Tailwind)

TODIR does NOT use Tailwind utility classes. All components use CSS Variables and design system tokens:

```css
/* Color Palette */
--color-primary: #102a43;        /* Dark Oxford Navy Header Anchor */
--color-primary-hover: #1e3a5f;
--color-background-canvas: #f0f4f8; /* Cool Neutral Slate */
--color-surface-card: #ffffff;   /* Crisp White */
--color-border-default: #cbd5e1; /* Solid Perimeter Border */
--color-text-primary: #0f172a;
--color-text-secondary: #475569;
--color-text-muted: #94a3b8;

/* Shadows */
--shadow-sm: 0 1px 3px rgba(15, 23, 42, 0.08), 0 1px 2px rgba(15, 23, 42, 0.04);
--shadow-md: 0 4px 6px -1px rgba(15, 23, 42, 0.1), 0 2px 4px -2px rgba(15, 23, 42, 0.06);
--shadow-lg: 0 10px 15px -3px rgba(15, 23, 42, 0.1), 0 4px 6px -4px rgba(15, 23, 42, 0.05);
```

### Writing Component Styles:
Use inline styles with logical properties or dedicated CSS files:
```tsx
// Correct TODIR style pattern
<div style={{
  backgroundColor: "var(--color-surface-card)",
  border: "1px solid var(--color-border-default)",
  borderRadius: "8px",
  padding: "16px",
  boxShadow: "var(--shadow-sm)",
  marginInlineStart: "8px",
  paddingInlineEnd: "12px",
  textAlign: "start"
}}>
  {children}
</div>
```

---

## 3. Data Fetching (TanStack Query Pattern)

Every module defines a `queryKeyFactory` to prevent cache collision:

```tsx
// frontend/modules/crm/hooks/use-crm-queries.ts
export const crmQueryKeys = {
  all: ["crm"] as const,
  leads: (entityId: string, params?: Record<string, unknown>) => 
    [...crmQueryKeys.all, "leads", entityId, params] as const,
  lead: (entityId: string, id: string) => 
    [...crmQueryKeys.all, "lead", entityId, id] as const,
  opportunities: (entityId: string, params?: Record<string, unknown>) => 
    [...crmQueryKeys.all, "opportunities", entityId, params] as const,
};

export function useLeads(entityId: string, params: LeadFilterParams) {
  return useQuery({
    queryKey: crmQueryKeys.leads(entityId, params),
    queryFn: ({ signal }) => crmService.getLeads(entityId, params, { signal }),
    placeholderData: (prev) => prev, // Keeps previous page data while loading next page
    staleTime: 30_000,
  });
}
```

### Invalidation Invariants:
After any mutation (create, update, change status, assign), invalidate the root key for that resource:
```tsx
const queryClient = useQueryClient();
await queryClient.invalidateQueries({ queryKey: crmQueryKeys.leads(entityId) });
```

---

## 4. URL State as the Single Source of Truth

Filters, pagination, tabs, and open detail inspectors must live in URL query params:

```tsx
const [searchParams, setSearchParams] = useSearchParams();

const page = Number(searchParams.get("page") ?? 1);
const search = searchParams.get("search") ?? "";
const status = searchParams.get("status") ?? "ALL";
const selectedId = searchParams.get("id"); // Open detail drawer

const updateFilter = (key: string, value: string | null) => {
  setSearchParams((prev) => {
    const next = new URLSearchParams(prev);
    if (value && value !== "ALL") {
      next.set(key, value);
    } else {
      next.delete(key);
    }
    if (key !== "page") next.set("page", "1"); // Reset page on filter change
    return next;
  }, { replace: key === "search" });
};
```

---

## 5. Forms, Validation & Problem Details (RFC-7807)

TODIR uses `react-hook-form` + `zod` for client validation, mapped to server RFC-7807 Problem Details:

```tsx
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { leadFormSchema, LeadFormValues } from "../validation/lead-schemas";
import { FormField } from "frontend/components/forms/form-field";
import { ValidationSummary } from "frontend/components/forms/validation-summary";
import { TextField } from "frontend/components/primitives/text-field";

export function CreateLeadDialog({ open, onClose, entityId }: Props) {
  const { t } = useTranslation();
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<LeadFormValues>({
    resolver: zodResolver(leadFormSchema),
  });

  const onSubmit = async (values: LeadFormValues) => {
    try {
      await crmService.createLead(entityId, values);
      onClose();
    } catch (err: any) {
      if (err.errors) {
        // Map backend field errors to react-hook-form
        Object.entries(err.errors).forEach(([field, msgs]: [string, any]) => {
          setError(field as any, { message: Array.isArray(msgs) ? msgs[0] : msgs });
        });
      }
    }
  };

  return (
    <Dialog open={open} onClose={onClose} title={t("crm.lead.new")}>
      <form onSubmit={handleSubmit(onSubmit)}>
        <ValidationSummary errors={errors} />
        <FormField label={t("crm.lead.name")} error={errors.displayName?.message} required>
          <TextField {...register("displayName")} />
        </FormField>
        {/* Actions */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "16px" }}>
          <Button variant="outline" onClick={onClose}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t("common.saving") : t("common.save")}</Button>
        </div>
      </form>
    </Dialog>
  );
}
```

---

## 6. Permissions & RBAC Enforcement

1. **Authorization Authority**: Backend is the single authority.
2. **Frontend Grace**: The UI guards actions with the session permissions hook (`usePermissions`):
   ```tsx
   const { hasPermission } = usePermissions();
   const canCreateLead = hasPermission("crm.lead.create");
   const canExport = hasPermission("crm.export");
   ```
3. Never render an actionable button that yields an HTTP 403 Forbidden on click. Hide or disable with clear tooltip explanation.
