using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using YAGOT_2._0.Models.UsersDatabase;

namespace YAGOT_2._0.Models;

public partial class UserSite
{
    [NotMapped]
    public string? Name { get; set; }

    [NotMapped]
    public string? Phone { get; set; }

    [NotMapped]
    public string? Email { get; set; }

    [NotMapped]
    public UsersDatabase.User? User { get; set; }

    [NotMapped]
    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    [NotMapped]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    [NotMapped]
    public virtual ICollection<Securitylog> Securitylogs { get; set; } = new List<Securitylog>();
}
