namespace CarRentalAPI.Models
{
    public class Customer
    {
        public int Id { get; set; }  // Auto-generert unik ID
        public required string Name { get; set; } = string.Empty;
    }
}
