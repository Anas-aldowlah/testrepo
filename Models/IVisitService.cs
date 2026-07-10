namespace YAGOT_2._0.Models
{
    public interface IVisitService
    {
        Task SaveVisitAsync(HttpContext context,string name);
    }
}
