import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router-dom";
import { PageHeader } from "../../../components/pages/page-header.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { CursorPagination } from "../../../components/primitives/cursor-pagination.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { FinancialStatusBadge } from "../components/financial-status-badge.tsx";
import { OpenItemDetailDrawer } from "../components/open-item-detail-drawer.tsx";
import { WriteOffDialog } from "../components/write-off-dialog.tsx";
import { ReversalDialog } from "../components/reversal-dialog.tsx";
import { useSubledgerScope, useOpenItemsQuery } from "../hooks/subledger-queries.ts";
import type { OpenItemListFilter, PartyOpenItem, SubledgerDirection } from "../types.ts";

export function OpenItemsPage(): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const navigate = useNavigate();
  const { legalEntityId } = useSubledgerScope();

  // Filters state
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [directionFilter, setDirectionFilter] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<string>("openOnly");
  const [currencyFilter, setCurrencyFilter] = useState<string>("");
  const [cursor, setCursor] = useState<string | undefined>();
  const [cursorHistory, setCursorHistory] = useState<readonly (string | undefined)[]>([]);
  const [pageSize, setPageSize] = useState(50);

  // Modals & Drawers state
  const [selectedDetailItem, setSelectedDetailItem] = useState<PartyOpenItem | null>(null);
  const [selectedWriteOffItem, setSelectedWriteOffItem] = useState<PartyOpenItem | null>(null);
  const [selectedReversal, setSelectedReversal] = useState<{ applicationId: string; openItemId: string } | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const queryFilter = useMemo<OpenItemListFilter>(() => {
    const f: {
      legalEntityId: string;
      limit: number;
      cursor?: string | undefined;
      direction?: SubledgerDirection;
      openOnly?: boolean;
      currencyCode?: string;
      search?: string;
      overdueOnly?: boolean;
      asOfDate?: string;
    } = {
      legalEntityId,
      limit: pageSize,
      cursor,
    };

    if (directionFilter === "receivable") {
      f.direction = "Receivable";
    } else if (directionFilter === "payable") {
      f.direction = "Payable";
    }

    if (statusFilter === "openOnly") {
      f.openOnly = true;
    } else if (statusFilter === "overdueOnly") {
      f.openOnly = true;
      f.overdueOnly = true;
      f.asOfDate = new Date().toISOString().slice(0, 10);
    } else {
      f.openOnly = false;
    }

    if (currencyFilter) {
      f.currencyCode = currencyFilter;
    }

    if (searchTerm.trim()) f.search = searchTerm.trim();

    return f;
  }, [legalEntityId, directionFilter, statusFilter, currencyFilter, searchTerm, cursor, pageSize]);

  const openItemsQuery = useOpenItemsQuery(queryFilter);

  const rawItems = openItemsQuery.data?.items ?? [];
  const isLoading = openItemsQuery.isLoading;
  const resetCursor = () => { setCursor(undefined); setCursorHistory([]); };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("openItems.title")}
        description={t("openItems.subtitle")}
        secondaryActions={
          <div className="flex items-center gap-2">
            <Link to="/subledger/applications">
              <Button variant="primary" size="compact" className="bg-slate-900 hover:bg-slate-800 text-white">
                {t("application.title")}
              </Button>
            </Link>
          </div>
        }
      />

      {successMessage && (
        <Alert variant="success">
          {successMessage}
        </Alert>
      )}

      {/* Filter Surface */}
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
          {/* Search Box */}
          <div>
            <label htmlFor="open-item-search" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("openItemsPage.searchLabel")}
            </label>
            <input
              id="open-item-search"
              type="text"
              placeholder={t("openItems.searchPlaceholder")}
              value={searchTerm}
              onChange={(e) => { setSearchTerm(e.target.value); resetCursor(); }}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
            />
          </div>

          {/* Direction Filter */}
          <div>
            <label htmlFor="open-item-dir" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("openItems.filterDirection")}
            </label>
            <select
              id="open-item-dir"
              value={directionFilter}
              onChange={(e) => { setDirectionFilter(e.target.value); resetCursor(); }}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
            >
              <option value="all">{t("openItems.allDirections")}</option>
              <option value="receivable">{t("openItems.receivableOnly")}</option>
              <option value="payable">{t("openItems.payableOnly")}</option>
            </select>
          </div>

          {/* Status Filter */}
          <div>
            <label htmlFor="open-item-status" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("openItems.filterStatus")}
            </label>
            <select
              id="open-item-status"
              value={statusFilter}
              onChange={(e) => { setStatusFilter(e.target.value); resetCursor(); }}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
            >
              <option value="openOnly">{t("openItems.openOnly")}</option>
              <option value="overdueOnly">{t("openItems.overdueOnly")}</option>
              <option value="all">{t("openItems.allStatuses")}</option>
            </select>
          </div>

          {/* Currency Filter */}
          <div>
            <label htmlFor="open-item-currency" className="text-xs font-semibold text-slate-700 block mb-1">
              {t("openItems.currency")}
            </label>
            <input
              id="open-item-currency"
              type="text"
              placeholder={t("common.currencyPlaceholder")}
              value={currencyFilter}
              onChange={(e) => { setCurrencyFilter(e.target.value.toUpperCase()); resetCursor(); }}
              className="w-full px-3 py-1.5 text-xs font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
            />
          </div>
        </div>
      </Surface>

      {/* High-Density Data Table */}
      <Surface variant="card" padding="none" className="bg-white border border-slate-200 rounded-lg shadow-sm overflow-hidden">
        <div className="px-5 py-3 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <span className="text-xs font-bold text-slate-700">
            {t("openItemsPage.totalDisplayed")} {rawItems.length}
          </span>
          <Button
            variant="secondary"
            size="compact"
            onClick={() => void openItemsQuery.refetch()}
            isDisabled={isLoading}
            className="border-slate-300 text-xs text-slate-700 hover:bg-slate-100"
          >
            {t("common.refresh")}
          </Button>
        </div>

        {isLoading ? (
          <div className="p-8 text-center text-slate-500 text-sm">
            {t("openItems.loadingText")}
          </div>
        ) : rawItems.length === 0 ? (
          <div className="p-10 text-center space-y-2">
            <h4 className="text-sm font-bold text-slate-800">{t("openItems.emptyTitle")}</h4>
            <p className="text-xs text-slate-500">{t("openItems.emptyDescription")}</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-xs text-start">
              <thead className="bg-slate-100 text-slate-700 font-semibold border-b border-slate-200">
                <tr>
                  <th className="px-4 py-3 text-start">{t("openItems.documentRef")}</th>
                  <th className="px-4 py-3 text-start">{t("drawer.sourceType")}</th>
                  <th className="px-4 py-3 text-start">{t("openItems.documentDate")}</th>
                  <th className="px-4 py-3 text-start">{t("openItems.dueDate")}</th>
                  <th className="px-4 py-3 text-end">{t("openItems.originalAmount")}</th>
                  <th className="px-4 py-3 text-end">{t("openItems.settledAmount")}</th>
                  <th className="px-4 py-3 text-end font-bold">{t("openItems.remainingAmount")}</th>
                  <th className="px-4 py-3 text-center">{t("openItems.status")}</th>
                  <th className="px-4 py-3 text-center">{t("openItems.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-slate-900 font-medium">
                {rawItems.map((item) => {
                  const remAmt = parseFloat(item.openTransactionAmount || "0");
                  const settledAmt = parseFloat(item.settledTransactionAmount || "0");
                  const isSettled = remAmt <= 0;
                  const isOverdue = new Date(item.dueDate) < new Date() && remAmt > 0;
                  const daysOverdue = Math.max(
                    0,
                    Math.floor((Date.now() - new Date(item.dueDate).getTime()) / (1000 * 60 * 60 * 24))
                  );

                  return (
                    <tr key={item.id} className="hover:bg-slate-50 transition-colors">
                      <td className="px-4 py-3">
                        <span className="font-bold text-slate-900 block">
                          {item.sourceDisplayNumber || item.sourceReference}
                        </span>
                        <span className="text-[10px] text-slate-500 font-mono">
                          {item.sourceReference}
                        </span>
                      </td>

                      <td className="px-4 py-3 text-slate-700 whitespace-nowrap">
                        <span className="font-semibold">{item.sourceType}</span>
                        <span className="text-[10px] text-slate-500 block">
                          {item.polarity === "Debit" ? t("common.debit") : t("common.credit")}
                        </span>
                      </td>

                      <td className="px-4 py-3 text-slate-700 whitespace-nowrap">
                        {item.documentDate}
                      </td>

                      <td className="px-4 py-3 whitespace-nowrap">
                        <span className={isOverdue ? "text-rose-700 font-bold" : "text-slate-700"}>
                          {item.dueDate}
                        </span>
                        {daysOverdue > 0 && (
                          <span className="text-[10px] text-rose-600 block">
                            +{daysOverdue} {t("common.days")}
                          </span>
                        )}
                      </td>

                      <td className="px-4 py-3 text-end text-slate-800">
                        <MoneyDisplay value={{ amount: item.originalTransactionAmount, currency: item.transactionCurrency }} />
                      </td>

                      <td className="px-4 py-3 text-end text-slate-600">
                        <MoneyDisplay value={{ amount: item.settledTransactionAmount, currency: item.transactionCurrency }} />
                      </td>

                      <td className="px-4 py-3 text-end font-bold text-slate-900">
                        <MoneyDisplay value={{ amount: item.openTransactionAmount, currency: item.transactionCurrency }} />
                      </td>

                      <td className="px-4 py-3 text-center">
                        <FinancialStatusBadge
                          status={item.status}
                          isOverdue={isOverdue}
                        />
                      </td>

                      <td className="px-4 py-3 text-center whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          <button
                            type="button"
                            onClick={() => setSelectedDetailItem(item)}
                            className="px-2 py-1 text-[11px] font-semibold text-slate-700 bg-slate-100 hover:bg-slate-200 rounded border border-slate-300"
                          >
                            {t("openItems.viewDetails")}
                          </button>

                          {!isSettled && (
                            <button
                              type="button"
                              onClick={() => {
                                navigate(
                                  `/subledger/applications?partyId=${item.partyId}&direction=${item.direction}&sourceId=${item.id}`
                                );
                              }}
                              className="px-2 py-1 text-[11px] font-semibold text-emerald-800 bg-emerald-50 hover:bg-emerald-100 rounded border border-emerald-300"
                            >
                              {t("openItems.applyNow")}
                            </button>
                          )}

                          {!isSettled && (
                            <button
                              type="button"
                              onClick={() => setSelectedWriteOffItem(item)}
                              className="px-2 py-1 text-[11px] font-semibold text-rose-800 bg-rose-50 hover:bg-rose-100 rounded border border-rose-300"
                            >
                              {t("openItems.writeOff")}
                            </button>
                          )}

                          {settledAmt > 0 && (
                            <button
                              type="button"
                              onClick={() =>
                                setSelectedReversal({
                                  applicationId: `app-${item.id}`,
                                  openItemId: item.id
                                })
                              }
                              className="px-2 py-1 text-[11px] font-semibold text-amber-800 bg-amber-50 hover:bg-amber-100 rounded border border-amber-300"
                            >
                              {t("reversal.title")}
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {!isLoading && (
          <div className="px-5">
            <CursorPagination
              page={cursorHistory.length + 1}
              itemCount={rawItems.length}
              pageSize={pageSize}
              hasPrevious={cursorHistory.length > 0}
              hasNext={Boolean(openItemsQuery.data?.nextCursor)}
              onPrevious={() => {
                if (cursorHistory.length === 0) return;
                const nextHistory = cursorHistory.slice(0, -1);
                setCursorHistory(nextHistory);
                setCursor(nextHistory[nextHistory.length - 1]);
              }}
              onNext={() => {
                if (!openItemsQuery.data?.nextCursor) return;
                setCursorHistory((history) => [...history, cursor]);
                setCursor(openItemsQuery.data.nextCursor ?? undefined);
              }}
              onPageSizeChange={(size) => { setPageSize(size); resetCursor(); }}
              isDisabled={openItemsQuery.isFetching}
            />
          </div>
        )}
      </Surface>

      {/* Item Detail Drawer */}
      {selectedDetailItem && (
        <OpenItemDetailDrawer
          item={selectedDetailItem}
          isOpen={Boolean(selectedDetailItem)}
          onClose={() => setSelectedDetailItem(null)}
          onTriggerReversal={(applicationId) => {
            setSelectedReversal({
              applicationId,
              openItemId: selectedDetailItem.id
            });
            setSelectedDetailItem(null);
          }}
          onTriggerWriteOff={() => {
            setSelectedWriteOffItem(selectedDetailItem);
            setSelectedDetailItem(null);
          }}
        />
      )}

      {/* Write-Off Dialog */}
      {selectedWriteOffItem && (
        <WriteOffDialog
          item={selectedWriteOffItem}
          legalEntityId={legalEntityId}
          isOpen={Boolean(selectedWriteOffItem)}
          onClose={() => setSelectedWriteOffItem(null)}
          onCompleted={() => {
            setSelectedWriteOffItem(null);
            setSuccessMessage(t("common.saved", "تم الحفظ بنجاح"));
            void openItemsQuery.refetch();
          }}
        />
      )}

      {/* Reversal Dialog */}
      {selectedReversal && (
        <ReversalDialog
          applicationId={selectedReversal.applicationId}
          legalEntityId={legalEntityId}
          isOpen={Boolean(selectedReversal)}
          onClose={() => setSelectedReversal(null)}
          onCompleted={() => {
            setSelectedReversal(null);
            setSuccessMessage(t("reversal.successMessage"));
            void openItemsQuery.refetch();
          }}
        />
      )}
    </div>
  );
}
