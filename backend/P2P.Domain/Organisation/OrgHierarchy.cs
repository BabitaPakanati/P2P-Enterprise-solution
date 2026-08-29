using P2P.Domain.Common;

namespace P2P.Domain.Organisation;

/// <summary>
/// A legal entity within this organisation (one org can span several - e.g. regional
/// subsidiaries that each issue their own POs and invoices).
/// </summary>
public sealed class LegalEntity : Entity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Country { get; set; }
    public string BaseCurrency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
}

public sealed class BusinessUnit : Entity
{
    public Guid LegalEntityId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public sealed class Department : Entity
{
    public Guid BusinessUnitId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public sealed class CostCenter : Entity
{
    public Guid BusinessUnitId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public sealed class Location : Entity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? AddressLine { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
}
