using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WMS.Audit.Source.Tests;

internal static partial class QueryCompilationTests
{
    private sealed class QueryCompiledException : Exception { }
}
