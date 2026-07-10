namespace WDAS.Application.Options;

public class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".jpg", ".jpeg", ".png"];
    public string StorageRoot { get; set; } = "uploads";
    public bool VirusScanEnabled { get; set; }
    public string ClamAvHost { get; set; } = "localhost";
    public int ClamAvPort { get; set; } = 3310;
    public bool FailUploadWhenScannerUnavailable { get; set; } = true;
}

public class ExternalApproverOptions
{
    public const string SectionName = "ExternalApprovers";

    public int LinkExpiryHours { get; set; } = 72;
    public int OtpExpiryMinutes { get; set; } = 10;
    public string AppBaseUrl { get; set; } = "http://localhost:8080";
}
