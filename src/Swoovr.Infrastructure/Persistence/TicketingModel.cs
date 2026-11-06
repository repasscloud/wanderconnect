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
            entity.Property(row => row.RetiredLogin).HasMaxLength(40);
            entity.Property(row => row.RetirementFailure).HasMaxLength(64);
            entity.Ignore(row => row.Retiring);
            entity.HasIndex(row => row.NextRetirementAt).HasFilter("retirement_requested_at IS NOT NULL AND retired_at IS NULL");
        });
        // D337 (Q136): IDs, classification and dates only; no conversation content.
        model.Entity<HelpdeskTicketRetention>(entity => {
            entity.ToTable("helpdesk_ticket_retention");
            entity.HasKey(row => row.ProviderTicketId);
            entity.Property(row => row.ProviderTicketId).ValueGeneratedNever();
            entity.Property(row => row.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(row => row.OverrideCategory).HasConversion<string>().HasMaxLength(32);
            entity.Property(row => row.OverrideReason).HasMaxLength(500);
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(row => row.FailureCode).HasMaxLength(64);
            entity.HasIndex(row => new {row.State, row.RetainUntil});
            entity.HasIndex(row => row.ProviderCustomerId);
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
        // D336 (#221): quarantined support files (facts only; bytes live in the private media store) and verdicts by hash.
        model.Entity<SupportAttachmentHold>(entity => {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Source).HasConversion<string>().HasMaxLength(16);
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(16);
            entity.Property(row => row.Filename).HasMaxLength(255);
            entity.Property(row => row.ContentType).HasMaxLength(128);
            entity.Property(row => row.Sha256).HasMaxLength(64);
            entity.Property(row => row.ObjectKey).HasMaxLength(64);
            entity.Property(row => row.Scanner).HasMaxLength(32);
            entity.Property(row => row.EngineVersion).HasMaxLength(64);
            entity.Property(row => row.SignatureVersion).HasMaxLength(64);
            entity.Property(row => row.Detection).HasMaxLength(200);
            entity.Property(row => row.FailureCode).HasMaxLength(64);
            entity.HasIndex(row => row.GroupId);
            entity.HasIndex(row => new {row.ProviderTicketId, row.ProviderArticleId});
            entity.HasIndex(row => new {row.State, row.NextAttemptAt});
        });
        model.Entity<AttachmentScanVerdict>(entity => {
            entity.HasKey(row => row.Sha256);
            entity.Property(row => row.Sha256).HasMaxLength(64);
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(16);
            entity.Property(row => row.Scanner).HasMaxLength(32);
            entity.Property(row => row.EngineVersion).HasMaxLength(64);
            entity.Property(row => row.SignatureVersion).HasMaxLength(64);
            entity.Property(row => row.Detection).HasMaxLength(200);
            entity.Property(row => row.FailureCode).HasMaxLength(64);
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
        // No foreign key to accounts: the row must outlive a removed account so its Zammad identities are still revoked.
        model.Entity<TicketingStaff>(entity => {
            entity.HasKey(row => row.AccountId);
            entity.HasIndex(row => row.ProviderUserId).IsUnique();
            entity.HasIndex(row => row.WebProviderUserId).IsUnique();
            entity.Property(row => row.AppliedDigest).HasMaxLength(64);
            entity.Property(row => row.FailureCode).HasMaxLength(64);
            entity.HasIndex(row => row.NextAttemptAt);
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
