namespace ConnectWiseManager.Models;

public class ReportingCompany
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Code) && !string.IsNullOrWhiteSpace(Name))
            {
                return $"{Code} - {Name} ({Id})";
            }
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return string.IsNullOrWhiteSpace(Id) ? Name : $"{Name} ({Id})";
            }
            return Id;
        }
    }
}
