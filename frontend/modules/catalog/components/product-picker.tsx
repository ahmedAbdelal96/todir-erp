import React, { useState, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { ComboBox, type ComboBoxOption } from "../../../components/primitives/combobox.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../components/primitives/table.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { SearchIcon, CheckIcon } from "../../../components/primitives/icons.tsx";
import { catalogApi } from "../services/catalog-api.ts";
import { useProductList } from "../hooks/catalog-queries.ts";
import { QuickCreateProductDialog } from "../features/quick-create-product-dialog.tsx";
import type { ProductSummary, ProductVariantSnapshot } from "../types.ts";

export interface ProductPickerSelection {
  readonly variantId: string;
  readonly templateId: string;
  readonly displayName: string;
  readonly sku?: string | null | undefined;
  readonly baseUomCode?: string | null | undefined;
  readonly isStocked?: boolean | undefined;
}

export interface ProductPickerProps {
  readonly value?: string | undefined; // variantId
  readonly onChange?: ((selection: ProductPickerSelection) => void) | undefined;
  readonly label?: string | undefined;
  readonly placeholder?: string | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly isRequired?: boolean | undefined;
  readonly enableQuickCreate?: boolean | undefined;
}

export function ProductPicker({
  value,
  onChange,
  label,
  placeholder,
  isDisabled = false,
  isRequired = false,
  // Transactional forms must select mastered products; creation belongs to Catalog.
  enableQuickCreate = false
}: ProductPickerProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState("");
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [isQuickCreateOpen, setIsQuickCreateOpen] = useState(false);
  const [quickCreateName, setQuickCreateName] = useState("");
  const [dialogSearch, setDialogSearch] = useState("");
  const [dialogPage, setDialogPage] = useState(1);
  const [scannerInput, setScannerInput] = useState("");
  const [resolveError, setResolveError] = useState<string | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearchTerm(searchTerm.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [searchTerm]);

  // Fast dropdown search
  const { data: dropdownData, isLoading: isDropdownLoading } = useProductList({
    search: debouncedSearchTerm || undefined,
    status: "Active",
    pageSize: 20
  });

  // Modal dialog list query
  const { data: dialogData, isLoading: isDialogLoading } = useProductList({
    search: dialogSearch || undefined,
    status: "Active",
    page: dialogPage,
    pageSize: 10
  });

  const products = dropdownData?.items ?? [];
  const options: ComboBoxOption[] = products.map((item) => ({
    id: item.defaultVariantId,
    label: item.displayName,
    description: item.sku ? `${item.sku} · ${item.baseUomCode ?? ""}` : (item.baseUomCode ?? undefined)
  }));

  const handleSelectKey = (key: React.Key | null) => {
    if (!key) return;
    const selected = products.find((p) => p.defaultVariantId === String(key));
    if (selected) {
      onChange?.({
        variantId: selected.defaultVariantId,
        templateId: selected.id,
        displayName: selected.displayName,
        sku: selected.sku,
        baseUomCode: selected.baseUomCode,
        isStocked: selected.nature !== "Service"
      });
    }
  };

  const handleSelectSummary = (summary: ProductSummary) => {
    onChange?.({
      variantId: summary.defaultVariantId,
      templateId: summary.id,
      displayName: summary.displayName,
      sku: summary.sku,
      baseUomCode: summary.baseUomCode,
      isStocked: summary.nature !== "Service"
    });
    setIsDialogOpen(false);
  };

  const handleScanResolve = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!scannerInput.trim()) return;
    setResolveError(null);
    try {
      const resolved = await catalogApi.resolve(scannerInput.trim());
      onChange?.({
        variantId: resolved.variantId,
        templateId: resolved.templateId,
        displayName: resolved.displayName,
        sku: resolved.sku,
        baseUomCode: resolved.resolvedUomCode || undefined,
        isStocked: true
      });
      setScannerInput("");
      setIsDialogOpen(false);
    } catch {
      setResolveError(t("productPicker.scanNotFound"));
    }
  };

  return (
    <div style={{ display: "flex", gap: "0.5rem", alignItems: "flex-end", width: "100%" }}>
      <div style={{ flex: 1 }}>
        <ComboBox
          label={label ?? t("productPicker.label")}
          selectedKey={value ?? null}
          onSelectionChange={handleSelectKey}
          onInputChange={setSearchTerm}
          options={options}
          placeholder={isDropdownLoading ? t("productPicker.loading") : (placeholder ?? t("productPicker.placeholder"))}
          noResultsMessage={t("productList.noResults")}
          isDisabled={isDisabled}
          isRequired={isRequired}
          onCreateNew={enableQuickCreate && !isDisabled ? (term) => {
            setQuickCreateName(term);
            setIsQuickCreateOpen(true);
          } : undefined}
          createActionLabel={(term) => term ? t("productPicker.createWithName", { name: term }) : t("productPicker.createNewProduct")}
        />
      </div>

      <Button
        variant="secondary"
        onPress={() => setIsDialogOpen(true)}
        aria-label={t("productPicker.openDialogAria")}
        isDisabled={isDisabled}
        style={{ marginBottom: "2px", minWidth: "40px", padding: "0 0.5rem" }}
      >
        <SearchIcon size={16} />
      </Button>

      {/* Rich Product Picker Dialog */}
      <Dialog
        isOpen={isDialogOpen}
        onOpenChange={setIsDialogOpen}
        title={t("productPicker.dialogTitle")}
        size="large"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {/* Scanner Input Header */}
          <form onSubmit={handleScanResolve} style={{ display: "flex", gap: "0.5rem" }}>
            <SearchField
              ariaLabel={t("productPicker.scanInputLabel")}
              placeholder={t("productPicker.scanPlaceholder")}
              value={scannerInput}
              onChange={setScannerInput}
            />
            <Button variant="secondary" type="submit" style={{ alignSelf: "flex-end", marginBottom: "2px" }}>
              {t("productPicker.scanButton")}
            </Button>
          </form>

          {resolveError ? (
            <p style={{ color: "var(--color-status-danger)", fontSize: "0.8125rem", margin: 0 }}>
              {resolveError}
            </p>
          ) : null}

          {/* Search Box */}
          <SearchField
            ariaLabel={t("productPicker.searchFilterLabel")}
            placeholder={t("productPicker.dialogSearchPlaceholder")}
            value={dialogSearch}
            onChange={(val) => {
              setDialogSearch(val);
              setDialogPage(1);
            }}
          />

          {/* Table of results */}
          <div style={{ border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)", maxHeight: "300px", overflowY: "auto" }}>
            <Table aria-label={t("productPicker.dialogTitle")}>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("common.name")}</TableHead>
                  <TableHead>{t("common.sku")}</TableHead>
                  <TableHead>{t("common.type")}</TableHead>
                  <TableHead>{t("common.unit")}</TableHead>
                  <TableHead>{t("common.status")}</TableHead>
                  <TableHead>{t("common.action")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(dialogData?.items ?? []).map((item) => (
                  <TableRow key={item.defaultVariantId}>
                    <TableCell style={{ fontWeight: 600 }}>{item.displayName}</TableCell>
                    <TableCell>
                      <bdi dir="ltr" style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem" }}>
                        {item.sku || "—"}
                      </bdi>
                    </TableCell>
                    <TableCell>{item.nature === "Service" ? t("common.service") : t("common.goods")}</TableCell>
                    <TableCell>{item.baseUomCode || "—"}</TableCell>
                    <TableCell>
                      <StatusBadge status={item.status} label={item.status === "Active" ? t("common.active") : t("common.archived")} />
                    </TableCell>
                    <TableCell>
                      <Button
                        variant={item.defaultVariantId === value ? "primary" : "secondary"}
                        size="compact"
                        onPress={() => handleSelectSummary(item)}
                      >
                        {item.defaultVariantId === value ? <CheckIcon size={14} /> : t("productPicker.selectAction")}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
              {t("productPicker.totalCount", { count: dialogData?.totalCount ?? 0 })}
            </span>
            <div style={{ display: "flex", gap: "0.5rem" }}>
              <Button
                variant="secondary"
                size="compact"
                isDisabled={dialogPage <= 1}
                onPress={() => setDialogPage((p) => Math.max(1, p - 1))}
              >
                {t("common.previous")}
              </Button>
              <Button
                variant="secondary"
                size="compact"
                isDisabled={(dialogData?.items.length ?? 0) < 10}
                onPress={() => setDialogPage((p) => p + 1)}
              >
                {t("common.next")}
              </Button>
            </div>
          </div>
        </div>
      </Dialog>

      {enableQuickCreate && (
        <QuickCreateProductDialog
          isOpen={isQuickCreateOpen}
          onOpenChange={setIsQuickCreateOpen}
          initialDisplayName={quickCreateName || searchTerm}
          onCreated={(created) => {
            onChange?.({
              variantId: created.defaultVariantId,
              templateId: created.id,
              displayName: created.displayName,
              sku: created.sku,
              baseUomCode: created.baseUomCode,
              isStocked: created.nature !== "Service"
            });
          }}
        />
      )}
    </div>
  );
}
