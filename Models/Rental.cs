using System;

namespace CarRentalAPI.Models
{
    public class Rental
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int CarId { get; set; }
        public DateTime RentalStart { get; set; }
        public DateTime? RentalEnd { get; set; } // Null betyr at leieforholdet er aktivt

        // Navigasjonsegenskaper 
        // (brukes for å hente tilhørende objekter fra databasen)
        //? means nullable
        public Customer? Customer { get; set; }
        public Car? Car { get; set; }
    }
}
