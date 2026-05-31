using ModelContextProtocol.Server;
using AgentForge.Verticals.Travel.Services;

namespace AgentForge.Verticals.Travel.Tools;

[McpServerToolType]
public class BookingInquiryTools(BookingInquiryService bookingService, TourCatalogService catalog)
{
    [McpServerTool(Name = "create_booking_inquiry", ReadOnly = false)]
    [Description("Register a customer booking inquiry. Call this once you have gathered the customer's details.")]
    public string CreateBookingInquiry(
        [Description("Customer's WhatsApp phone number")] string phone,
        [Description("Customer's full name")] string customerName,
        [Description("Tour ID to book")] string tourId,
        [Description("Desired travel month in YYYY-MM format")] string travelMonth,
        [Description("Number of passengers")] int paxCount,
        [Description("Any special requests (e.g. vegetarian meals, wheelchair access, honeymoon decoration)")] string? specialRequests = null)
    {
        var tour = catalog.GetById(tourId);
        if (tour is null)
            return $"Cannot create inquiry: tour '{tourId}' not found. Please verify the tour ID.";

        var inquiry = bookingService.Create(phone, customerName, tourId, travelMonth, paxCount, specialRequests);

        return $"""
            ✅ *Booking Inquiry Registered!*
            Reference: *{inquiry.InquiryId}*
            Customer: {customerName}
            Tour: {tour.Name}
            Travel Month: {travelMonth} | Pax: {paxCount}
            {(specialRequests is not null ? $"Special Requests: {specialRequests}" : "")}

            Our team will contact you within 2 hours on this WhatsApp number to confirm availability and collect the 25% deposit. 🎉
            """;
    }

    [McpServerTool(Name = "get_customer_inquiries", ReadOnly = true)]
    [Description("Retrieve existing booking inquiries for a customer by their phone number.")]
    public string GetCustomerInquiries(
        [Description("Customer's WhatsApp phone number")] string phone)
    {
        var inquiries = bookingService.GetByPhone(phone);
        if (inquiries.Count == 0)
            return "No previous inquiries found for this number. I can help you plan a new trip! 😊";

        var lines = inquiries.Select(i =>
        {
            var tour = catalog.GetById(i.TourId);
            return $"• *{i.InquiryId}* — {tour?.Name ?? i.TourId} | {i.TravelMonth} | {i.PaxCount} pax | Created: {i.CreatedAt:dd MMM yyyy}";
        });
        return $"Found {inquiries.Count} inquiry(ies):\n\n{string.Join("\n", lines)}";
    }
}
