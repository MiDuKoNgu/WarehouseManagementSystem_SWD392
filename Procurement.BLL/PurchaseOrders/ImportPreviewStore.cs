using System.Collections.Concurrent;
using System.Security.Cryptography;
using Procurement.Models.PurchaseOrders;

namespace Procurement.BLL.PurchaseOrders;

public sealed partial class ImportPreviewStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    public string Add(int userId, CreatePurchaseOrder request)
    {
        foreach (var item in _entries.Where(item => item.Value.ExpiresAt <= DateTimeOffset.UtcNow))
            _entries.TryRemove(item.Key, out _);
        if (_entries.Count >= 1000) throw new ImportConflictException("Có quá nhiều bản xem trước. Vui lòng thử lại sau.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _entries[token] = new(userId, request);
        return token;
    }
    public Entry Get(string token, int userId)
    {
        if (string.IsNullOrEmpty(token) || !_entries.TryGetValue(token, out var entry)
            || entry.UserId != userId || entry.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ImportConflictException("Bản xem trước đã hết hạn hoặc không hợp lệ. Vui lòng tải lại file.");
        return entry;
    }
}
