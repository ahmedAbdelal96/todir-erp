import { lazy } from "react";
import { useNavigate, useParams } from "react-router-dom";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";
import type { UserAccountSummary } from "./types.ts";

const UserListPage = lazy(() => import("./pages/user-list-page.tsx").then(module => ({ default: module.UserListPage })));
const UserRecordPage = lazy(() => import("./pages/user-record-page.tsx").then(module => ({ default: module.UserRecordPage })));
const LoginDomainSettingsPage = lazy(() => import("./pages/login-domain-settings-page.tsx").then(module => ({ default: module.LoginDomainSettingsPage })));

function UserListRoute(): React.JSX.Element {
  const navigate = useNavigate();
  return <UserListPage onSelectUser={(user: UserAccountSummary) => navigate(`/users/${encodeURIComponent(user.id)}`)} />;
}

function UserRecordRoute(): React.JSX.Element {
  const navigate = useNavigate();
  const { userId = "" } = useParams();
  return <UserRecordPage userId={userId} onBack={() => navigate("/users")} />;
}

export const identityRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "users",
    moduleIds: ["users", "iam"],
    path: "/users",
    label: { ar: "المستخدمون", en: "Users" },
    permission: "platform.identity.read",
    icon: "users"
  },
  routes: [
    { path: "users", element: <UserListRoute /> },
    { path: "users/:userId", element: <UserRecordRoute /> },
    { path: "domain-settings", element: <LoginDomainSettingsPage /> }
  ]
};
