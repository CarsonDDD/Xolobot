namespace Hackathon.Entities;

public class InventoryItem
{
    public int Id { get; set; }
    public int Inventory_Id { get; set; }
    public int Item_Id { get; set; }
    public int ActualCost { get; set; }
    public int Amount { get; set; }
}
