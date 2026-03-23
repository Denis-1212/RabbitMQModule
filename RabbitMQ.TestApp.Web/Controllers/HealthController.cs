namespace RabbitMQ.TestApp.Web.Controllers;

using Microsoft.AspNetCore.Mvc;

using Module;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{

    #region Fields

    private readonly MessagingModule _module;
    private readonly ILogger<HealthController> _logger;

    #endregion

    #region Constructors

    public HealthController(MessagingModule module, ILogger<HealthController> logger)
    {
        _module = module;
        _logger = logger;
    }

    #endregion

    #region Methods

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Проверяем, что модуль не disposed
            if (_module == null)
            {
                return StatusCode(
                    503,
                    new
                    {
                        Status = "Unhealthy",
                        Reason = "Module not initialized"
                    });
            }

            // Простая проверка (можно добавить проверку соединения)
            return Ok(
                new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Queue = "webapp.messages"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(
                503,
                new
                {
                    Status = "Unhealthy",
                    Reason = ex.Message
                });
        }
    }

    #endregion

}
