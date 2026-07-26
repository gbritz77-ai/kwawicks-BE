namespace KwaWicks.Application.DTOs;

public class AiReportRequest
{
    public string Prompt { get; set; } = "";
}

public class AiReportResult
{
    public string Narrative { get; set; } = "";
    public List<string> Columns { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}
