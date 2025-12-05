using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Models;
using ThuYBinhDuongAPI.Services;
using System.Text.Json.Serialization;

namespace ThuYBinhDuongAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class KNNAnalysisController : ControllerBase
{
    private readonly ThuybinhduongContext _context;
    private readonly ILogger<KNNAnalysisController> _logger;
    private readonly ServiceRecommendationService _recommendationService;

    public KNNAnalysisController(
        ThuybinhduongContext context, 
        ILogger<KNNAnalysisController> logger,
        ServiceRecommendationService recommendationService)
    {
        _context = context;
        _logger = logger;
        _recommendationService = recommendationService;
    }

    // GET: api/KNNAnalysis/customers
    [HttpGet("customers")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<List<CustomerSummaryDto>>> GetCustomersForAnalysis()
    {
        try
        {
            var customers = await _context.Customers
                .Include(c => c.User)
                .Include(c => c.Pets)
                    .ThenInclude(p => p.Appointments)
                .Where(c => c.Pets.Any(p => p.Appointments.Any(a => a.Status == 2))) // Status 2 = Completed
                .ToListAsync();

            var result = customers.Select(c => new CustomerSummaryDto
            {
                CustomerId = c.CustomerId,
                CustomerName = c.CustomerName ?? "Unknown",
                Email = c.User?.Email ?? "",
                PhoneNumber = c.User?.PhoneNumber ?? "",
                TotalCompletedAppointments = c.Pets.SelectMany(p => p.Appointments).Count(a => a.Status == 2),
                LastAppointmentDate = c.Pets
                    .SelectMany(p => p.Appointments)
                    .Where(a => a.Status == 2)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Select(a => (DateTime?)a.AppointmentDate.ToDateTime(TimeOnly.MinValue))
                    .FirstOrDefault()
            })
            .OrderByDescending(c => c.TotalCompletedAppointments)
            .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customers for KNN analysis");
            return StatusCode(500, new { message = "Error retrieving customers", error = ex.Message });
        }
    }

    // GET: api/KNNAnalysis/analyze/{customerId}
    [HttpGet("analyze/{customerId}")]
    [AuthorizeRole(1)] // Admin only
    public async Task<ActionResult<KNNAnalysisResultDto>> AnalyzeCustomer(int customerId, [FromQuery] int k = 5)
    {
        try
        {
            // 1. Lấy thông tin khách hàng được chọn với appointments qua Pet
            var targetCustomer = await _context.Customers
                .Include(c => c.User)
                .Include(c => c.Pets)
                    .ThenInclude(p => p.Appointments.Where(a => a.Status == 2)) // Completed
                    .ThenInclude(a => a.Service)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (targetCustomer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            // Flatten appointments from all pets
            var targetAppointments = targetCustomer.Pets
                .SelectMany(p => p.Appointments)
                .ToList();

            // 2. Lấy tất cả khách hàng khác có lịch sử completed
            var allCustomers = await _context.Customers
                .Where(c => c.CustomerId != customerId)
                .Include(c => c.Pets)
                    .ThenInclude(p => p.Appointments.Where(a => a.Status == 2))
                    .ThenInclude(a => a.Service)
                .ToListAsync();

            // Filter customers that have at least one completed appointment
            allCustomers = allCustomers
                .Where(c => c.Pets.Any(p => p.Appointments.Any()))
                .ToList();

            // 3. Lấy danh sách tất cả services
            var allServices = await _context.Services.ToListAsync();
            var serviceIds = allServices.Select(s => s.ServiceId).ToList();

            // 4. Tạo vector cho target customer
            var targetVector = BuildServiceVector(targetAppointments, serviceIds);

            // 5. Tính similarity cho tất cả customers (sử dụng logic giống ServiceRecommendationService)
            var similarities = new List<CustomerSimilarityDto>();
            foreach (var customer in allCustomers)
            {
                var customerAppointments = customer.Pets.SelectMany(p => p.Appointments).ToList();
                var customerVector = BuildServiceVector(customerAppointments, serviceIds);
                var similarity = CalculateCosineSimilarity(targetVector, customerVector);

                if (similarity > 0.2) // Threshold giống ServiceRecommendationService
                {
                    similarities.Add(new CustomerSimilarityDto
                    {
                        CustomerId = customer.CustomerId,
                        CustomerName = customer.CustomerName ?? "Unknown",
                        SimilarityScore = similarity,
                        CommonServices = GetCommonServices(targetVector, customerVector, allServices, serviceIds),
                        TotalServices = customerAppointments
                            .Select(a => a.ServiceId)
                            .Distinct()
                            .Count(),
                        ServiceVector = customerVector.Select((v, idx) => new ServiceVectorItem
                        {
                            ServiceId = serviceIds[idx],
                            ServiceName = allServices.FirstOrDefault(s => s.ServiceId == serviceIds[idx])?.Name ?? "Unknown",
                            Count = v
                        }).Where(s => s.Count > 0).ToList()
                    });
                }
            }

            // 6. Lấy top K similar customers
            var topKSimilar = similarities
                .OrderByDescending(s => s.SimilarityScore)
                .Take(k)
                .ToList();

            // 7. Gọi ServiceRecommendationService để lấy recommendations (bao gồm cả dịch vụ đã dùng)
            var knnRecommendations = await _recommendationService.GetKNNRecommendationsForCustomer(customerId, k);

            // Kiểm tra dịch vụ nào đã được target customer sử dụng
            var usedServiceIds = targetAppointments
                .Select(a => a.ServiceId)
                .Distinct()
                .ToHashSet();

            // 8. Map recommendations với thông tin từ similar customers
            var recommendations = knnRecommendations.Select(rec =>
            {
                var recommendedByDetails = topKSimilar
                    .Select(sim =>
                    {
                        var customer = allCustomers.First(c => c.CustomerId == sim.CustomerId);
                        var usageCount = customer.Pets
                            .SelectMany(p => p.Appointments)
                            .Count(a => a.ServiceId == rec.ServiceId && a.Status == 2);
                        
                        return new
                        {
                            sim.CustomerName,
                            sim.SimilarityScore,
                            UsageCount = usageCount
                        };
                    })
                    .Where(x => x.UsageCount > 0)
                    .ToList();

                var recommendedBy = recommendedByDetails
                    .Select(x => $"{x.CustomerName} (similarity: {x.SimilarityScore:F3}, usage: {x.UsageCount})")
                    .ToList();

                var isUsed = usedServiceIds.Contains(rec.ServiceId);
                var targetUsageCount = targetAppointments.Count(a => a.ServiceId == rec.ServiceId);

                return new ServiceRecommendationDetailDto
                {
                    ServiceId = rec.ServiceId,
                    ServiceName = rec.ServiceName,
                    ServiceDescription = rec.Description ?? "",
                    RecommendationScore = rec.RecommendationScore,
                    RecommendedByCount = recommendedBy.Count,
                    RecommendedBy = recommendedBy,
                    RecommendedByDetails = recommendedByDetails.Select(x => new RecommendedByDetail
                    {
                        CustomerName = x.CustomerName,
                        SimilarityScore = x.SimilarityScore,
                        UsageCount = x.UsageCount
                    }).ToList(),
                    Reason = isUsed 
                        ? $"{rec.Reason} (Đã sử dụng {targetUsageCount} lần)"
                        : rec.Reason
                };
            }).ToList();

            // 9. Tạo result
            var result = new KNNAnalysisResultDto
            {
                TargetCustomer = new CustomerDetailDto
                {
                    CustomerId = targetCustomer.CustomerId,
                    CustomerName = targetCustomer.CustomerName ?? "Unknown",
                    Email = targetCustomer.User?.Email ?? "",
                    PhoneNumber = targetCustomer.User?.PhoneNumber ?? "",
                    CompletedAppointments = targetAppointments.Count,
                    UniqueServices = targetAppointments.Select(a => a.ServiceId).Distinct().Count(),
                    ServiceHistory = targetAppointments
                        .GroupBy(a => a.Service)
                        .Select(g => new ServiceUsageDto
                        {
                            ServiceId = g.Key?.ServiceId ?? 0,
                            ServiceName = g.Key?.Name ?? "Unknown",
                            UsageCount = g.Count(),
                            LastUsedDate = g.Max(a => (DateTime?)a.AppointmentDate.ToDateTime(TimeOnly.MinValue))
                        })
                        .OrderByDescending(s => s.UsageCount)
                        .ToList(),
                    ServiceVector = targetVector.Select((v, idx) => new ServiceVectorItem
                    {
                        ServiceId = serviceIds[idx],
                        ServiceName = allServices.FirstOrDefault(s => s.ServiceId == serviceIds[idx])?.Name ?? "Unknown",
                        Count = v
                    }).Where(s => s.Count > 0).ToList()
                },
                K = k,
                TotalCustomersAnalyzed = allCustomers.Count,
                SimilarCustomers = topKSimilar,
                Recommendations = recommendations,
                CalculationDetails = new KNNCalculationDetailsDto
                {
                    VectorSize = serviceIds.Count,
                    CosineSimilarityThreshold = 0.2,
                    AlgorithmDescription = "KNN with Cosine Similarity (threshold 0.2) - finds K most similar customers based on service usage patterns. Uses ServiceRecommendationService for recommendation logic."
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing customer {CustomerId}", customerId);
            return StatusCode(500, new { message = "Error analyzing customer", error = ex.Message });
        }
    }

    // Helper: Build service usage vector
    private List<int> BuildServiceVector(List<Appointment> appointments, List<int> allServiceIds)
    {
        var serviceCount = appointments
            .Where(a => a.ServiceId > 0) // ServiceId is int, not nullable
            .GroupBy(a => a.ServiceId)
            .ToDictionary(g => g.Key, g => g.Count());

        return allServiceIds.Select(sid => serviceCount.GetValueOrDefault(sid, 0)).ToList();
    }

    // Helper: Calculate cosine similarity
    private double CalculateCosineSimilarity(List<int> vector1, List<int> vector2)
    {
        if (vector1.Count != vector2.Count) return 0;

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Count; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0) return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    // Helper: Get common services between two customers
    private List<string> GetCommonServices(List<int> vector1, List<int> vector2, List<Service> allServices, List<int> serviceIds)
    {
        var common = new List<string>();
        for (int i = 0; i < vector1.Count && i < vector2.Count; i++)
        {
            if (vector1[i] > 0 && vector2[i] > 0)
            {
                var serviceId = serviceIds[i];
                var service = allServices.FirstOrDefault(s => s.ServiceId == serviceId);
                if (service != null)
                {
                    common.Add(service.Name ?? "Unknown");
                }
            }
        }
        return common;
    }
}

// DTOs
public class CustomerSummaryDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int TotalCompletedAppointments { get; set; }
    public DateTime? LastAppointmentDate { get; set; }
}

public class KNNAnalysisResultDto
{
    public CustomerDetailDto TargetCustomer { get; set; } = new();
    public int K { get; set; }
    public int TotalCustomersAnalyzed { get; set; }
    public List<CustomerSimilarityDto> SimilarCustomers { get; set; } = new();
    public List<ServiceRecommendationDetailDto> Recommendations { get; set; } = new();
    public KNNCalculationDetailsDto CalculationDetails { get; set; } = new();
}

public class CustomerDetailDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int CompletedAppointments { get; set; }
    public int UniqueServices { get; set; }
    public List<ServiceUsageDto> ServiceHistory { get; set; } = new();
    public List<ServiceVectorItem> ServiceVector { get; set; } = new();
}

public class ServiceUsageDto
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime? LastUsedDate { get; set; }
}

public class CustomerSimilarityDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public List<string> CommonServices { get; set; } = new();
    public int TotalServices { get; set; }
    public List<ServiceVectorItem> ServiceVector { get; set; } = new();
}

public class ServiceVectorItem
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ServiceRecommendationDetailDto
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceDescription { get; set; } = string.Empty;
    public double RecommendationScore { get; set; }
    public int RecommendedByCount { get; set; }
    public List<string> RecommendedBy { get; set; } = new();
    public List<RecommendedByDetail> RecommendedByDetails { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public class RecommendedByDetail
{
    public string CustomerName { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public int UsageCount { get; set; }
}

public class KNNCalculationDetailsDto
{
    public int VectorSize { get; set; }
    public double CosineSimilarityThreshold { get; set; }
    public string AlgorithmDescription { get; set; } = string.Empty;
}
