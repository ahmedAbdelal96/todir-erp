export const en = {
  title: "Catalog",
  productList: {
    productService: "Product / service", type: "Type", service: "Service", goods: "Goods", category: "Category", brand: "Brand", unit: "Unit",
    status: "Status", active: "Active", archived: "Archived", pageTitle: "Products and services catalog",
    subtitle: "A shared identity for products and services used in sales, purchasing, and inventory",
    breadcrumbCatalog: "Catalog", breadcrumbProducts: "Products and services", add: "Add product or service",
    searchLabel: "Search products", searchPlaceholder: "Search by name, SKU, or barcode...", typeFilter: "Product type",
    allTypes: "All types", goodsFilter: "Goods", servicesFilter: "Services", loadError: "Could not load the product catalog",
    loadErrorDescription: "The products and services could not be retrieved from the server.", emptyTitle: "No products or services yet",
    noResults: "No products match the current search.", emptyDescription: "Add the first product or service to the organization catalog.",
    addDescription: "Quickly create the identity and base unit; commercial details belong to other modules."
  }
  ,productPicker: { label: "Product", loading: "Loading...", placeholder: "Search by product name or code..." },
  productForm: {
    identity: "Core identity", identityDescription: "The catalog owns product identity; pricing, tax, and inventory are managed by other modules.",
    name: "Product / service name", nature: "Type", goods: "Product / goods", service: "Service", sku: "SKU (optional)", nameAr: "Arabic name", nameEn: "English name",
    classification: "Classification and unit", serviceUnitDescription: "A service uses a unit such as an hour or visit without implying inventory.",
    baseUnitDescription: "The base unit defines the quantity used by downstream modules.", category: "Category", noCategory: "No category",
    brand: "Brand", noBrand: "No brand", baseUnit: "Base unit", chooseUnit: "Choose a unit",
    save: "Save product", cancel: "Cancel", nameRequired: "Enter the product name", baseUnitRequired: "Choose a base unit"
  },
  productRecord: {
    loading: "Loading product details...", notFound: "The requested product details could not be found.", backToCatalog: "Back to catalog",
    nature: "Item type", serviceNonStock: "Non-stock service", goodsInventory: "Goods and inventory", noInventoryTracking: "Does not require inventory balance tracking",
    inventoryMovements: "Subject to warehouse movements and stock counts", productStatus: "Product status", activeForSale: "Active and available for sale", archived: "Archived",
    availableForTransactions: "Available on invoices and purchase orders", stoppedForNewTransactions: "Unavailable for new transactions", baseUnit: "Base unit of measure",
    tradingUnit: "Unit used for pricing and trading", barcodes: "Registered barcodes", barcodeCount: "{{count}} barcodes", none: "None",
    scanCodes: "Associated scan codes", edit: "Edit product", archive: "Archive item", unarchive: "Restore item", back: "Back",
    identity: "Identification and naming", identityDescription: "Commercial names and item identifiers", displayName: "Primary display name",
    sku: "Stock keeping unit (SKU)", itemType: "Item type", itemStatus: "Item status", stockGoods: "Stocked goods", active: "Active and available", classification: "Classification and brand",
    classificationDescription: "Product category, manufacturer, and units of measure", uncategorized: "Uncategorized", unitOfMeasure: "Unit of measure (UoM)",
    linkedBarcodes: "Linked barcodes", barcodeDescription: "Codes used at points of sale and barcode scanners", noBarcodes: "No barcode has been assigned to this item yet."
  }
} as const;
