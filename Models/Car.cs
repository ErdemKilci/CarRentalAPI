namespace CarRentalAPI.Models
{
    public class Car
    {
        public int Id { get; set; }  // Auto-generert unik ID

        //string? making it nullable or use "required" attribute
        //= string.Empty; to set default value
        public required string Model { get; set; }
    }
}
