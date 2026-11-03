using System.Net;
using System.Net.Http.Json;
using Inventory.BLL.Putaway;
using Inventory.Models.Putaway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Inbound.Tests;

internal static partial class PutawayApiTests
{
    private sealed class Repository : IPutawayRepository
    {
        public PutawayActor? LastActor; public (string?, int, int) LastQuery; public string? LastSerial; public int Writes; public PutawayException? Error;
        private static readonly PutawaySerial Serial = new(1, "TEST", 1, "IN_WAREHOUSE", "GOOD", false, 1, null, null,
            Convert.ToBase64String(new byte[8]), new(1, "SKU", "TV", 1, "TV", 1, "Brand", "ACTIVE", 50, 50, 50, 10), "Kho A", 1, "IN-1", DateTime.UtcNow, InboundStatus: "DONE");
        public Task<PutawayPendingPage> PendingAsync(PutawayActor actor, string? search, int page, int pageSize, CancellationToken ct)
        { LastActor = actor; LastQuery = (search, page, pageSize); return Task.FromResult(new PutawayPendingPage([Serial], 1, page, pageSize)); }
        public Task<PutawaySerial> FindAsync(PutawayActor actor, long? id, string? serial, CancellationToken ct)
        { LastActor = actor; LastSerial = serial; return Task.FromResult(Serial); }
        public Task<PutawaySuggestions> SuggestionsAsync(PutawayActor actor, long id, CancellationToken ct)
        { LastActor = actor; return Task.FromResult(new PutawaySuggestions(Serial, [])); }
        public Task<PutawayResult> ConfirmAsync(PutawayActor actor, ConfirmPutaway request, CancellationToken ct)
        { LastActor = actor; if (Error is not null) throw Error; Writes++; return Task.FromResult(new PutawayResult(1, "TEST", 3, "Kho A → A → Ô 1", DateTime.UtcNow, actor.UserId, actor.Name, Serial.RowVersion)); }
    }
}
