namespace WDAS.Application.Abstractions;

public interface IArchivePdfGenerator
{
    byte[] Generate(string archiveId, string subject, string bodyHtml, string approvalTrailJson, string ownerName, DateTime finalizedAtUtc);
}
