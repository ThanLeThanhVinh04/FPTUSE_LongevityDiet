using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LongevityDiet.Repositories.Catalog.Configurations;

public sealed class RuleVersionConfiguration : IEntityTypeConfiguration<RuleVersion>
{
    public void Configure(EntityTypeBuilder<RuleVersion> builder)
    {
        builder.ToTable("RuleVersions", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ParametersJson).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Citation).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.EvidenceLevel).HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(24);
        builder.HasIndex(x => new { x.DietRuleId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.State, x.PublishedAt });
        builder.HasOne(x => x.DietRule)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.DietRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
