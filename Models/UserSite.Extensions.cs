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
    public User? User { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Securitylog> Securitylogs { get; set; } = new List<Securitylog>();
}
