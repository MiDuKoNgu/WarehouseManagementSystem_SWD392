using Inventory.Models.Inbound;

namespace Inventory.BLL.Inbound;

public sealed class ReceiptException(int statusCode, string message) : Exception(message)
{ public int StatusCode { get; } = statusCode; }

public static class InboundRules
{
    public static SaveReceiptItem Validate(SaveReceiptItem value, ReceiptPolicy policy, DateOnly today)
    {
        var serial = value.Serial?.Trim() ?? "";
        if (serial.Length is < 1 or > 100 || serial.Any(char.IsControl))
            throw new ReceiptException(400, "Serial cần từ 1 đến 100 ký tự, không chứa ký tự điều khiển.");
        if (value.PoLineId <= 0 || value.ManufacturingMonth is < 1 or > 12 || value.ManufacturingYear < 1900
            || value.ManufacturingYear > today.Year || value.ManufacturingYear == today.Year && value.ManufacturingMonth > today.Month)
            throw new ReceiptException(400, "Dòng PO hoặc tháng/năm sản xuất không hợp lệ; không được ở tương lai.");
        // DAMAGED denotes minor damage explicitly accepted by warehouse staff into quarantine.
        // Severely damaged/rejected devices never enter this receipt or physical inventory.
        if (value.ReceivedCondition is not ("GOOD" or "DAMAGED")) throw new ReceiptException(400, "Chỉ tiếp nhận hàng tốt hoặc hàng hỏng nhẹ được chấp nhận cách ly; hàng hỏng nặng phải từ chối.");
        if (value.ReceivedCondition == "DAMAGED" && (!policy.AcceptDamaged || string.IsNullOrWhiteSpace(value.ConditionNote)))
            throw new ReceiptException(400, policy.AcceptDamaged ? "Hàng hỏng cần mô tả tình trạng." : "Kho chưa cho phép tiếp nhận hàng hỏng.");
        if (value.ConditionNote?.Length > 1000) throw new ReceiptException(400, "Ghi chú tình trạng tối đa 1000 ký tự.");
        if (value.InputMethod is not ("MANUAL" or "CAMERA" or "IMAGE_UPLOAD")) throw new ReceiptException(400, "Phương thức nhập không hợp lệ.");
        Version(value.RowVersion);
        return value with { Serial = serial, ConditionNote = value.ConditionNote?.Trim() };
    }
    public static void Version(string? version)
    {
        try { if (Convert.FromBase64String(version ?? "").Length == 8) return; } catch (FormatException) { }
        throw new ReceiptException(400, "Phiên bản phiếu không hợp lệ. Vui lòng tải lại.");
    }
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
}
