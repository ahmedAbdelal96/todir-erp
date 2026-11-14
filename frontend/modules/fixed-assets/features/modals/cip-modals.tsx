import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetCategoriesQuery,
  useCapitalizeCipMutation,
  useSplitCipMutation,
  useCombineCipMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type { CipProjectDto } from "../../types.ts";

export interface CapitalizeCipModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly project: CipProjectDto | null;
}

export function CapitalizeCipModal({
  isOpen,
  onClose,
  legalEntityId,
  project
}: CapitalizeCipModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const categoriesQuery = useFixedAssetCategoriesQuery(legalEntityId);
  const capitalizeMutation = useCapitalizeCipMutation();

  const [targetAssetName, setTargetAssetName] = useState("");
  const [targetCategoryId, setTargetCategoryId] = useState("");
  const [inServiceDate, setInServiceDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [usefulLifeMonths, setUsefulLifeMonths] = useState("120");
  const [salvageValue, setSalvageValue] = useState("0");
  const [capitalizationAmount, setCapitalizationAmount] = useState(() => String(project?.totalCost ?? ""));
  const [error, setError] = useState<string | null>(null);

  // Update capitalization amount if project changes
  React.useEffect(() => {
    if (project) {
      setCapitalizationAmount(project.totalCost !== undefined && project.totalCost !== null ? String(project.totalCost) : "");
      setTargetAssetName(project.projectName ?? project.name);
    }
  }, [project]);

  const categoryOptions: SelectOption[] = useMemo(() => {
    return (
      categoriesQuery.data?.items
        ?.filter((c) => c.nature !== "CIP")
        ?.map((cat) => ({
          id: cat.id,
          label: `${cat.categoryName} (${cat.nature})`
        })) ?? []
    );
  }, [categoriesQuery.data?.items]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!project) return;
    setError(null);

    const amount = parseFloat(capitalizationAmount);
    if (isNaN(amount) || amount <= 0) {
      setError(t("validation.validAmountRequired", "يجب إدخال مبلغ رسملة صحيح أكبر من الصفر"));
      return;
    }
    if (!targetAssetName.trim()) {
      setError(t("validation.assetNameRequired", "اسم الأصل الناتج مطلوب"));
      return;
    }

    try {
      await capitalizeMutation.mutateAsync({
        assetId: project.assetId,
        request: {
          targetCategoryId: targetCategoryId || categoriesQuery.data?.items?.[0]?.id || "cat-default",
          name: targetAssetName.trim(),
          capitalizationDate: inServiceDate,
          placedInServiceDate: inServiceDate
        }
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل رسملة المشروع");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("cip.capitalizeModalTitle", "رسملة مشروع رأسمالي وتحويله إلى أصل في الخدمة")}
      size="medium"
      data-testid="capitalize-cip-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {project && (
          <div
            style={{
              padding: "0.75rem",
              backgroundColor: "#f8fafc",
              border: "1px solid #cbd5e1",
              borderRadius: "0.375rem",
              fontSize: "0.875rem",
              color: "#334155"
            }}
          >
            <div><strong>{t("cip.project", "المشروع الرأسمالي")}:</strong> {project.projectName ?? project.name} ({project.projectCode ?? project.assetNumber})</div>
            <div style={{ marginTop: "0.25rem", color: "#64748b" }}>
              {t("cip.accumulatedCost", "إجمالي التكاليف المتراكمة")}: <strong>{project.totalCost !== undefined && project.totalCost !== null ? project.totalCost.toLocaleString("ar-EG", { minimumFractionDigits: 2 }) : "—"}</strong>
            </div>
          </div>
        )}

        {error && (
          <div
            data-testid="capitalize-cip-error"
            style={{
              padding: "0.75rem",
              borderRadius: "0.375rem",
              backgroundColor: "#fef2f2",
              border: "1px solid #f87171",
              color: "#991b1b",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <TextField
          label={t("fields.targetAssetName", "اسم الأصل الرأسمالي المشغل")}
          value={targetAssetName}
          onChange={setTargetAssetName}
          isRequired
          data-testid="target-asset-name-input"
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.25rem", color: "#334155" }}>
            {t("fields.targetCategory", "التصنيف المحاسبي للأصل المشغل")}
          </label>
          <Select
            options={categoryOptions}
            selectedKey={targetCategoryId || null}
            onSelectionChange={(k) => setTargetCategoryId(String(k ?? ""))}
            placeholder={t("fields.selectCategory", "اختر التصنيف...")}
            data-testid="target-category-select"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.capitalizationAmount", "مبلغ الرسملة المحول للأصل")}
            type="number"
            value={capitalizationAmount}
            onChange={setCapitalizationAmount}
            isRequired
            data-testid="capitalization-amount-input"
          />
          <TextField
            label={t("fields.inServiceDate", "تاريخ دخول الخدمة والبدء في الإهلاك")}
            type="date"
            value={inServiceDate}
            onChange={setInServiceDate}
            isRequired
            data-testid="in-service-date-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.usefulLifeMonths", "العمر الإنتاجي (أشهر)")}
            type="number"
            value={usefulLifeMonths}
            onChange={setUsefulLifeMonths}
            data-testid="useful-life-input"
          />
          <TextField
            label={t("fields.salvageValue", "قيمة الخردة المقدرة")}
            type="number"
            value={salvageValue}
            onChange={setSalvageValue}
            data-testid="salvage-value-input"
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={capitalizeMutation.isPending} data-testid="submit-capitalize-btn">
            {t("cip.confirmCapitalization", "تأكيد الرسملة وترحيل قيد اليومية")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface SplitCipModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly project: CipProjectDto | null;
}

export function SplitCipModal({
  isOpen,
  onClose,
  legalEntityId,
  project
}: SplitCipModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const categoriesQuery = useFixedAssetCategoriesQuery(legalEntityId);
  const splitMutation = useSplitCipMutation();

  const [portion1Name, setPortion1Name] = useState("");
  const [portion1Amount, setPortion1Amount] = useState("");
  const [portion2Name, setPortion2Name] = useState("");
  const [portion2Amount, setPortion2Amount] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!project) return;
    setError(null);

    const a1 = parseFloat(portion1Amount) || 0;
    const a2 = parseFloat(portion2Amount) || 0;
    if (a1 <= 0 || a2 <= 0) {
      setError(t("validation.validSplitAmountsRequired", "يجب إدخال مبالغ صحيحة للجزئين"));
      return;
    }

    try {
      const targetCatId = categoriesQuery.data?.items?.[0]?.id || "cat-default";
      await splitMutation.mutateAsync({
        assetId: project.assetId,
        request: {
          idempotencyKey: `split-${project.assetId}-${Date.now()}`,
          targets: [
            {
              targetCategoryId: targetCatId,
              name: portion1Name.trim(),
              amount: a1,
              quantity: 1,
              capitalizationDate: new Date().toISOString().slice(0, 10)
            },
            {
              targetCategoryId: targetCatId,
              name: portion2Name.trim(),
              amount: a2,
              quantity: 1,
              capitalizationDate: new Date().toISOString().slice(0, 10)
            }
          ]
        }
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل تقسيم المشروع");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("cip.splitModalTitle", "تقسيم مشروع رأسمالي قيد التنفيذ (CIP Split)")}
      size="medium"
      data-testid="split-cip-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {project && (
          <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", borderRadius: "0.375rem" }}>
            <div><strong>{t("cip.sourceProject", "المشروع الأصلي")}:</strong> {project.projectName ?? project.name}</div>
            <div>{t("cip.totalCost", "إجمالي التكلفة")}: {project.totalCost !== undefined && project.totalCost !== null ? project.totalCost.toLocaleString("ar-EG") : "—"}</div>
          </div>
        )}

        {error && (
          <div data-testid="split-cip-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("fields.portion1Name", "اسم الجزء الأول")}
            value={portion1Name}
            onChange={setPortion1Name}
            isRequired
            data-testid="portion1-name-input"
          />
          <TextField
            label={t("fields.portion1Amount", "المبلغ المخصص 1")}
            type="number"
            value={portion1Amount}
            onChange={setPortion1Amount}
            isRequired
            data-testid="portion1-amount-input"
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("fields.portion2Name", "اسم الجزء الثاني")}
            value={portion2Name}
            onChange={setPortion2Name}
            isRequired
            data-testid="portion2-name-input"
          />
          <TextField
            label={t("fields.portion2Amount", "المبلغ المخصص 2")}
            type="number"
            value={portion2Amount}
            onChange={setPortion2Amount}
            isRequired
            data-testid="portion2-amount-input"
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={splitMutation.isPending} data-testid="submit-split-btn">
            {t("cip.confirmSplit", "تأكيد تقسيم المشروع")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface CombineCipModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly availableProjects: readonly CipProjectDto[];
}

export function CombineCipModal({
  isOpen,
  onClose,
  legalEntityId,
  availableProjects
}: CombineCipModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const categoriesQuery = useFixedAssetCategoriesQuery(legalEntityId);
  const combineMutation = useCombineCipMutation();

  const [combinedProjectName, setCombinedProjectName] = useState("");
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((i) => i !== id) : [...prev, id]
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (selectedIds.length < 2) {
      setError(t("validation.atLeastTwoProjectsRequired", "يجب اختيار مشروعين على الأقل للدمج"));
      return;
    }
    if (!combinedProjectName.trim()) {
      setError(t("validation.projectNameRequired", "اسم المشروع المدمج مطلوب"));
      return;
    }

    try {
      const targetCatId = categoriesQuery.data?.items?.[0]?.id || "cat-default";
      await combineMutation.mutateAsync({
        legalEntityId,
        targetCategoryId: targetCatId,
        name: combinedProjectName.trim(),
        capitalizationDate: new Date().toISOString().slice(0, 10),
        idempotencyKey: `combine-${Date.now()}`,
        sources: selectedIds.map((id) => ({
          assetId: id,
          amount: 1000
        }))
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل دمج المشاريع");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("cip.combineModalTitle", "دمج مشاريع رأسمالية (CIP Combine)")}
      size="medium"
      data-testid="combine-cip-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div data-testid="combine-cip-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <TextField
          label={t("fields.combinedProjectName", "اسم المشروع الرأسمالي الموحد الجديد")}
          value={combinedProjectName}
          onChange={setCombinedProjectName}
          isRequired
          data-testid="combined-project-name-input"
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.5rem", color: "#334155" }}>
            {t("cip.selectProjectsToCombine", "اختر المشاريع المراد دمجها:")}
          </label>
          <div style={{ maxHeight: "200px", overflowY: "auto", border: "1px solid #cbd5e1", borderRadius: "0.375rem", padding: "0.5rem", display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            {availableProjects.map((p) => (
              <label key={p.assetId} style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", cursor: "pointer" }}>
                <input
                  type="checkbox"
                  checked={selectedIds.includes(p.assetId)}
                  onChange={() => toggleSelect(p.assetId)}
                />
                <span>{p.projectName ?? p.name} {p.totalCost !== undefined && p.totalCost !== null ? `(${p.totalCost.toLocaleString("ar-EG")})` : ""}</span>
              </label>
            ))}
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={combineMutation.isPending} data-testid="submit-combine-btn">
            {t("cip.confirmCombine", "تأكيد دمج المشاريع")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
