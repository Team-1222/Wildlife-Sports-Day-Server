using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Wildlife_Sports_Day_Server.Infrastructure;

#nullable disable

namespace Wildlife_Sports_Day_Server.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.9")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Wildlife_Sports_Day_Server.Entities.EmailVerificationCode", builder =>
        {
            builder.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer")
                .HasColumnName("email_verification_code_id");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(builder.Property<int>("Id"));

            builder.Property<string>("CodeHash")
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("code_hash");

            builder.Property<int>("AttemptCount")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer")
                .HasColumnName("attempt_count")
                .HasDefaultValue(0);

            builder.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            builder.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("email");

            builder.Property<DateTime>("ExpiresAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("expires_at");

            builder.Property<bool>("IsUsed")
                .ValueGeneratedOnAdd()
                .HasColumnType("boolean")
                .HasColumnName("is_used")
                .HasDefaultValue(false);

            builder.Property<bool>("IsVerified")
                .ValueGeneratedOnAdd()
                .HasColumnType("boolean")
                .HasColumnName("is_verified")
                .HasDefaultValue(false);

            builder.Property<DateTime?>("UsedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("used_at");

            builder.Property<DateTime?>("VerifiedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("verified_at");

            builder.HasKey("Id");

            builder.HasIndex("Email");

            builder.ToTable("email_verification_codes", (string)null);
        });

        modelBuilder.Entity("Wildlife_Sports_Day_Server.Entities.User", builder =>
        {
            builder.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer")
                .HasColumnName("user_id");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(builder.Property<int>("Id"));

            builder.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            builder.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("email");

            builder.Property<string>("Nickname")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)")
                .HasColumnName("nickname");

            builder.Property<string>("PasswordHash")
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("password_hash");

            builder.HasKey("Id");

            builder.HasIndex("Email")
                .IsUnique();

            builder.ToTable("users", (string)null);
        });
    }
}
