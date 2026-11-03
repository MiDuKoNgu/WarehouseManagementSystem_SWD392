using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WMS.Audit;

/// <summary>Read-only SQL operations against one owning service database. No cross-database joins.</summary>
public sealed class SqlAuditReader
{
    private readonly IDbContextFactory<AuditDbContext> _contexts;
    private readonly string _source;
    private readonly string _cte;

    public SqlAuditReader(string connectionString, string source)
        : this(new PooledDbContextFactory<AuditDbContext>(new DbContextOptionsBuilder<AuditDbContext>().UseSqlServer(connectionString).Options), source) { }

    internal SqlAuditReader(IDbContextFactory<AuditDbContext> contexts, string source)
    {
        if (source is not ("procurement" or "inventory")) throw new ArgumentException("Unsupported audit source.", nameof(source));
        _contexts = contexts;
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
                CROSS APPLY (SELECT CASE WHEN ISJSON(a.NewValues) = 1 THEN a.NewValues ELSE NCHAR(123) + NCHAR(125) END AS Payload) j
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
        await using var db = await _contexts.CreateDbContextAsync(ct);
        // Materialize each CTE directly: SQL Server does not allow composing it as a subquery.
        var totals = await db.Database.SqlQueryRaw<int>(_cte + " SELECT COUNT(*) AS Value FROM Logs " + Filter,
            Parameters(query)).ToArrayAsync(ct);
        var parameters = Parameters(query).Concat(new object[]
        {
            new SqlParameter("@take", SqlDbType.Int) { Value = query.PageSize + 1 },
            new SqlParameter("@cursorTime", SqlDbType.DateTime2) { Value = cursor is null ? DBNull.Value : cursor.CreatedAt.UtcDateTime },
            new SqlParameter("@cursorId", SqlDbType.BigInt) { Value = cursor?.AuditLogId ?? 0 },
            new SqlParameter("@sourceAfter", SqlDbType.Bit) { Value = cursor is not null && string.CompareOrdinal(_source, cursor.Source) > 0 },
            new SqlParameter("@sameSource", SqlDbType.Bit) { Value = cursor?.Source == _source }
        }).ToArray();
        var rows = await db.Database.SqlQueryRaw<AuditReadRow>(_cte + "\n" + """
            SELECT TOP (@take) AuditLogID,UserID,ActorName,BusinessGroup,Action,EntityName,EntityID,DocumentCode,Status,CreatedAt,
                CAST(NULL AS nvarchar(max)) AS OldValues, CAST(NULL AS nvarchar(max)) AS NewValues
            FROM Logs
            """ + "\n" + Filter + "\n" + """
            AND (@cursorTime IS NULL OR CreatedAt < @cursorTime
                OR (CreatedAt = @cursorTime AND (@sourceAfter = 1 OR (@sameSource = 1 AND AuditLogID < @cursorId))))
            ORDER BY CreatedAt DESC,AuditLogID DESC
            """, parameters).ToArrayAsync(ct);
        var items = rows.Take(query.PageSize).Select(Read).ToArray();
        var hasMore = rows.Length > query.PageSize;
        var next = hasMore && items.Length > 0 ? AuditCursor.Encode(new(items[^1].CreatedAt, _source, items[^1].AuditLogId)) : null;
        return new(items, totals.Single(), hasMore, next, query.PageSize);
    }

    public async Task<AuditDetail?> DetailAsync(long id, CancellationToken ct)
    {
        if (id <= 0) return null;
        await using var db = await _contexts.CreateDbContextAsync(ct);
        var rows = await db.Database.SqlQueryRaw<AuditReadRow>(_cte + "\n" + """
            SELECT l.AuditLogID,l.UserID,l.ActorName,l.BusinessGroup,l.Action,l.EntityName,l.EntityID,l.DocumentCode,l.Status,l.CreatedAt,
                   a.OldValues,a.NewValues
            FROM Logs l JOIN dbo.AuditLog a ON a.AuditLogID=l.AuditLogID
            WHERE l.AuditLogID=@id AND l.BusinessGroup IS NOT NULL
            """, new SqlParameter("@id", SqlDbType.BigInt) { Value = id }).ToArrayAsync(ct);
        var row = rows.SingleOrDefault();
        return row is null ? null : new(Read(row), AuditValueRedactor.Sanitize(row.OldValues),
            AuditValueRedactor.Sanitize(row.NewValues));
    }

    public async Task<string[]> ActionsAsync(CancellationToken ct)
    {
        await using var db = await _contexts.CreateDbContextAsync(ct);
        return await db.Database.SqlQueryRaw<string>(_cte +
            " SELECT DISTINCT Action AS Value FROM Logs WHERE BusinessGroup IS NOT NULL ORDER BY Action").ToArrayAsync(ct);
    }

    private AuditItem Read(AuditReadRow row) => new(_source, row.AuditLogID, row.UserID, row.ActorName,
        row.BusinessGroup, row.Action, row.EntityName, row.EntityID, row.DocumentCode, row.Status,
        new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)));

    private static object[] Parameters(AuditQuery query)
    {
        var code = query.DocumentCode?.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
        return
        [
            new SqlParameter("@user", SqlDbType.Int) { Value = query.UserId is { } user ? user : DBNull.Value },
            new SqlParameter("@group", SqlDbType.VarChar, 16) { Value = (object?)query.BusinessGroup ?? DBNull.Value },
            new SqlParameter("@action", SqlDbType.VarChar, 60) { Value = (object?)query.Action ?? DBNull.Value },
            new SqlParameter("@document", SqlDbType.NVarChar, 210) { Value = code is null ? DBNull.Value : "%" + code + "%" },
            new SqlParameter("@from", SqlDbType.DateTime2) { Value = query.From is { } from ? from.UtcDateTime : DBNull.Value },
            new SqlParameter("@to", SqlDbType.DateTime2) { Value = query.To is { } to ? to.UtcDateTime : DBNull.Value }
        ];
    }
}
