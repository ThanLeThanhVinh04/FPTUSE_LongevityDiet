using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Repositories;

public sealed class FmdRepository(LongevityDietDbContext dbContext) : IFmdRepository
{
    public async Task<FmdEducationAcknowledgement?> GetLatestAcknowledgementAsync(
        Guid userId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.FmdEducationAcknowledgements
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(version))
        {
            query = query.Where(x => x.EducationVersion == version.Trim());
        }

        var items = await query.ToListAsync(cancellationToken);
        return items
            .OrderByDescending(x => x.AcknowledgedAt)
            .FirstOrDefault();
    }

    public async Task AddAcknowledgementAsync(
        FmdEducationAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default)
    {
        await dbContext.FmdEducationAcknowledgements.AddAsync(acknowledgement, cancellationToken);
    }

    public async Task<FmdSafetyAssessment?> GetLatestAssessmentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.FmdSafetyAssessments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(x => x.EvaluatedAt)
            .FirstOrDefault();
    }

    public async Task AddAssessmentAsync(
        FmdSafetyAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        await dbContext.FmdSafetyAssessments.AddAsync(assessment, cancellationToken);
    }

    public async Task<FmdCycle?> GetActiveCycleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var cycles = await dbContext.FmdCycles
            .Include(x => x.Days)
            .Where(x => x.UserId == userId && x.Status == FmdCycleStatus.Active)
            .ToListAsync(cancellationToken);

        return cycles
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
    }

    public async Task<FmdCycle?> GetCycleByIdAsync(
        Guid cycleId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.FmdCycles
            .Include(x => x.Days)
            .FirstOrDefaultAsync(
                x => x.Id == cycleId && x.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<FmdCycle>> ListCyclesByUserAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var cycles = await dbContext.FmdCycles
            .AsNoTracking()
            .Include(x => x.Days)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return cycles
            .OrderByDescending(x => x.StartedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    public async Task AddCycleAsync(
        FmdCycle cycle,
        CancellationToken cancellationToken = default)
    {
        await dbContext.FmdCycles.AddAsync(cycle, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

