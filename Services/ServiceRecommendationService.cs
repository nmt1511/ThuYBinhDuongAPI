using Microsoft.EntityFrameworkCore;
using ThuYBinhDuongAPI.Models;
using ThuYBinhDuongAPI.Data.Dtos;

namespace ThuYBinhDuongAPI.Services
{
    public class ServiceRecommendationService
    {
        private readonly ThuybinhduongContext _context;
        private readonly ILogger<ServiceRecommendationService> _logger;

        public ServiceRecommendationService(
            ThuybinhduongContext context,
            ILogger<ServiceRecommendationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// KNN Algorithm để tìm khách hàng tương tự và đề xuất dịch vụ
        /// </summary>
        public async Task<List<ServiceRecommendationDto>> GetKNNRecommendationsForCustomer(
            int customerId, 
            int k = 5)
        {
            try
            {
                // 1. Lấy lịch sử sử dụng dịch vụ của khách hàng hiện tại
                var currentCustomerHistory = await GetCustomerServiceHistory(customerId);
                
                if (currentCustomerHistory.Count == 0)
                {
                    // Nếu chưa có lịch sử, đề xuất dịch vụ phổ biến nhất
                    return await GetPopularServices(10);
                }

                // 2. Lấy tất cả khách hàng khác
                var allCustomers = await _context.Customers
                    .Where(c => c.CustomerId != customerId)
                    .Select(c => c.CustomerId)
                    .ToListAsync();

                var customerSimilarities = new List<(int CustomerId, double Similarity)>();

                // 3. Tính similarity với từng khách hàng khác
                foreach (var otherCustomerId in allCustomers)
                {
                    var otherHistory = await GetCustomerServiceHistory(otherCustomerId);
                    
                    if (otherHistory.Count == 0) continue;

                    // Tính Cosine Similarity
                    var similarity = CalculateCosineSimilarity(
                        currentCustomerHistory, 
                        otherHistory
                    );

                    if (similarity > 0.2) // Threshold
                    {
                        customerSimilarities.Add((otherCustomerId, similarity));
                    }
                }

                // 4. Tìm k khách hàng gần nhất (KNN)
                var nearestNeighbors = customerSimilarities
                    .OrderByDescending(x => x.Similarity)
                    .Take(k)
                    .ToList();

                if (nearestNeighbors.Count == 0)
                {
                    return await GetPopularServices(10);
                }

                // 5. Lấy dịch vụ từ k neighbors mà khách hàng hiện tại chưa dùng
                var recommendedServiceIds = new Dictionary<int, double>(); // ServiceId -> Total Score
                var currentCustomerUsedServices = currentCustomerHistory
                    .Select(h => h.ServiceId)
                    .ToHashSet();

                foreach (var (neighborId, similarity) in nearestNeighbors)
                {
                    var neighborServices = await GetCustomerServiceHistory(neighborId);
                    
                    foreach (var service in neighborServices)
                    {
                        if (!currentCustomerUsedServices.Contains(service.ServiceId))
                        {
                            // Tính điểm: similarity * usage frequency
                            var score = similarity * service.UsageCount;
                            
                            if (recommendedServiceIds.ContainsKey(service.ServiceId))
                            {
                                recommendedServiceIds[service.ServiceId] += score; 
                            }
                            else
                            {
                                recommendedServiceIds[service.ServiceId] = score;
                            }
                        }
                    }
                }

                // 6. Lấy thông tin dịch vụ được đề xuất
                var recommendations = await _context.Services
                    .Where(s => recommendedServiceIds.Keys.Contains(s.ServiceId) && 
                               s.IsActive == true)
                    .Select(s => new ServiceRecommendationDto
                    {
                        ServiceId = s.ServiceId,
                        ServiceName = s.Name,
                        Description = s.Description,
                        Price = s.Price,
                        Category = s.Category,
                        RecommendationScore = (int)recommendedServiceIds[s.ServiceId],
                        Reason = $"Được đề xuất dựa trên hành vi của {nearestNeighbors.Count} khách hàng tương tự"
                    })
                    .OrderByDescending(r => r.RecommendationScore)
                    .Take(10)
                    .ToListAsync();

                return recommendations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in KNN recommendation for customer {CustomerId}", customerId);
                return await GetPopularServices(10);
            }
        }

        private async Task<List<CustomerServiceHistory>> GetCustomerServiceHistory(int customerId)
        {
            var appointments = await _context.Appointments
                .Include(a => a.Pet)
                    .ThenInclude(p => p.Customer)
                .Include(a => a.Service)
                .Where(a => a.Pet.CustomerId == customerId && a.Status == 2) // Completed
                .ToListAsync();

            return appointments
                .GroupBy(a => a.ServiceId)
                .Select(g => new CustomerServiceHistory
                {
                    ServiceId = g.Key,
                    UsageCount = g.Count(),
                    LastUsed = g.Max(a => a.AppointmentDate),
                    AvgDaysBetween = CalculateAvgDaysBetween(
                        g.Select(a => a.AppointmentDate).OrderBy(d => d).ToList()
                    )
                })
                .ToList();
        }

        private double CalculateCosineSimilarity(
            List<CustomerServiceHistory> history1,
            List<CustomerServiceHistory> history2)
        {
            // Tạo vector từ tất cả service IDs
            var allServiceIds = history1.Select(h => h.ServiceId)
                .Union(history2.Select(h => h.ServiceId))
                .Distinct()
                .ToList();

            var vector1 = allServiceIds.Select(serviceId =>
            {
                var item = history1.FirstOrDefault(h => h.ServiceId == serviceId);
                return item?.UsageCount ?? 0;
            }).ToArray();

            var vector2 = allServiceIds.Select(serviceId =>
            {
                var item = history2.FirstOrDefault(h => h.ServiceId == serviceId);
                return item?.UsageCount ?? 0;
            }).ToArray();

            // Cosine Similarity
            var dotProduct = vector1.Zip(vector2, (a, b) => a * b).Sum();
            var magnitude1 = Math.Sqrt(vector1.Sum(x => x * x));
            var magnitude2 = Math.Sqrt(vector2.Sum(x => x * x));

            if (magnitude1 == 0 || magnitude2 == 0)
                return 0;

            return dotProduct / (magnitude1 * magnitude2);
        }

        private double? CalculateAvgDaysBetween(List<DateOnly> dates)
        {
            if (dates.Count < 2) return null;
            
            var intervals = new List<int>();
            for (int i = 1; i < dates.Count; i++)
            {
                intervals.Add((dates[i].ToDateTime(TimeOnly.MinValue) - 
                              dates[i-1].ToDateTime(TimeOnly.MinValue)).Days);
            }
            
            return intervals.Average();
        }

        private async Task<List<ServiceRecommendationDto>> GetPopularServices(int limit)
        {
            var appointments = await _context.Appointments
                .Include(a => a.Service)
                .Where(a => a.Status == 2)
                .ToListAsync();

            return appointments
                .GroupBy(a => a.ServiceId)
                .OrderByDescending(g => g.Count())
                .Take(limit)
                .Select(g => new ServiceRecommendationDto
                {
                    ServiceId = g.Key,
                    ServiceName = g.First().Service.Name,
                    Description = g.First().Service.Description,
                    Price = g.First().Service.Price,
                    Category = g.First().Service.Category,
                    RecommendationScore = g.Count(),
                    Reason = "Dịch vụ phổ biến"
                })
                .ToList();
        }

        private class CustomerServiceHistory
        {
            public int ServiceId { get; set; }
            public int UsageCount { get; set; }
            public DateOnly LastUsed { get; set; }
            public double? AvgDaysBetween { get; set; }
        }
    }

    public class ServiceRecommendationDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? Category { get; set; }
        public int RecommendationScore { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}

