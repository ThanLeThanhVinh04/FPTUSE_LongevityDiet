namespace LongevityDiet.Domain.Catalog;

public enum RuleVersionState
{
    Draft = 1,
    Published = 2,
    Retired = 3
}

public enum EvidenceLevel
{
    Low = 1,
    Moderate = 2,
    High = 3
}

public enum SourceType
{
    Book = 1,
    OfficialGuidance = 2,
    PeerReviewed = 3,
    TeamDefined = 4
}
