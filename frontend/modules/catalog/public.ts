export { ProductPicker } from "./components/product-picker.tsx";
export type { ProductPickerProps, ProductPickerSelection } from "./components/product-picker.tsx";
export type {
  ProductSummary,
  ProductDetails,
  ProductNature,
  CatalogStatus,
  UomDimension,
  Uom,
  ProductUom,
  Barcode,
  BarcodeType,
  ProductReference,
  ProductReadiness,
  CatalogAttribute,
  CatalogAttributeValue,
  TemplateAttribute,
  ProductVariantSnapshot,
  ResolvedVariantIdentity,
  ProductVisibility,
  CatalogImportSummary
} from "./types.ts";
export { catalogApi } from "./services/catalog-api.ts";
export { useProductUoms } from "./hooks/catalog-queries.ts";
export { catalogRoutes } from "./routes.tsx";
export { ar } from "./i18n/ar.ts";
export { en } from "./i18n/en.ts";
