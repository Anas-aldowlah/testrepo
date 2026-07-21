using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models.UsersDatabase;

public partial class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Passwordhash { get; set; } = null!;

    public DateTime? Createdat { get; set; }

    public string? Email { get; set; }
}