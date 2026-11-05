using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LongevityDiet.Repositories.Catalog.Configurations;

public sealed class DietRuleConfiguration : IEntityTypeConfiguration<DietRule>
{
    public void Configure(EntityTypeBuilder<DietRule> builder)
    {
        builder.ToTable("DietRules", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RuleKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.RuleKey).IsUnique();
    }
}
