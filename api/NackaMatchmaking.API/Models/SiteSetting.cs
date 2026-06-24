namespace NackaMatchmaking.API.Models
{
    public class SiteSetting
    {
        public Guid CompanyId { get; set; }
        // public Company? Company { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; } = string.Empty;
    }
}
