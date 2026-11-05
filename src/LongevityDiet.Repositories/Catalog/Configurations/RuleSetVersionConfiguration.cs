using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LongevityDiet.Repositories.Catalog.Configurations;

public sealed class RuleSetVersionConfiguration : IEntityTypeConfiguration<RuleSetVersion>
{
    public void Configure(EntityTypeBuilder<RuleSetVersion> builder)
    {
        builder.ToTable("RuleSetVersions", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VersionLabel).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.VersionLabel).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.EffectiveAt });
        builder.HasMany(x => x.RuleVersions)
            .WithMany(x => x.RuleSets)
            .UsingEntity<Dictionary<string, object>>(
                "RuleSetVersionItem",
                right => right.HasOne<RuleVersion>().WithMany().HasForeignKey("RuleVersionId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<RuleSetVersion>().WithMany().HasForeignKey("RuleSetVersionId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("RuleSetVersionItems", "catalog");
                    join.HasKey("RuleSetVersionId", "RuleVersionId");
                    join.HasIndex("RuleVersionId");
                });
    }
}
