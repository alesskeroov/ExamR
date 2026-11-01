namespace ExamFlow.Api.Common;

/// <summary>Timestamps are set by <see cref="Infrastructure.Data.AuditInterceptor"/>.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
