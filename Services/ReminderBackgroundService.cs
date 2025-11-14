using ThuYBinhDuongAPI.Services;

namespace ThuYBinhDuongAPI.Services
{
    /// <summary>
    /// Background service để tự động kiểm tra và gửi reminders
    /// Chạy mỗi ngày lúc 8:00 sáng để gửi nhắc hẹn
    /// </summary>
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReminderBackgroundService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(24); // Chạy mỗi 24 giờ
        private readonly TimeSpan _checkTime = new TimeSpan(8, 0, 0); // 8:00 sáng

        public ReminderBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ReminderBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReminderBackgroundService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var nextRun = GetNextRunTime(now);
                    var delay = nextRun - now;

                    _logger.LogInformation($"Next reminder check scheduled for: {nextRun:yyyy-MM-dd HH:mm:ss}");

                    // Chờ đến giờ chạy tiếp theo
                    await Task.Delay(delay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await CheckAndSendReminders();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ReminderBackgroundService is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReminderBackgroundService");
                    
                    // Nếu có lỗi, chờ 1 giờ rồi thử lại
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("ReminderBackgroundService stopped");
        }

        private DateTime GetNextRunTime(DateTime now)
        {
            var today = now.Date.Add(_checkTime);
            
            if (now < today)
            {
                // Nếu chưa đến 8:00 sáng hôm nay
                return today;
            }
            else
            {
                // Nếu đã qua 8:00 sáng hôm nay, chạy vào 8:00 sáng ngày mai
                return today.AddDays(1);
            }
        }

        private async Task CheckAndSendReminders()
        {
            try
            {
                _logger.LogInformation("Starting automatic reminder check at {Time}", DateTime.Now);

                using var scope = _serviceProvider.CreateScope();
                var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();

                var sentCount = await reminderService.CheckAndSendRemindersAsync();

                _logger.LogInformation("Automatic reminder check completed. Sent {Count} reminders", sentCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic reminder check");
            }
        }
    }
}
