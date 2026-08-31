using System;
using System.Collections.Generic;

namespace YAGOT_2._0.Models;

public partial class UserSite
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? Role { get; set; }

    public int SearchNameSyncVersion { get; set; }
}

