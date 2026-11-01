using System.Data;
using Microsoft.Data.SqlClient;

namespace WMS.Audit;

/// <summary>Read-only SQL operations against one owning service database. No cross-database joins.</summary>
public sealed class SqlAuditReader
{
    private readonly string _connectionString;
    private readonly string _source;
    private readonly string _cte;

    public SqlAuditReader(string connectionString, string source)
    {
        if (source is not ("procurement" or "inventory")) throw new ArgumentException("Unsupported audit source.", nameof(source));
        _connectionString = connectionString;
        _source = source;
        var join = source == "procurement"
            ? "LEFT JOIN dbo.PurchaseOrder d ON d.PurchaseOrderID = TRY_CONVERT(bigint, a.EntityID) AND UPPER(a.EntityName) IN ('PURCHASEORDER','PO')"
            : "LEFT JOIN dbo.Inbound d ON d.InboundID = COALESCE(TRY_CONVERT(bigint, JSON_VALUE(j.Payload, '$.inboundId')), CASE WHEN UPPER(a.EntityName) = 'INBOUND' THEN TRY_CONVERT(bigint, a.EntityID) END)";
        var document = source == "procurement" ? "d.POCode" : "d.ReceiptCode";
        var group = source == "procurement"
            ? "CASE WHEN UPPER(a.EntityName) IN ('PURCHASEORDER','PO') OR UPPER(a.Action) IN ('CREATE_PO','UPDATE_PO','CONFIRM_PO','CANCEL_PO','CLOSE_PO','IMPORT_PO_EXCEL') THEN 'PO' END"
            : "CASE WHEN UPPER(a.Action) LIKE '%PUTAWAY%' OR UPPER(a.EntityName) = 'PUTAWAY' THEN 'PUTAWAY' WHEN UPPER(a.EntityName) = 'INBOUND' OR UPPER(a.Action) IN ('CREATE_INBOUND','UPDATE_INBOUND','COMPLETE_INBOUND') THEN 'INBOUND' END";
        _cte = $$"""
            WITH Logs AS (
                SELECT a.AuditLogID, a.UserID, a.Action, a.EntityName, a.EntityID, a.Status, a.CreatedAt,
                    COALESCE(JSON_VALUE(j.Payload,'$.performedByName'),
                        CASE WHEN UPPER(a.Action) IN ('CREATE_PO','IMPORT_PO_EXCEL','CREATE_INBOUND')
                            THEN JSON_VALUE(j.Payload,'$.createdByName') END) AS ActorName,
                    {{group}} AS BusinessGroup,
                    COALESCE({{document}},JSON_VALUE(j.Payload,'$.poCode'),JSON_VALUE(j.Payload,'$.receiptCode'),a.EntityID) AS DocumentCode
                FROM dbo.AuditLog a
                CROSS APPLY (SELECT CASE WHEN ISJSON(a.NewValues) = 1 THEN a.NewValues ELSE N'{}' END AS Payload) j
                {{join}}
            )
            """;
    }

    private const string Filter = """
        WHERE BusinessGroup IS NOT NULL AND (@user IS NULL OR UserID = @user)
        AND (@group IS NULL OR BusinessGroup = @group) AND (@action IS NULL OR Action = @action)
        AND (@document IS NULL OR DocumentCode LIKE @document ESCAPE '~')
        AND (@from IS NULL OR CreatedAt >= @from) AND (@to IS NULL OR CreatedAt < @to)
        """;

    public async Task<AuditPage> PageAsync(AuditQuery query, CancellationToken ct)
    {
        query.Validate();
        var cursor = AuditCursor.Decode(query.Cursor);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var count = new SqlCommand(_cte + " SELECT COUNT(*) FROM Logs " + Filter, connection);
        Parameters(count, query);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct));
        await using var command = new SqlCommand(_cte + "\n" + """
            SELECT TOP (@take) AuditLogID,UserID,ActorName,BusinessGroup,Action,EntityName,EntityID,DocumentCode,Status,CreatedAt
            FROM Logs
            """ + "\n" + Filter + "\n" + """
            AND (@cursorTime IS NULL OR CreatedAt < @cursorTime
                OR (CreatedAt = @cursorTime AND (@sourceAfter = 1 OR (@sameSource = 1 AND AuditLogID < @cursorId))))
            ORDER BY CreatedAt DESC,AuditLogID DESC
            """, connection);
        Parameters(command, query);
        command.Parameters.Add("@take", SqlDbType.Int).Value = query.PageSize + 1;
        command.Parameters.Add("@cursorTime", SqlDbType.DateTime2).Value = cursor is null ? DBNull.Value : cursor.CreatedAt.UtcDateTime;
        command.Parameters.Add("@cursorId", SqlDbType.BigInt).Value = cursor?.AuditLogId ?? 0;
        command.Parameters.Add("@sourceAfter", SqlDbType.Bit).Value = cursor is not null && string.CompareOrdinal(_source, cursor.Source) > 0;
        command.Parameters.Add("@sameSource", SqlDbType.Bit).Value = cursor?.Source == _source;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<AuditItem>();
        while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
        var items = rows.Take(query.PageSize).ToArray();
        var hasMore = rows.Count > query.PageSize;
        var next = hasMore && items.Length > 0 ? AuditCursor.Encode(new(items[^1].CreatedAt, _source, items[^1].AuditLogId)) : null;
        return new(items, total, hasMore, next, query.PageSize);
    }

    public async Task<AuditDetail?> DetailAsync(long id, CancellationToken ct)
    {
        if (id <= 0) return null;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(_cte + "\n" + """
            SELECT l.AuditLogID,l.UserID,l.ActorName,l.BusinessGroup,l.Action,l.EntityName,l.EntityID,l.DocumentCode,l.Status,l.CreatedAt,
                   a.OldValues,a.NewValues
            FROM Logs l JOIN dbo.AuditLog a ON a.AuditLogID=l.AuditLogID
            WHERE l.AuditLogID=@id AND l.BusinessGroup IS NOT NULL
            """, connection);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(Read(reader), AuditValueRedactor.Sanitize(reader.IsDBNull(10) ? null : reader.GetString(10)),
            AuditValueRedactor.Sanitize(reader.IsDBNull(11) ? null : reader.GetString(11)));
    }

    public async Task<string[]> ActionsAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(_cte + " SELECT DISTINCT Action FROM Logs WHERE BusinessGroup IS NOT NULL ORDER BY Action", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var values = new List<string>();
        while (await reader.ReadAsync(ct)) values.Add(reader.GetString(0));
        return values.ToArray();
    }

    private AuditItem Read(SqlDataReader reader) => new(_source, reader.GetInt64(0), reader.GetInt32(1),
        reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc)));

    private static void Parameters(SqlCommand command, AuditQuery query)
    {
        command.Parameters.Add("@user", SqlDbType.Int).Value = query.UserId is { } user ? user : DBNull.Value;
        command.Parameters.Add("@group", SqlDbType.VarChar, 16).Value = (object?)query.BusinessGroup ?? DBNull.Value;
        command.Parameters.Add("@action", SqlDbType.VarChar, 60).Value = (object?)query.Action ?? DBNull.Value;
        var code = query.DocumentCode?.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
        command.Parameters.Add("@document", SqlDbType.NVarChar, 210).Value = code is null ? DBNull.Value : "%" + code + "%";
        command.Parameters.Add("@from", SqlDbType.DateTime2).Value = query.From is { } from ? from.UtcDateTime : DBNull.Value;
        command.Parameters.Add("@to", SqlDbType.DateTime2).Value = query.To is { } to ? to.UtcDateTime : DBNull.Value;
    }
}
