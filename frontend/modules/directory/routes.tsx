import { lazy } from "react";
import { useNavigate, useParams } from "react-router-dom";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";

const PartyListPage = lazy(() => import("./pages/party-list-page.tsx").then(module => ({ default: module.PartyListPage })));
const PartyRecordPage = lazy(() => import("./pages/party-record-page.tsx").then(module => ({ default: module.PartyRecordPage })));

function DirectoryListRoute(): React.JSX.Element {
  const navigate = useNavigate();
  return <PartyListPage onSelectParty={(party) => navigate(`/directory/${encodeURIComponent(party.id)}`)} />;
}

function DirectoryRecordRoute(): React.JSX.Element {
  const navigate = useNavigate();
  const { partyId = "" } = useParams();
  return <PartyRecordPage partyId={partyId} onBack={() => navigate("/directory")} />;
}

export const directoryRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "directory",
    moduleIds: ["directory"],
    path: "/directory",
    navigationKey: "directory",
    permission: "directory.party.read",
    icon: "directory"
  },
  routes: [
    { path: "directory", element: <DirectoryListRoute /> },
    { path: "directory/:partyId", element: <DirectoryRecordRoute /> }
  ]
};
