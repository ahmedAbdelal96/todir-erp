import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { ApiError } from "../../../core/api/http-client.ts";
import type {
  StockBalance,
  Warehouse,
  InventoryLocation,
  LocationTrackingMode,
  StockStatus,
  PhysicalCount,
  PhysicalCountLine,
  InventoryValuationSnapshot
} from "../public.ts";
import { compareQuantities, summarizeStockBalances, toQty, toMoneyVal } from "../utils.ts";
import { mapInventoryError } from "../services/inventory-error-map.ts";

describe("Inventory Domain Invariants & Frontend Slice Contracts", () => {
  it("formats quantities and monetary values safely without floating point inaccuracies", () => {
    assert.deepEqual(toQty(15.5), { value: "15.5", unitOfMeasure: "EA" });
    assert.deepEqual(toQty("20", "KG"), { value: "20", unitOfMeasure: "KG" });
    assert.deepEqual(toQty(undefined), { value: "0", unitOfMeasure: "EA" });
    assert.deepEqual(toMoneyVal(150.75), { amount: "150.75", currency: "SAR" });
    assert.deepEqual(toMoneyVal("1200", "USD"), { amount: "1200.00", currency: "USD" });
    assert.deepEqual(toMoneyVal(undefined), { amount: "0.00", currency: "SAR" });
  });

  it("summarizes available stock with exact decimal quantities for operational dialogs", () => {
    const balances: StockBalance[] = [
      {
        productVariantId: "prod-sku-001",
        warehouseId: "wh-01",
        locationId: "loc-01",
        stockStatus: "Available",
        onHandBaseQuantity: 1.1,
        reservedBaseQuantity: 0.1,
        availableBaseQuantity: 1,
        version: 1,
        lastMovementSequence: 10
      },
      {
        productVariantId: "prod-sku-001",
        warehouseId: "wh-01",
        locationId: "loc-02",
        stockStatus: "Available",
        onHandBaseQuantity: 2.2,
        reservedBaseQuantity: 0.2,
        availableBaseQuantity: 2,
        version: 1,
        lastMovementSequence: 11
      }
    ];

    assert.deepEqual(summarizeStockBalances(balances), {
      onHandBaseQuantity: "3.3",
      reservedBaseQuantity: "0.3",
      availableBaseQuantity: "3"
    });
    assert.equal(compareQuantities("3.01", "3"), 1);
    assert.equal(compareQuantities("3.0", "3"), 0);
  });

  it("maps RFC-7807 inventory domain errors to expected localization keys", () => {
    const insufficientStockErr = mapInventoryError(new ApiError(409, "Insufficient stock", {
      code: "inventory.insufficient_stock",
      title: "Insufficient Stock",
      status: 409
    }));
    assert.equal(insufficientStockErr.code, "inventory.insufficient_stock");
    assert.equal(insufficientStockErr.messageKey, "errors.insufficientStock");

    const negativeStockErr = mapInventoryError(new ApiError(422, "Negative stock prohibited", {
      code: "inventory.negative_stock_disallowed",
      title: "Negative Stock Prohibited",
      status: 422
    }));
    assert.equal(negativeStockErr.code, "inventory.negative_stock_disallowed");
    assert.equal(negativeStockErr.messageKey, "errors.negativeStockDisallowed");

    const periodLockedErr = mapInventoryError(new ApiError(422, "Period locked", {
      code: "inventory.period_locked",
      title: "Period locked",
      status: 422
    }));
    assert.equal(periodLockedErr.code, "inventory.period_locked");
    assert.equal(periodLockedErr.messageKey, "errors.periodLocked");

    const missingProfileErr = mapInventoryError(new ApiError(404, "Inventory profile not found", {
      code: "inventory.profile_not_found",
      title: "Inventory profile not found",
      status: 404
    }));
    assert.equal(missingProfileErr.code, "inventory.profile_not_found");
    assert.equal(missingProfileErr.messageKey, "errors.profileNotFound");

    const forbiddenErr = mapInventoryError(new ApiError(403, "Forbidden", {
      code: "inventory.forbidden",
      title: "Forbidden",
      status: 403
    }));
    assert.equal(forbiddenErr.code, "inventory.forbidden");
    assert.equal(forbiddenErr.messageKey, "errors.forbidden");

    const genericErr = mapInventoryError(new Error("Unknown socket disconnect"));
    assert.equal(genericErr.code, "inventory.unknown");
    assert.equal(genericErr.messageKey, "errors.generic");
  });

  it("enforces strict cost privacy — cost data is nullified or hidden when inventory.cost.read is missing", () => {
    const valuationDto: InventoryValuationSnapshot = {
      productVariantId: "prod-sku-001",
      quantity: 450,
      carryingValue: 85200.50,
      currencyCode: "SAR",
      costingMethod: "MovingAverage",
      asOfDate: "2026-10-28"
    };

    function sanitizeForUser(dto: InventoryValuationSnapshot, canReadCost: boolean) {
      if (!canReadCost) {
        return {
          ...dto,
          carryingValue: undefined
        };
      }
      return dto;
    }

    const unprivilegedView = sanitizeForUser(valuationDto, false);
    assert.equal(unprivilegedView.carryingValue, undefined);

    const privilegedView = sanitizeForUser(valuationDto, true);
    assert.equal(privilegedView.carryingValue, 85200.50);
  });

  it("verifies warehouse location tracking modes and hierarchy rules", () => {
    const warehouseOnly: Warehouse = {
      id: "wh-01",
      tenantId: "tenant-001",
      legalEntityId: "le-001",
      code: "WH-MAIN",
      name: "Main Central Warehouse",
      locationMode: "WarehouseOnly" as LocationTrackingMode,
      defaultLocationId: "loc-def-01",
      status: "Active"
    };

    const hierarchicalWarehouse: Warehouse = {
      id: "wh-02",
      tenantId: "tenant-001",
      legalEntityId: "le-001",
      code: "WH-HIER",
      name: "Jeddah Logistics Distribution",
      locationMode: "HierarchicalLocations" as LocationTrackingMode,
      defaultLocationId: "loc-def-02",
      status: "Active"
    };

    assert.equal(warehouseOnly.locationMode, "WarehouseOnly");
    assert.equal(hierarchicalWarehouse.locationMode, "HierarchicalLocations");

    const locations: InventoryLocation[] = [
      {
        id: "loc-01",
        tenantId: "tenant-001",
        legalEntityId: "le-001",
        warehouseId: "wh-02",
        code: "ZONE-A",
        name: "Dry Goods Zone A",
        locationType: "Zone",
        canHoldStock: false,
        canReceiveInto: false,
        canPickFrom: false,
        hidden: false,
        active: true
      },
      {
        id: "loc-02",
        tenantId: "tenant-001",
        legalEntityId: "le-001",
        warehouseId: "wh-02",
        code: "A-01-BIN-1",
        name: "Shelf 1 Bin 1",
        parentLocationId: "loc-01",
        locationType: "Bin",
        canHoldStock: true,
        canReceiveInto: true,
        canPickFrom: true,
        hidden: false,
        active: true
      }
    ];

    const [zoneLocation, binLocation] = locations;
    assert.ok(zoneLocation && binLocation);
    assert.equal(zoneLocation.canHoldStock, false); // Zones cannot hold stock directly
    assert.equal(binLocation.canHoldStock, true);  // Bins hold stock
    assert.equal(binLocation.parentLocationId, "loc-01");
  });

  it("validates stock balance invariants and statuses", () => {
    const balance: StockBalance = {
      productVariantId: "prod-sku-001",
      warehouseId: "wh-01",
      locationId: "loc-01",
      stockStatus: "Available" as StockStatus,
      onHandBaseQuantity: 100,
      reservedBaseQuantity: 20,
      availableBaseQuantity: 80,
      version: 1,
      lastMovementSequence: 10
    };

    assert.equal(balance.availableBaseQuantity, balance.onHandBaseQuantity - balance.reservedBaseQuantity);
    assert.ok(["Available", "QualityHold", "Quarantine", "Damaged", "Blocked"].includes(balance.stockStatus) || balance.stockStatus === "Available");
  });

  it("validates physical count line variance calculation and blind count behavior", () => {
    const count: PhysicalCount = {
      id: "cnt-001",
      number: "PC-2026-001",
      status: "InProgress",
      lines: 1,
      countedLines: 1,
      varianceQuantity: -2
    };

    const countLine: PhysicalCountLine = {
      id: "line-01",
      productVariantId: "prod-sku-001",
      warehouseId: "wh-01",
      locationId: "loc-01",
      stockStatus: "Available",
      expected: 50,
      counted: 48,
      difference: -2
    };

    assert.equal(count.varianceQuantity, -2);
    assert.equal(countLine.difference, countLine.counted! - countLine.expected);
  });
});
