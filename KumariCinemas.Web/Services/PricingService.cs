namespace KumariCinemas.Web.Services
{
    public interface IPricingService
    {
        decimal CalculatePrice(decimal basePrice, DateTime showDate, bool isNewRelease);
    }

    public class PricingService : IPricingService
    {
        public decimal CalculatePrice(decimal basePrice, DateTime showDate, bool isNewRelease)
        {
            decimal finalPrice = basePrice;

            // 1. Check for New Release (Add premium, e.g., 20%)
            if (isNewRelease)
            {
                finalPrice += (basePrice * 0.20m);
            }

            // 2. Check for Public Holidays (Add premium, e.g., 15%)
            if (IsPublicHoliday(showDate))
            {
                finalPrice += (basePrice * 0.15m);
            }

            return finalPrice;
        }

        private bool IsPublicHoliday(DateTime date)
        {
            // Default Holidays logic
            var holidays = new List<(int Month, int Day)>
            {
                (1, 1),   // New Year's Day
                (5, 1),   // Workers' Day
                (12, 25), // Christmas
                // Add logic for Dashain/Tihar if specific dates were provided
            };

            return holidays.Any(h => h.Month == date.Month && h.Day == date.Day);
        }
    }
}
