using System.ComponentModel.DataAnnotations;

namespace YAGOT_2._0.Models;

public static class InventoryConflictDecisions
{
    public const string Continue = "Continue";
    public const string Remove = "Remove";
}

public sealed class CustomerInventoryConflictViewModel
{
    public int OrderId { get; init; }
    public IReadOnlyList<CustomerInventoryConflictProductViewModel> Products { get; init; } = [];
    public IReadOnlyList<CustomerInventoryConflictLineViewModel> Lines { get; init; } = [];
    public bool CanSubmitResolution => Lines.Count > 0;
}

public sealed class CustomerInventoryConflictProductViewModel
{
    public int OrderitemId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int RequestedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public bool IsConflict { get; init; }
}

public sealed class CustomerInventoryConflictLineViewModel
{
    public int OrderitemId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int RequestedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public bool CanContinue => AvailableQuantity > 0;
    public bool CanRemove { get; init; }
}

public sealed class ResolveInventoryConflictRequest
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    public bool CancelEntireOrder { get; set; }

    public List<InventoryConflictLineDecisionInput> Decisions { get; set; } = [];
}

public sealed class InventoryConflictLineDecisionInput
{
    [Range(1, int.MaxValue)]
    public int OrderitemId { get; set; }

    [Required]
    public string Decision { get; set; } = string.Empty;
}
