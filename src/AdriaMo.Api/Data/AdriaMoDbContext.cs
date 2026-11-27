using AdriaMo.Core.Constats;
using AdriaMo.Core.Photos;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdriaMo.Api.Data;

/// <summary>The database: the accounts (ASP.NET Core Identity) and their paired phones, the constats and their pieces, the inbox.</summary>
/// <param name="options">The options.</param>
internal sealed class AdriaMoDbContext(DbContextOptions<AdriaMoDbContext> options) : IdentityDbContext<AdriaMoUser>(options)
{
    /// <summary>The constats.</summary>
    public DbSet<Constat> Constats => Set<Constat>();

    /// <summary>The pieces.</summary>
    public DbSet<Piece> Pieces => Set<Piece>();

    /// <summary>The photos waiting in the inbox "À classer".</summary>
    public DbSet<InboxPhoto> Inbox => Set<InboxPhoto>();

    /// <summary>The paired phones.</summary>
    public DbSet<PairedDevice> Devices => Set<PairedDevice>();

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
            piece.OwnsOne(p => p.Photo, photo => ConfigurePhoto(photo, unique: false));
        });

        builder.Entity<InboxPhoto>(inbox =>
        {
            inbox.ToTable("InboxPhotos");
            inbox.Property(p => p.AuthorId).HasMaxLength(450);
            inbox.HasOne<AdriaMoUser>().WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);
            inbox.Ignore(p => p.IsDated);
            // One photo waits once: the phone may send it again when an answer is lost on the way.
            inbox.OwnsOne(p => p.Photo, photo => ConfigurePhoto(photo, unique: true));
            inbox.Navigation(p => p.Photo).IsRequired();
        });

        builder.Entity<PairedDevice>(device =>
        {
            device.Property(d => d.Id).HasMaxLength(32);
            device.Property(d => d.UserId).HasMaxLength(450);
            device.Property(d => d.Name).HasMaxLength(Core.Accounts.PairingRules.NameMaxLength);
            device.Property(d => d.CodeHash).HasMaxLength(64);
            device.Property(d => d.SecurityStamp).HasMaxLength(256);
            device.HasIndex(d => d.CodeHash).IsUnique();
            device.HasOne<AdriaMoUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePhoto<TOwner>(OwnedNavigationBuilder<TOwner, PiecePhoto> photo, bool unique)
        where TOwner : class
    {
        photo.Property(f => f.OriginalSha256).HasMaxLength(64);
        photo.Property(f => f.OriginalFileName).HasMaxLength(80);
        photo.Property(f => f.PublishedFileName).HasMaxLength(80);
        photo.Property(f => f.ThumbnailFileName).HasMaxLength(80);
        photo.HasIndex(f => f.OriginalSha256).IsUnique(unique);
    }
}
