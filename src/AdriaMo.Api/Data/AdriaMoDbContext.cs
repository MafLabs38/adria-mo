using AdriaMo.Core.Constats;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdriaMo.Api.Data;

/// <summary>The database: the accounts (ASP.NET Core Identity), the constats and their pieces.</summary>
/// <param name="options">The options.</param>
internal sealed class AdriaMoDbContext(DbContextOptions<AdriaMoDbContext> options) : IdentityDbContext<AdriaMoUser>(options)
{
    /// <summary>The constats.</summary>
    public DbSet<Constat> Constats => Set<Constat>();

    /// <summary>The pieces.</summary>
    public DbSet<Piece> Pieces => Set<Piece>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no date type: an instant is stored as a number that keeps its order (UTC ticks and the offset).
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<ConstatStatus>().HaveConversion<string>().HaveMaxLength(32);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Constat>(constat =>
        {
            constat.Property(c => c.Title).HasMaxLength(Constat.TitleMaxLength);
            constat.Property(c => c.Category).HasMaxLength(32);
            constat.Property(c => c.Location).HasMaxLength(Constat.LocationMaxLength);
            constat.Ignore(c => c.FirstObservedAt);
            constat.HasMany(c => c.Pieces).WithOne().HasForeignKey(p => p.ConstatId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Piece>(piece =>
        {
            piece.Property(p => p.Text).HasMaxLength(Constat.TextMaxLength);
            piece.Property(p => p.AuthorId).HasMaxLength(450);
            piece.HasOne<AdriaMoUser>().WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);
            piece.Ignore(p => p.ObservedAt);
            piece.OwnsOne(p => p.Photo, photo =>
            {
                photo.Property(f => f.OriginalSha256).HasMaxLength(64);
                photo.Property(f => f.OriginalFileName).HasMaxLength(80);
                photo.Property(f => f.PublishedFileName).HasMaxLength(80);
                photo.Property(f => f.ThumbnailFileName).HasMaxLength(80);
            });
        });
    }
}
