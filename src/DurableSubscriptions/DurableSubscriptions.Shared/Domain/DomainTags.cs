namespace DurableSubscriptions.Shared;

public static class DomainConstants
{
    public static string ProductTag(ProductId productId) => $"product-{productId.Id}";
    
    public static readonly string[] Products = ["oil", "gold", "silver", "copper", "platinum"];
}