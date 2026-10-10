using Modules.Catalog.Entities;
using Xunit;

namespace CatalogTests;

public sealed class CatalogDomainTests
{
    [Fact]
    public void ProductAlwaysHasExplicitDefaultVariantIdentity() { var template = ProductTemplate.Create(Guid.CreateVersion7(), "Consulting", ProductNature.Service); var variant = ProductVariant.Create(Guid.CreateVersion7(), template.Id); Assert.Equal(template.Id, variant.TemplateId); Assert.True(variant.IsDefault); }
    [Fact]
    public void GoodsAndServiceAreDistinctWithoutInventorySemantics() { Assert.NotEqual(ProductNature.Goods, ProductNature.Service); Assert.Equal(ProductNature.Goods, ProductTemplate.Create(Guid.CreateVersion7(), "Paper", ProductNature.Goods).Nature); }
    [Fact]
    public void BarcodeAndSkuNormalizeWithoutReplacingDisplayValues() { Assert.Equal("ABC-123", CatalogNormalization.Text(" abc-123 ")); Assert.Equal("ABC123", IdentifierRules.RequireBarcode(" abc-123 ")); }
    [Fact]
    public void FractionalQuantityIsRejectedForWholeUnit() { Assert.Throws<CatalogDomainException>(() => QuantityConversion.ToBase(1.5m, 1m, false, 0)); Assert.Equal(12m, QuantityConversion.ToBase(2m, 6m, false, 0)); }
    [Fact]
    public void CategoryCannotBecomeItsOwnAncestor() { var id = Guid.CreateVersion7(); var parent = Guid.CreateVersion7(); var category = new CategoryNode(id, "Root", parent); var parents = new Dictionary<Guid, Guid?> { [parent] = id }; Assert.Throws<CatalogDomainException>(() => category.Move(parent, parents)); }
    [Fact]
    public void ArchivedProductCannotChangeIdentity() { var product = ProductTemplate.Create(Guid.CreateVersion7(), "Paper", ProductNature.Goods); product.Archive(); Assert.Throws<CatalogDomainException>(() => product.ChangeIdentity("Other", null, null, null, null)); }
}
