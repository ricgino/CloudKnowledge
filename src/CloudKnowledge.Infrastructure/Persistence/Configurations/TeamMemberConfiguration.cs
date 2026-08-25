using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class TeamMemberConfiguration
    : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(
        EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("team_members");

        builder.HasKey(member => new
        {
            member.TeamId,
            member.UserId
        });

        builder.Property(member => member.TeamId)
            .HasColumnName("team_id");

        builder.Property(member => member.UserId)
            .HasColumnName("user_id");

        builder.Property(member => member.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(member => member.JoinedAtUtc)
            .HasColumnName("joined_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(member => member.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_team_members_user_id");
    }
}