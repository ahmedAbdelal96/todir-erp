namespace Modules.Manufacturing.Entities;

public sealed class ManufacturingException(string code, string? message = null) : Exception(message ?? code)
{
    public string Code { get; } = code;
}

public static class ManufacturingRules
{
    public static void ValidateMode(string mode, Guid? bomVersionId, Guid? formulaVersionId)
    {
        if (mode is not ("Discrete" or "ProcessBatch")) throw new ManufacturingException("manufacturing.mode_invalid");
        if (mode == "Discrete" && bomVersionId is null) throw new ManufacturingException("manufacturing.bom_required");
        if (mode == "ProcessBatch" && formulaVersionId is null) throw new ManufacturingException("manufacturing.formula_required");
    }

    public static void ValidateBomLines(IReadOnlyList<DTOs.BomLineRequest> lines)
    {
        if (lines.Count == 0) throw new ManufacturingException("manufacturing.bom_lines_required");
        if (lines.Any(line => line.RequiredQty <= 0 || line.ScrapFactor < 0 || line.ScrapFactor >= 1 || string.IsNullOrWhiteSpace(line.UomCode)))
            throw new ManufacturingException("manufacturing.bom_line_invalid");
        if (lines.GroupBy(line => line.ComponentProductVariantId).Any(group => group.Count() > 1)) throw new ManufacturingException("manufacturing.bom_component_duplicate");
    }

    public static string NextOrderState(string current, string action) => (current, action) switch
    {
        ("Draft", "release") => "Released",
        ("Released", "start") => "InProgress",
        ("InProgress", "hold") => "OnHold",
        ("OnHold", "resume") => "InProgress",
        ("InProgress", "complete") => "Completed",
        ("Completed", "close") => "Closed",
        ("Draft", "cancel") => "Cancelled",
        ("Released", "cancel") => "Cancelled",
        ("OnHold", "cancel") => "Cancelled",
        _ => throw new ManufacturingException("manufacturing.invalid_production_transition")
    };

    public static void ValidateMaterialConservation(decimal issued, decimal consumed, decimal returned, decimal scrapped)
    {
        if (issued < 0 || consumed < 0 || returned < 0 || scrapped < 0 || consumed + returned + scrapped > issued)
            throw new ManufacturingException("manufacturing.material_conservation_violation");
    }

    public static void ValidateOutput(decimal ordered, decimal completed, decimal newQuantity)
    {
        if (newQuantity <= 0 || completed + newQuantity > ordered) throw new ManufacturingException("manufacturing.output_exceeds_order");
    }
}
