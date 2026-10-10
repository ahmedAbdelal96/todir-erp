import { lazy } from "react";
import { useNavigate, useParams } from "react-router-dom";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";

const ProductListPage = lazy(() => import("./pages/product-list-page.tsx").then(module => ({ default: module.ProductListPage })));
const ProductRecordPage = lazy(() => import("./pages/product-record-page.tsx").then(module => ({ default: module.ProductRecordPage })));

function CatalogListRoute(): React.JSX.Element {
  const navigate = useNavigate();
  return <ProductListPage onSelectProduct={(product) => navigate(`/catalog/${encodeURIComponent(product.id)}`)} />;
}

function CatalogRecordRoute(): React.JSX.Element {
  const navigate = useNavigate();
  const { productId = "" } = useParams();
  return <ProductRecordPage productId={productId} onBack={() => navigate("/catalog")} />;
}

export const catalogRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "catalog",
    moduleIds: ["catalog"],
    path: "/catalog",
    label: { ar: "الكتالوج", en: "Catalog" },
    permission: "catalog.product.read",
    icon: "catalog"
  },
  routes: [
    { path: "catalog", element: <CatalogListRoute /> },
    { path: "catalog/:productId", element: <CatalogRecordRoute /> }
  ]
};
