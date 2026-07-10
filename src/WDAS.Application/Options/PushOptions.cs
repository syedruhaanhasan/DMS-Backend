namespace WDAS.Application.Options;

public class PushOptions
{
    public const string SectionName = "Push";

    public bool Enabled { get; set; }
    public string ExpoApiUrl { get; set; } = "https://exp.host/--/api/v2/push/send";
}
