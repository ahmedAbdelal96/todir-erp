using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Core.Identity;

/// <summary>
/// OpenIddict owns the OAuth application, authorization and token persistence
/// model. It is kept in the platform schema and is separate from the browser
/// session registry so the cookie and bearer boundaries cannot be conflated.
/// </summary>
public sealed class OpenIddictStoreContext(DbContextOptions<OpenIddictStoreContext> options) : DbContext(options)
{
    public DbSet<OpenIddictEntityFrameworkCoreApplication> Applications => Set<OpenIddictEntityFrameworkCoreApplication>();
    public DbSet<OpenIddictEntityFrameworkCoreAuthorization> Authorizations => Set<OpenIddictEntityFrameworkCoreAuthorization>();
    public DbSet<OpenIddictEntityFrameworkCoreScope> Scopes => Set<OpenIddictEntityFrameworkCoreScope>();
    public DbSet<OpenIddictEntityFrameworkCoreToken> Tokens => Set<OpenIddictEntityFrameworkCoreToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("platform");
        modelBuilder.UseOpenIddict();
    }
}
