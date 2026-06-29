---
name: efcore-guide
description: EF Core and PostgreSQL guidance for Wildlife Survival Server. Use for DbContext design, FluentAPI entity configuration, migrations, secure connection string setup, ranking query performance, and N+1 prevention.
---

# efcore-guide Skill

Guide for EF Core migrations, FluentAPI configuration, and N+1 prevention patterns.

---

## AppDbContext

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<Score> Scores => Set<Score>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

---

## FluentAPI Configuration (Separate configuration files recommended)

```csharp
// Infrastructure/Configurations/UserConfiguration.cs
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("user_id");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Nickname)
            .HasColumnName("nickname")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
    }
}
```

---

## Score Entity & Configuration

```csharp
public class Score
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int Value { get; set; }
    public DateTime AchievedAt { get; set; } = DateTime.UtcNow;
}

// ScoreConfiguration.cs
public class ScoreConfiguration : IEntityTypeConfiguration<Score>
{
    public void Configure(EntityTypeBuilder<Score> builder)
    {
        builder.ToTable("scores");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("score_id");
        builder.Property(s => s.Value).HasColumnName("value").IsRequired();
        builder.Property(s => s.AchievedAt).HasColumnName("achieved_at");

        builder.HasOne(s => s.User)
            .WithMany(u => u.Scores)
            .HasForeignKey(s => s.UserId)
            .HasConstraintName("fk_scores_users");

        // Indexes for ranking query optimization
        builder.HasIndex(s => s.Value);
        builder.HasIndex(s => new { s.UserId, s.Value });
    }
}
```

---

## N+1 Prevention Patterns

```csharp
// Wrong — causes N+1
var scores = await dbContext.Scores.ToListAsync();
foreach (var score in scores)
{
    var user = score.User; // Triggers a SELECT per item
}

// Correct — Eager Loading
var scores = await dbContext.Scores
    .Include(s => s.User)
    .ToListAsync();

// Ranking query — best score per user
var rankings = await dbContext.Scores
    .Include(s => s.User)
    .GroupBy(s => s.UserId)
    .Select(g => new
    {
        UserId = g.Key,
        Nickname = g.First().User.Nickname,
        BestScore = g.Max(s => s.Value)
    })
    .OrderByDescending(r => r.BestScore)
    .Take(100)
    .ToListAsync();
```

---

## Migration Management

For migration planning, generation, review, rollback, scripts, or database updates, also load `Codex/skills/migration-guide/SKILL.md` and follow its approval guardrails.

```bash
# Add migration
dotnet ef migrations add InitialCreate --project Wildlife-Sports-Day-Server/Wildlife-Sports-Day-Server.csproj

# Apply to DB
dotnet ef database update

# Roll back migration
dotnet ef database update PreviousMigrationName

# Check SQL script
dotnet ef migrations script
```

**Rules:**
- Migration files **must be included in commits**
- Never run `dotnet ef database update` directly on production DB → apply via migration scripts
- Migration names: PascalCase verb+noun (`AddEmailVerificationCode`, `CreateScoreTable`)

---

## ConnectionString Setup (Security)

### Development (User Secrets)

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=wildlife_db;Username=postgres;Password=<password>"
```

### appsettings.json (structure only, no values)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

### Program.cs

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionString is not configured.")
    ));
```
