using YAGOT_2._0.Models.UsersDatabase;

namespace YAGOT_2._0.Models.Admin;

public class AdminUsersIndexViewModel
{
    public PagedResult<YAGOT_2._0.Models.UsersDatabase.User> Users { get; init; } = new();
    public int TotalUsers { get; init; }
}
