import React, { useState, useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  useCreateTransfer,
  useTreasuryAccounts
} from "../../hooks/use-banking-queries.ts";
import { useEntityCurrencies } from "../../../finbase/public.ts";
import type { EntityCurrencyDto } from "../../../finbase/public.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";

interface Props {
  readonly legalEntityId: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function NewTransferModal({ legalEntityId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateTransfer(legalEntityId);

  const [accountSearch, setAccountSearch] = useState("");
  const trimmedSearch = accountSearch.trim();
  const accountQueryFilters = useMemo(() => {
    return trimmedSearch
      ? { pageSize: 100, search: trimmedSearch }
      : { pageSize: 100 };
  }, [trimmedSearch]);
  const { data: accountsData } = useTreasuryAccounts(legalEntityId, accountQueryFilters);
  const allAccounts = accountsData?.items ?? [];
  const activeAccounts = allAccounts.filter((a) => a.status === "Active");

  const { data: entityCurrenciesData } = useEntityCurrencies(legalEntityId);
  const availableCurrencies = useMemo(() => {
    const list: string[] = [];
    if (entityCurrenciesData && entityCurrenciesData.length > 0) {
      entityCurrenciesData.forEach((c: EntityCurrencyDto) => {
        if (c.currencyCode && !list.includes(c.currencyCode)) {
          list.push(c.currencyCode);
        }
      });
    }
    allAccounts.forEach((acc) => {
      if (acc.currencyCode && !list.includes(acc.currencyCode)) {
        list.push(acc.currencyCode);
      }
    });
    if (list.length === 0) list.push("EGP", "USD", "EUR", "SAR");
    return list;
  }, [entityCurrenciesData, allAccounts]);

  const [sourceAccountId, setSourceAccountId] = useState("");
  const [destAccountId, setDestAccountId] = useState("");
  const [amount, setAmount] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");

  // Idempotency key generated once per modal session
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID());

  useEffect(() => {
    if (isOpen) {
      setIdempotencyKey(crypto.randomUUID());
      setErrorMsg(null);
      setAccountSearch("");
    }
  }, [isOpen]);

  const today = new Date().toISOString().slice(0, 10);
  const [paymentDate, setPaymentDate] = useState(today);
  const [postingDate, setPostingDate] = useState(today);
  const [reference, setReference] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const isSameAccount = Boolean(sourceAccountId && destAccountId && sourceAccountId === destAccountId);
  const parsedAmount = parseFloat(amount) || 0;
  const isAmountValid = parsedAmount > 0;

  const handleSourceAccountChange = (key: string) => {
    setSourceAccountId(key);
    const src = allAccounts.find((a) => a.id === key);
    if (src?.currencyCode) {
      setCurrencyCode(src.currencyCode);
    }
  };

  const destinationOptions = activeAccounts
    .filter((acc) => acc.id !== sourceAccountId)
    .map((acc) => {
      const curr = acc.currencyCode ? ` - ${acc.currencyCode}` : "";
      return {
        id: acc.id,
        label: `${acc.name} (${acc.code})${curr}`
      };
    });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!sourceAccountId || !destAccountId) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    if (sourceAccountId === destAccountId) {
      setErrorMsg(t("transfers.sameAccountError"));
      return;
    }

    if (parsedAmount <= 0) {
      setErrorMsg(t("transfers.amountPositiveError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        request: {
          legalEntityId,
          sourceTreasuryAccountId: sourceAccountId,
          destinationTreasuryAccountId: destAccountId,
          amount: parsedAmount,
          currencyCode: currencyCode.trim() || "EGP",
          paymentDate: paymentDate || today,
          postingDate: postingDate || today,
          reference: reference.trim() || null
        },
        idempotencyKey
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("transfers.newTransferModalTitle")}>
      <form
        onSubmit={handleSubmit}
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
          minWidth: "320px",
          maxWidth: "min(96vw, 540px)"
        }}
      >
        {errorMsg && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              background: "var(--color-surface-danger)",
              color: "var(--color-status-danger)",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        {/* Account search helper for large account lists */}
        {allAccounts.length > 5 && (
          <div>
            <TextField
              label={t("accounts.searchAccounts")}
              value={accountSearch}
              onChange={(val) => setAccountSearch(val)}
              placeholder={t("accounts.searchPlaceholder")}
            />
          </div>
        )}

        <div>
          <Select
            label={t("transfers.sourceAccountLabel")}
            selectedKey={sourceAccountId}
            onSelectionChange={(key) => handleSourceAccountChange(key ? String(key) : "")}
            options={activeAccounts.map((acc) => {
              const curr = acc.currencyCode ? ` - ${acc.currencyCode}` : "";
              return {
                id: acc.id,
                label: `${acc.name} (${acc.code})${curr}`
              };
            })}
            isRequired
          />
        </div>

        <div>
          <Select
            label={t("transfers.destinationAccountLabel")}
            selectedKey={destAccountId}
            onSelectionChange={(key) => setDestAccountId(key ? String(key) : "")}
            options={destinationOptions}
            isRequired
          />
          {isSameAccount && (
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-status-danger)" }}>
              {t("transfers.sameAccountError")}
            </p>
          )}
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("transfers.amountLabel")}
            type="number"
            value={amount}
            onChange={(val) => setAmount(val)}
            isRequired
          />
          <Select
            label={t("transfers.currencyLabel")}
            selectedKey={currencyCode}
            onSelectionChange={(key) => setCurrencyCode(key ? String(key) : "EGP")}
            options={availableCurrencies.map((c) => ({
              id: c,
              label: c
            }))}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("transfers.paymentDateLabel")}
            type="date"
            value={paymentDate}
            onChange={(val) => setPaymentDate(val)}
            isRequired
          />
          <TextField
            label={t("transfers.postingDateLabel")}
            type="date"
            value={postingDate}
            onChange={(val) => setPostingDate(val)}
            isRequired
          />
        </div>

        <TextField
          label={t("transfers.referenceLabel")}
          value={reference}
          onChange={(val) => setReference(val)}
          placeholder={t("transfers.referencePlaceholder")}
        />

        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            gap: "0.75rem",
            marginTop: "1rem",
            borderTop: "1px solid var(--color-border-subtle)",
            paddingTop: "1rem"
          }}
        >
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button
            type="submit"
            variant="primary"
            isDisabled={
              createMutation.isPending ||
              !sourceAccountId ||
              !destAccountId ||
              isSameAccount ||
              !isAmountValid
            }
          >
            {createMutation.isPending ? t("common.loading") : t("transfers.transferSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
