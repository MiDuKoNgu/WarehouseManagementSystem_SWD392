using System.Data.Common;
using Inventory.DAL;
using Inventory.DAL.Inbound;
using Inventory.DAL.Receiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Inbound.Tests;

internal static partial class QueryTranslationTests
{

    private sealed class TranslationCompleteException : Exception { }
}
