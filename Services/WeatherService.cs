using System.Text.Json;

namespace ThuYBinhDuongAPI.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeatherService> _logger;
        private readonly string? _apiKey;

        public WeatherService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<WeatherService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["WeatherApi:ApiKey"]; // OpenWeatherMap API key
        }

        /// <summary>
        /// Lấy thông tin thời tiết hiện tại cho Bình Dương
        /// </summary>
        public async Task<WeatherInfo?> GetCurrentWeatherAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    // Fallback: Ước tính dựa trên tháng
                    _logger.LogInformation("Weather API key not configured, using estimated weather");
                    return GetEstimatedWeather();
                }

                // Bình Dương coordinates: 10.9794° N, 106.6507° E
                var url = $"https://api.openweathermap.org/data/2.5/weather?lat=10.9794&lon=106.6507&appid={_apiKey}&units=metric&lang=vi";
                
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Weather API returned error, using estimated weather");
                    return GetEstimatedWeather();
                }

                var json = await response.Content.ReadAsStringAsync();
                var weatherData = JsonSerializer.Deserialize<OpenWeatherResponse>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (weatherData == null || weatherData.Weather == null || weatherData.Weather.Count == 0)
                {
                    return GetEstimatedWeather();
                }

                var weatherCondition = weatherData.Weather[0].Main?.ToLower() ?? "clear";
                var isRainy = weatherCondition.Contains("rain") || 
                             weatherCondition.Contains("drizzle") ||
                             weatherCondition.Contains("storm");

                return new WeatherInfo
                {
                    Temperature = weatherData.Main?.Temp ?? 0,
                    Condition = DetermineConditionFromWeather(weatherCondition),
                    Description = weatherData.Weather[0].Description ?? "",
                    Humidity = weatherData.Main?.Humidity ?? 0,
                    IsRainy = isRainy,
                    Season = DetermineSeason(DateTime.Now)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch weather, using estimated weather");
                return GetEstimatedWeather();
            }
        }

        private WeatherInfo GetEstimatedWeather()
        {
            var now = DateTime.Now;
            var month = now.Month;
            
            return new WeatherInfo
            {
                Temperature = month >= 3 && month <= 5 ? 30 : 
                             month >= 6 && month <= 8 ? 28 : 
                             month >= 9 && month <= 11 ? 26 : 24,
                Condition = DetermineCondition(month),
                Description = DetermineDescription(month),
                Humidity = month >= 5 && month <= 10 ? 80 : 60,
                IsRainy = month >= 5 && month <= 10, // Mùa mưa
                Season = DetermineSeason(now)
            };
        }

        private string DetermineCondition(int month)
        {
            if (month >= 5 && month <= 10) return "rainy";
            if (month >= 11 || month <= 2) return "cold";
            return "hot";
        }

        private string DetermineConditionFromWeather(string weatherCondition)
        {
            if (weatherCondition.Contains("rain") || weatherCondition.Contains("drizzle") || weatherCondition.Contains("storm"))
                return "rainy";
            if (weatherCondition.Contains("snow") || weatherCondition.Contains("cold"))
                return "cold";
            return "hot";
        }

        private string DetermineDescription(int month)
        {
            if (month >= 5 && month <= 10) return "Mùa mưa";
            if (month >= 11 || month <= 2) return "Mùa lạnh";
            return "Mùa nóng";
        }

        private string DetermineSeason(DateTime date)
        {
            var month = date.Month;
            if (month >= 5 && month <= 10) return "rainy";
            if (month >= 11 || month <= 2) return "cold";
            return "hot";
        }
    }

    public class WeatherInfo
    {
        public double Temperature { get; set; }
        public string Condition { get; set; } = "normal"; // rainy, hot, cold, normal
        public string Description { get; set; } = "";
        public int Humidity { get; set; }
        public bool IsRainy { get; set; }
        public string Season { get; set; } = "normal"; // rainy, hot, cold, normal
    }

    // OpenWeatherMap API Response Models
    internal class OpenWeatherResponse
    {
        public MainInfo? Main { get; set; }
        public List<Weather>? Weather { get; set; }
    }

    internal class MainInfo
    {
        public double Temp { get; set; }
        public int Humidity { get; set; }
    }

    internal class Weather
    {
        public string? Main { get; set; }
        public string? Description { get; set; }
    }
}

