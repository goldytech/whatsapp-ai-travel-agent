namespace AgentForge.Verticals.Travel.Models;

public record BookingInquiry(
    string InquiryId, string Phone, string CustomerName,
    string TourId, string TravelMonth, int PaxCount,
    string? SpecialRequests, DateTime CreatedAt
);
