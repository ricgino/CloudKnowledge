using CloudKnowledge.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration
    : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(
        EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("IX_user_accounts_email");
    }
}