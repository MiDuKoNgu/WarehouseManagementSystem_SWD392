namespace Warehouse.Models.Lookups;

// The flat parent-linked snapshot retains inactive nodes so callers can reject invalid ancestry.
public sealed record PutawayLayout(PutawayWarehouse Warehouse, PutawayLocation[] Locations,
    PutawayZoneRule[] Rules);
