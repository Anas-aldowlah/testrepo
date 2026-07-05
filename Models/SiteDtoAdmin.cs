namespace YAGOT_2._0.Models
{
    public class SiteDtoAdmin
    {
        public int Siteid { get; set; }
        public string Sitename { get; set; } = null!;
        public string Url { get; set; } = null!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int DurationDay { get; set; }
    }
}
