namespace PosApp.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Cashier = "Cashier";
}

public static class OptionKinds
{
    public const string Size = "Size";
    public const string Addition = "Addition";
}

public class UserAccount
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = Roles.Cashier;
}

public class Category
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>A customization of a product: a Size (S/M/L...) or an Addition (extra chocolate...).</summary>
public class CustomOption
{
    public long Id { get; set; }
    public string Kind { get; set; } = OptionKinds.Size;
    public string Name { get; set; } = "";
    /// <summary>Amount added to the base price when this option is chosen.</summary>
    public decimal PriceDelta { get; set; }
}

public class Product
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public byte[]? Image { get; set; }
    public List<CustomOption> Options { get; set; } = new();
}

public class PaymentMethod
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Details { get; set; } = "";
    public byte[]? Image { get; set; }
}

public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Served = "Served";
    public const string Voided = "Voided";
}

public static class DiscountTypes
{
    public const string None = "";
    public const string Pwd = "PWD";
    public const string Senior = "Senior";
    public const decimal Rate = 0.20m;
}

/// <summary>One line of an order being saved (already priced by the cashier screen).</summary>
public class NewOrderLine
{
    public string ItemName { get; set; } = "";
    public string Options { get; set; } = "";
    public int Qty { get; set; }
    public decimal LineTotal { get; set; }
}

public class NewOrder
{
    public string CashierName { get; set; } = "";
    public string DiscountType { get; set; } = DiscountTypes.None;
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    /// <summary>Money handed over by the customer; change = AmountPaid − Total.</summary>
    public decimal AmountPaid { get; set; }
    public string? PaymentMethod { get; set; }
    public List<NewOrderLine> Lines { get; set; } = new();
}

/// <summary>An order waiting in the queue panel.</summary>
public class QueueOrder
{
    public long Id { get; set; }
    public int QueueNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
    public string Summary { get; set; } = "";
}

public class OrderRecord
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CashierName { get; set; } = "";
    public decimal Total { get; set; }
    public int Quantity { get; set; }
    public string Summary { get; set; } = "";
}
