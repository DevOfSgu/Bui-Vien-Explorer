namespace TravelSystem.Shared.Models;

public class Zone
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Radius { get; set; }
    public int OrderIndex { get; set; }
    public int ZoneType { get; set; }
    public int ShopId { get; set; }
    public int IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}