using Identity.DAL;
using Identity.DAL.Repositories;
using Identity.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Identity.Authentication.Tests;

internal static partial class SqlIdentityTests
{
    private sealed class ProbeContextFactory(DbContextOptions<IdentityDbContext> options) : IDbContextFactory<IdentityDbContext>
    {
        public IdentityDbContext CreateDbContext() => new(options);
    }
}
