using System.ComponentModel.DataAnnotations.Schema;

namespace YAGOT_2._0.Models;

public partial class Order
{
    [NotMapped]
    public UserSite? User { get; set; }
}
