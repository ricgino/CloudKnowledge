using CloudKnowledge.Domain.Notifications;
using CloudKnowledge.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
{
    public void Configure(
        EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification =>
            notification.Id);

        builder.Property(notification =>
                notification.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(notification =>
                notification.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(notification =>
                notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(notification =>
                notification.Type)
            .HasColumnName("type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(notification =>
                notification.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(notification =>
                notification.Message)
            .HasColumnName("message")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(notification =>
                notification.Target)
            .HasColumnName("target")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(notification =>
                notification.DeduplicationKey)
            .HasColumnName("deduplication_key")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(notification =>
                notification.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(notification =>
                notification.ReadAtUtc)
            .HasColumnName("read_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Ignore(notification =>
            notification.IsRead);

        builder.HasIndex(notification =>
                new
                {
                    notification.UserId,
                    notification.DeduplicationKey
                })
            .IsUnique()
            .HasDatabaseName(
                "IX_notifications_user_id_deduplication_key");

        builder.HasIndex(notification =>
                new
                {
                    notification.UserId,
                    notification.CreatedAtUtc,
                    notification.Id
                })
            .IsDescending(false, true, true)
            .HasDatabaseName(
                "IX_notifications_user_id_created_at_utc_id");
    }
}
