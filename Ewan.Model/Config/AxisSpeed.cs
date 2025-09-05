namespace Ewan.Model.Config
{
    public class AxisSpeed
    {
        public string SpeedName { get; set; } = "HighSpd";
        public double Jerk { get; set; } = 500000;
        public double MaxSpeed { get; set; } = 1000;
        public double MinSpeed { get; set; } = 800;
        public double Acc { get; set; } = 6500;
        public double Dec { get; set; } = 6500;
    }
}
