namespace Waha.McpServer.Services;

using System.Collections.Concurrent;
using Waha.McpServer.Models;

public class BookingInquiryService
{
    private readonly ConcurrentDictionary<string, BookingInquiry> _inquiries = new();
    private int _counter = 1000;

    public BookingInquiry Create(string phone, string customerName, string tourId,
        string travelMonth, int paxCount, string? specialRequests)
    {
        var id = $"RJ{Interlocked.Increment(ref _counter)}";
        var inquiry = new BookingInquiry(id, phone, customerName, tourId, travelMonth,
            paxCount, specialRequests, DateTime.UtcNow);
        _inquiries[id] = inquiry;
        return inquiry;
    }

    public IReadOnlyList<BookingInquiry> GetByPhone(string phone) =>
        _inquiries.Values
            .Where(i => string.Equals(i.Phone, phone, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
}
