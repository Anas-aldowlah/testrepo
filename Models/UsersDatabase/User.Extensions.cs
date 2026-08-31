using System.ComponentModel.DataAnnotations.Schema;

namespace YAGOT_2._0.Models.UsersDatabase;

public partial class User
{
    [NotMapped]
    public string? Role { get; set; }

    [NotMapped]
    public int SearchNameSyncVersion { get; set; }

    [NotMapped]
    public bool IsBlocked => SearchNameSyncVersion == 1;
}
