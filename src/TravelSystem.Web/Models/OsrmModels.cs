namespace TravelSystem.Web.Models
{
    public class OsrmResponse
    {
        public List<OsrmRoute> Routes { get; set; } = [];
    }

    public class OsrmRoute
    {
        public OsrmGeometry Geometry { get; set; } = new();
    }

    public class OsrmGeometry
    {
        public List<double[]> Coordinates { get; set; } = [];
    }
}
