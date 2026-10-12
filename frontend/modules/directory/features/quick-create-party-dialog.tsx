import React, { useState } from "react";
import type { Party, PartyType, PartyRoleCode, CreatePartyInput } from "../types.ts";
import { useCreateParty, useCreatePartyWithOverride } from "../hooks/party-queries.ts";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { useToast } from "../../../components/primitives/toast.tsx";
import { usePermissions } from "../../../core/runtime/runtime-provider.tsx";

export interface QuickCreatePartyDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly defaultRole?: PartyRoleCode | undefined;
  readonly onCreated?: ((party: Party) => void) | undefined;
  readonly onOpenFullForm?: (() => void) | undefined;
}

/**
 * Quick Create Party Dialog — adheres strictly to Section 49 of canonical pack.
 * Fast, progressive-disclosure form designed for sales & purchasing workflows:
 * - Display Name *
 * - Entity Type (Company / Individual)
 * - Phone / Mobile
 * - Country
 * Optional progressive disclosure:
 * - Email
 * - Tax ID / VAT
 * - Roles (Customer / Supplier)
 * Allows duplicate override if user possesses directory.duplicate.override permission.
 */
export function QuickCreatePartyDialog({
  isOpen,
  onOpenChange,
  defaultRole,
  onCreated,
  onOpenFullForm
}: QuickCreatePartyDialogProps): React.JSX.Element {
  const [displayName, setDisplayName] = useState("");
  const [partyType, setPartyType] = useState<PartyType>("Organization");
  const [phone, setPhone] = useState("");
  const [countryCode, setCountryCode] = useState("EG");
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [email, setEmail] = useState("");
  const [taxId, setTaxId] = useState("");
  const [roles, setRoles] = useState<PartyRoleCode[]>(defaultRole ? [defaultRole] : []);
  const [duplicateWarning, setDuplicateWarning] = useState<string | null>(null);

  const { showToast } = useToast();
  let perms;
  try {
    perms = usePermissions();
  } catch {
    perms = null;
  }
  const canOverrideDuplicate = perms?.hasPermission("directory.duplicate.override") ?? true;

  const createParty = useCreateParty();
  const createPartyWithOverride = useCreatePartyWithOverride();

  const resetForm = () => {
    setDisplayName("");
    setPartyType("Organization");
    setPhone("");
    setCountryCode("EG");
    setShowAdvanced(false);
    setEmail("");
    setTaxId("");
    setRoles(defaultRole ? [defaultRole] : []);
    setDuplicateWarning(null);
  };

  const handleClose = (open: boolean) => {
    if (!open) {
      resetForm();
    }
    onOpenChange(open);
  };

  const buildInput = (): CreatePartyInput => ({
    partyType,
    displayName: displayName.trim(),
    countryCode,
    roles,
    contactPoints: [
      phone.trim() ? { type: "Phone" as const, value: phone.trim(), label: "الهاتف الرئيسي", isPrimary: true } : null,
      email.trim() ? { type: "Email" as const, value: email.trim(), label: "البريد الرئيسي", isPrimary: true } : null
    ].filter((p): p is NonNullable<typeof p> => p !== null),
    identifiers: taxId.trim()
      ? [{
          type: "TaxId",
          value: taxId.trim(),
          countryCode,
          isPrimary: true
        }]
      : []
  });

  const handleSubmit = async (override = false) => {
    if (!displayName.trim() || displayName.trim().length < 2) {
      showToast({
        type: "warning",
        title: "بيانات مطلوبة",
        description: "يرجى إدخال اسم الطرف التجاري (حرفان على الأقل)."
      });
      return;
    }

    try {
      const input = buildInput();
      const result = override
        ? await createPartyWithOverride.mutateAsync(input)
        : await createParty.mutateAsync(input);

      showToast({
        type: "success",
        title: "تم إنشاء الطرف التجاري",
        description: `تم حفظ "${result.displayName}" في الدليل بنجاح.`
      });
      handleClose(false);
      onCreated?.(result);
    } catch (err: unknown) {
      const message = (err as Error)?.message || "حدث خطأ أثناء حفظ السجل.";
      if (message.includes("duplicate") || message.includes("409")) {
        setDuplicateWarning("وجد النظام سجلًا قريبًا بنفس بيانات التعريف. راجع السجلات المقترحة قبل إنشاء سجل جديد.");
      } else {
        showToast({
          type: "error",
          title: "تعذر الحفظ",
          description: "تعذر حفظ السجل الآن. راجع البيانات وحاول مرة أخرى."
        });
      }
    }
  };

  const isSubmitting = createParty.isPending || createPartyWithOverride.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title="إضافة طرف تجاري جديد"
      description="تسجيل هوية الطرف بالحد الأدنى من الحقول اللازمة لمتابعة المعاملة الحالية."
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem", paddingTop: "0.25rem" }}>
        {duplicateWarning && (
          <div
            style={{
              padding: "0.875rem 1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-warning-subtle, #fffbeb)",
              border: "1px solid var(--color-status-warning, #f59e0b)",
              display: "flex",
              flexDirection: "column",
              gap: "0.625rem"
            }}
          >
            <span style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--color-status-warning-content, #92400e)" }}>
              تنبيه: احتمال وجود سجل مكرر
            </span>
            <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
              {duplicateWarning}
            </p>
            <div style={{ display: "flex", gap: "0.5rem", marginTop: "0.25rem" }}>
              {canOverrideDuplicate && (
                <Button
                  size="compact"
                  variant="danger"
                  isDisabled={isSubmitting}
                  onPress={() => void handleSubmit(true)}
                >
                  المتابعة والإنشاء على أي حال
                </Button>
              )}
              <Button
                size="compact"
                variant="secondary"
                onPress={() => setDuplicateWarning(null)}
              >
                مراجعة وتعديل البيانات
              </Button>
            </div>
          </div>
        )}

        <TextField
          label="الاسم التجاري / المعروض"
          placeholder="مثال: شركة الأفق أو محمد عبد الله"
          value={displayName}
          onChange={setDisplayName}
          isRequired
          autoFocus
        />

        <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
          <div style={{ flex: "1 1 180px" }}>
            <Select
              label="طبيعة الكيان"
              selectedKey={partyType}
              onSelectionChange={(k) => setPartyType(String(k ?? "Organization") as PartyType)}
            >
              <SelectItem id="Organization" textValue="منشأة / شركة">منشأة / شركة</SelectItem>
              <SelectItem id="Person" textValue="فرد مستقل">فرد مستقل</SelectItem>
            </Select>
          </div>

          <div style={{ flex: "1 1 180px" }}>
            <Select
              label="الدولة الأساسية"
              selectedKey={countryCode}
              onSelectionChange={(k) => setCountryCode(String(k ?? "EG"))}
            >
              <SelectItem id="EG" textValue="جمهورية مصر العربية (EG)">جمهورية مصر العربية (EG)</SelectItem>
              <SelectItem id="SA" textValue="المملكة العربية السعودية (SA)">المملكة العربية السعودية (SA)</SelectItem>
              <SelectItem id="AE" textValue="الإمارات العربية المتحدة (AE)">الإمارات العربية المتحدة (AE)</SelectItem>
            </Select>
          </div>
        </div>

        <TextField
          label="الهاتف للتواصل"
          placeholder="+20 100 000 0000"
          value={phone}
          onChange={setPhone}
        />

        {/* Progressive Disclosure Toggle */}
        <button
          type="button"
          onClick={() => setShowAdvanced(!showAdvanced)}
          style={{
            alignSelf: "flex-start",
            border: 0,
            background: "none",
            color: "var(--color-brand-primary, #4f46e5)",
            fontSize: "0.8125rem",
            fontWeight: 600,
            cursor: "pointer",
            padding: "0.25rem 0",
            display: "flex",
            alignItems: "center",
            gap: "0.375rem"
          }}
        >
          {showAdvanced ? "▲ إخفاء البيانات الإضافية" : "▼ إضافة الرقم الضريبي، البريد، والأدوار"}
        </button>

        {showAdvanced && (
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.875rem",
              padding: "0.875rem 1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-surface-sunken, #f8fafc)",
              border: "1px solid var(--color-border-subtle, #e2e8f0)"
            }}
          >
            <TextField
              label="الرقم الضريبي (VAT / Tax ID)"
              placeholder="300123456700003"
              value={taxId}
              onChange={setTaxId}
            />

            <TextField
              label="البريد الإلكتروني المعتمد"
              placeholder="contact@company.com"
              value={email}
              onChange={setEmail}
            />

            <div>
              <span style={{ fontSize: "0.8125rem", fontWeight: 600, display: "block", marginBottom: "0.5rem" }}>
                الأدوار التجارية (اختياري)
              </span>
              <div style={{ display: "flex", gap: "1.5rem" }}>
                <Checkbox
                  isSelected={roles.includes("Customer")}
                  onChange={() => {
                    setRoles(roles.includes("Customer") ? roles.filter(r => r !== "Customer") : [...roles, "Customer"]);
                  }}
                >
                  <span style={{ fontSize: "0.8125rem" }}>عميل</span>
                </Checkbox>
                <Checkbox
                  isSelected={roles.includes("Supplier")}
                  onChange={() => {
                    setRoles(roles.includes("Supplier") ? roles.filter(r => r !== "Supplier") : [...roles, "Supplier"]);
                  }}
                >
                  <span style={{ fontSize: "0.8125rem" }}>مورد</span>
                </Checkbox>
              </div>
            </div>
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "0.75rem", paddingTop: "0.75rem", borderTop: "1px solid var(--color-border-subtle)" }}>
          {onOpenFullForm ? (
            <button
              type="button"
              onClick={() => {
                handleClose(false);
                onOpenFullForm();
              }}
              style={{
                border: 0,
                background: "none",
                color: "var(--color-content-secondary)",
                fontSize: "0.8125rem",
                cursor: "pointer",
                textDecoration: "underline",
                padding: 0
              }}
            >
              فتح النموذج التفصيلي الكامل
            </button>
          ) : <span />}

          <div style={{ display: "flex", gap: "0.625rem" }}>
            <Button variant="secondary" aria-label="إلغاء التعديل" onPress={() => handleClose(false)}>
              إلغاء
            </Button>
            <Button
              variant="primary"
              isDisabled={isSubmitting || !displayName.trim()}
              onPress={() => void handleSubmit(false)}
            >
              {isSubmitting ? "جارٍ الحفظ..." : "إنشاء ومتابعة"}
            </Button>
          </div>
        </div>
      </div>
    </Dialog>
  );
}
