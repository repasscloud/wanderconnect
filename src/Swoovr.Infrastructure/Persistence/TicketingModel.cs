using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

internal static class TicketingModel
{
    public static void AddTicketing(this ModelBuilder model)
    {
        model.Entity<TicketingIdentity>(entity => {
            entity.HasKey(row => row.AccountId);
            entity.HasIndex(row => row.ProviderUserId).IsUnique();
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(32);
        });
        model.Entity<TicketingOperation>(entity => {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Kind).HasMaxLength(32);
            entity.Property(row => row.RequestDigest).HasMaxLength(64);
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(row => new {row.AccountId, row.CreatedAt});
        });
        model.Entity<TicketingCaseLink>(entity => {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => new {row.ProviderTicketId, row.CaseId, row.AppealId}).IsUnique().HasFilter("appeal_id IS NOT NULL");
            entity.HasIndex(row => new {row.ProviderTicketId, row.CaseId}).IsUnique().HasFilter("appeal_id IS NULL");
            entity.HasIndex(row => row.CaseId);
        });
        model.Entity<TicketingWebhookWork>(entity => {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.DeliveryId).HasMaxLength(128);
            entity.Property(row => row.BodyDigest).HasMaxLength(64);
            entity.Property(row => row.FailureCode).HasMaxLength(64);
            entity.HasIndex(row => row.DeliveryId).IsUnique();
            entity.HasIndex(row => row.BodyDigest).IsUnique();
            entity.HasIndex(row => new {row.CompletedAt, row.NextAttemptAt});
        });
        model.Entity<TicketingGuest>(entity => {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EmailLookup).HasMaxLength(32);
            entity.HasIndex(row => row.EmailLookup).IsUnique();
            entity.HasIndex(row => row.ProviderUserId).IsUnique();
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(32);
        });
        model.Entity<TicketingGuestChallenge>(entity => {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EmailLookup).HasMaxLength(32);
            entity.Property(row => row.AddressLookup).HasMaxLength(32);
            entity.Property(row => row.CodeHash).HasMaxLength(32);
            entity.HasIndex(row => new {row.EmailLookup, row.CreatedAt});
            entity.HasIndex(row => new {row.AddressLookup, row.CreatedAt});
        });
        model.Entity<TicketingGuestGrant>(entity => {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.TokenHash).HasMaxLength(32);
            entity.HasIndex(row => row.TokenHash).IsUnique();
            entity.HasIndex(row => new {row.GuestId, row.ExpiresAt});
            entity.HasOne<TicketingGuest>().WithMany().HasForeignKey(row => row.GuestId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
