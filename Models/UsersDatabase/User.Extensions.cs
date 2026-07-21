using System.ComponentModel.DataAnnotations.Schema;

namespace YAGOT_2._0.Models.UsersDatabase;

public partial class User
{
    [NotMapped]
    public string? Role { get; set; }
}
