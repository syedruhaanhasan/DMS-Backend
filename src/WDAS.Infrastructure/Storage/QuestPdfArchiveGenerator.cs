using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WDAS.Application.Abstractions;

namespace WDAS.Infrastructure.Storage;

public class QuestPdfArchiveGenerator : IArchivePdfGenerator
{
    static QuestPdfArchiveGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(
        string archiveId,
        string subject,
        string bodyHtml,
        string approvalTrailJson,
        string ownerName,
        DateTime finalizedAtUtc)
    {
        var bodyText = StripHtml(bodyHtml);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("WDAS — Archived Document").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(archiveId).Bold().FontSize(18);
                    col.Item().PaddingTop(4).Text(subject).SemiBold().FontSize(14);
                    col.Item().Text($"Owner: {ownerName} · Finalized: {finalizedAtUtc:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Item().Text("Document body").Bold().FontSize(11);
                    col.Item().PaddingTop(4).Text(bodyText);

                    col.Item().PaddingTop(16).Text("Approval trail").Bold().FontSize(11);
                    col.Item().PaddingTop(4).Text(approvalTrailJson).FontFamily(Fonts.CourierNew).FontSize(8);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Immutable archive · ");
                    text.Span(archiveId).SemiBold();
                    text.Span(" · Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</p>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, "\\s+", " ").Trim();
    }
}
