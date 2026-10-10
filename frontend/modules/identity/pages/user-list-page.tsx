import { useState, useMemo } from "react";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../components/primitives/table.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { useUsersList, useLoginDomain, type UseIdentityContext } from "../hooks/identity-queries.ts";
import { CreateUserDialog } from "../features/create-user-dialog.tsx";
import { AdminPasswordResetDialog } from "../features/admin-password-reset-dialog.tsx";
import { DeactivateUserDialog } from "../features/deactivate-user-dialog.tsx";
import { ReactivateUserDialog } from "../features/reactivate-user-dialog.tsx";
import type { UserAccountSummary } from "../types.ts";

export interface UserListPageProps {
  readonly onSelectUser?: (user: UserAccountSummary) => void;
  readonly context?: UseIdentityContext | undefined;
}

export function UserListPage({ onSelectUser, context }: UserListPageProps): React.JSX.Element {
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");

  // Dialog States
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [resetTargetUser, setResetTargetUser] = useState<UserAccountSummary | null>(null);
  const [deactivateTargetUser, setDeactivateTargetUser] = useState<UserAccountSummary | null>(null);
  const [reactivateTargetUser, setReactivateTargetUser] = useState<UserAccountSummary | null>(null);

  const activeParam = statusFilter === "all" ? undefined : statusFilter === "active";

  const { data: users = [], isLoading, error } = useUsersList(
    {
      search: searchTerm.trim() || undefined,
      active: activeParam
    },
    context
  );

  const { data: domainInfo } = useLoginDomain(context);
  const currentDomain = domainInfo?.loginDomain || "";

  const filteredUsers = useMemo(() => {
    return users.filter((u) => {
      if (!searchTerm) return true;
      const term = searchTerm.toLowerCase();
      return u.name.toLowerCase().includes(term) || u.email.toLowerCase().includes(term);
    });
  }, [users, searchTerm]);

  return (
    <ListPage
      title="المستخدمون"
      subtitle="إدارة حسابات مستخدمي الشركة وصلاحيات الوصول"
      primaryAction={{
        label: "إضافة مستخدم",
        onClick: () => setIsCreateOpen(true)
      }}
      filterBar={
        <FilterBar
          searchInput={
            <SearchField
              ariaLabel="البحث عن مستخدم"
              placeholder="البحث بالاسم أو البريد الإلكتروني..."
              value={searchTerm}
              onChange={setSearchTerm}
            />
          }
          activeFilterCount={statusFilter !== "all" || searchTerm ? 1 : 0}
          onResetFilters={() => {
            setSearchTerm("");
            setStatusFilter("all");
          }}
        >
          <div style={{ minWidth: "160px" }}>
            <Select
              label="حالة الحساب"
              selectedKey={statusFilter}
              onSelectionChange={(key) => setStatusFilter(String(key))}
            >
              <SelectItem id="all" textValue="جميع الحسابات">جميع الحسابات</SelectItem>
              <SelectItem id="active" textValue="الحسابات النشطة">الحسابات النشطة</SelectItem>
              <SelectItem id="suspended" textValue="الحسابات المعطلة">الحسابات المعطلة</SelectItem>
            </Select>
          </div>
        </FilterBar>
      }
    >
      {isLoading ? (
        <div style={{ display: "flex", justifyContent: "center", padding: "4rem" }}>
          <Spinner size="large" aria-label="جارٍ تحميل المستخدمين..." />
        </div>
      ) : error ? (
        <div className="empty-state" style={{ padding: "3rem", textAlign: "center" }}>
          <p style={{ color: "var(--color-status-danger, #ef4444)", fontWeight: 600 }}>
            تعذر تحميل بيانات المستخدمين. يرجى المحاولة مرة أخرى.
          </p>
        </div>
      ) : filteredUsers.length === 0 ? (
        <div
          className="empty-state"
          style={{
            padding: "4rem 2rem",
            textAlign: "center",
            backgroundColor: "var(--color-surface-card, #ffffff)",
            borderRadius: "var(--radius-lg, 0.5rem)",
            border: "1px dashed var(--color-border-default, #cbd5e1)"
          }}
        >
          <div style={{ fontSize: "2rem", marginBottom: "0.5rem" }}>👥</div>
          <h3 style={{ margin: "0 0 0.5rem 0", fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
            لا يوجد مستخدمون حتى الآن
          </h3>
          <p style={{ margin: "0 0 1.5rem 0", color: "var(--color-content-muted, #64748b)", fontSize: "0.875rem" }}>
            {searchTerm || statusFilter !== "all"
              ? "لم يتم العثور على مستخدمين يطابقون معايير البحث."
              : "ابدأ بإضافة مستخدمي الشركة لتزويدهم بحسابات تسجيل الدخول."}
          </p>
          <Button variant="primary" onPress={() => setIsCreateOpen(true)}>
            إضافة مستخدم جديد
          </Button>
        </div>
      ) : (
        <div
          style={{
            backgroundColor: "var(--color-surface-card, #ffffff)",
            borderRadius: "var(--radius-lg, 0.5rem)",
            border: "1px solid var(--color-border-default, #e2e8f0)",
            overflow: "hidden"
          }}
        >
          <Table aria-label="قائمة مستخدمي الشركة">
            <TableHeader>
              <TableRow>
                <TableHead>الاسم</TableHead>
                <TableHead>البريد المستخدم لتسجيل الدخول</TableHead>
                <TableHead>الحالة</TableHead>
                <TableHead>كلمة المرور المؤقتة</TableHead>
                <TableHead>تاريخ الإنشاء</TableHead>
                <TableHead style={{ textAlign: "center" }}>الإجراءات</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredUsers.map((user) => {
                const isActive = user.status === "Active";
                return (
                  <TableRow key={user.id}>
                    <TableCell style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                      {user.name}
                    </TableCell>

                    <TableCell>
                      <bdi dir="ltr" style={{ color: "var(--color-content-secondary, #334155)", fontFamily: "monospace" }}>
                        {user.email}
                      </bdi>
                    </TableCell>

                    <TableCell>
                      <StatusBadge
                        intent={isActive ? "success" : "neutral"}
                        label={isActive ? "نشط" : "معطّل"}
                        size="small"
                      />
                    </TableCell>

                    <TableCell>
                      {user.mustChangePassword ? (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-status-warning, #f59e0b)", fontWeight: 600 }}>
                          مطلوب تغييرها
                        </span>
                      ) : (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                          مُعيّنة
                        </span>
                      )}
                    </TableCell>

                    <TableCell style={{ color: "var(--color-content-muted, #64748b)", fontSize: "0.8125rem" }}>
                      {user.createdAt ? new Date(user.createdAt).toLocaleDateString("ar-EG") : "—"}
                    </TableCell>

                    <TableCell>
                      <div style={{ display: "flex", gap: "0.5rem", justifyContent: "center" }}>
                        {onSelectUser && (
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => onSelectUser(user)}
                          >
                            التفاصيل
                          </Button>
                        )}
                        <Button
                          variant="secondary"
                          size="compact"
                          onPress={() => setResetTargetUser(user)}
                        >
                          إعادة تعيين كلمة المرور
                        </Button>
                        {isActive ? (
                          <Button
                            variant="danger"
                            size="compact"
                            onPress={() => setDeactivateTargetUser(user)}
                          >
                            تعطيل
                          </Button>
                        ) : (
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => setReactivateTargetUser(user)}
                          >
                            إعادة تفعيل
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Feature Dialogs */}
      <CreateUserDialog
        isOpen={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        loginDomain={currentDomain}
        context={context}
      />

      <AdminPasswordResetDialog
        isOpen={Boolean(resetTargetUser)}
        onOpenChange={(open) => !open && setResetTargetUser(null)}
        user={resetTargetUser}
        context={context}
      />

      <DeactivateUserDialog
        isOpen={Boolean(deactivateTargetUser)}
        onOpenChange={(open) => !open && setDeactivateTargetUser(null)}
        user={deactivateTargetUser}
        context={context}
      />

      <ReactivateUserDialog
        isOpen={Boolean(reactivateTargetUser)}
        onOpenChange={(open) => !open && setReactivateTargetUser(null)}
        user={reactivateTargetUser}
        context={context}
      />
    </ListPage>
  );
}
