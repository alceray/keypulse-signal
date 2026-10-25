namespace KeyPulse.Models;

public class CollectionItem
{
    public int CollectionItemId { get; set; }
    public Guid ItemKey { get; set; } = Guid.NewGuid();
    public string PartKind { get; set; } = "";
    public string? CatalogEntryId { get; set; }
    public string Name { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Brand { get; set; }
    public string? Designer { get; set; }
    public int DetailsSchemaVersion { get; set; } = 1;
    public string DetailsJson { get; set; } = "{}";
    public string QuantityUnit { get; set; } = "";
    public long Revision { get; set; } = 1;
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;
}
