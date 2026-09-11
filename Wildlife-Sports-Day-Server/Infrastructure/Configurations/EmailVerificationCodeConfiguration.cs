using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wildlife_Sports_Day_Server.Entities;

namespace Wildlife_Sports_Day_Server.Infrastructure.Configurations;

public class EmailVerificationCodeConfiguration : IEntityTypeConfiguration<EmailVerificationCode>
{
    public void Configure(EntityTypeBuilder<EmailVerificationCode> builder)
    {
        builder.ToTable("email_verification_codes");

        builder.HasKey(code => code.Id);
        builder.Property(code => code.Id).HasColumnName("email_verification_code_id");

        builder.Property(code => code.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();
        builder.HasIndex(code => code.Email);

        builder.Property(code => code.CodeHash)
            .HasColumnName("code_hash")
            .IsRequired();

        builder.Property(code => code.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(code => code.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(code => code.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(EmailVerificationCodeStatus.Pending)
            .IsRequired();

        builder.Property(code => code.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(code => code.VerifiedAt)
            .HasColumnName("verified_at");

        builder.Property(code => code.UnavailableAt)
            .HasColumnName("unavailable_at");
    }
}
