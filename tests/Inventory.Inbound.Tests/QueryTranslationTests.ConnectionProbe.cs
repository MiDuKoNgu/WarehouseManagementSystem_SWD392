using System.Data.Common;
using Inventory.DAL;
using Inventory.DAL.Inbound;
using Inventory.DAL.Receiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Inbound.Tests;

internal static partial class QueryTranslationTests
{
    private sealed class ConnectionProbe : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new TranslationCompleteException();
    }
}
