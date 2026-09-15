namespace KitchenFlow.Api.Models
{
    public class Machine
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;      // 신규
        public int CapacityMl { get; set; }                   // 신규
        public int MinTempC { get; set; }                     // 신규
        public int MaxTempC { get; set; }                     // 신규
        public bool IsAvailable { get; set; } = true;         // 신규: 고장/점검 중이면 false
    }
}
