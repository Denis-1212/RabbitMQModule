namespace RabbitMQ.TestApp.Web.Controllers;

using Microsoft.AspNetCore.Mvc;

using Models;

using Services;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{

    #region Fields

    private readonly IMessageService _messageService;
    private readonly IMessageStore _messageStore;
    private readonly ILogger<MessagesController> _logger;

    #endregion

    #region Constructors

    public MessagesController(
        IMessageService messageService,
        IMessageStore messageStore,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _messageStore = messageStore;
        _logger = logger;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Отправить сообщение
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(
                new
                {
                    Error = "Text is required"
                });
        }

        try
        {
            GenericMessage message = await _messageService.SendMessageAsync(request);

            return Ok(
                new
                {
                    message.Id,
                    message.Text,
                    message.Sender,
                    message.Timestamp,
                    Status = "Sent"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке сообщения");
            return StatusCode(
                500,
                new
                {
                    Error = ex.Message
                });
        }
    }

    /// <summary>
    /// Получить историю обработанных сообщений
    /// </summary>
    [HttpGet]
    public IActionResult GetMessages([FromQuery] int count = 20)
    {
        IReadOnlyList<ProcessedMessage> messages = _messageStore.GetRecent(Math.Min(count, 100));
        return Ok(messages);
    }

    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    [HttpGet("{id}")]
    public IActionResult GetMessage(string id)
    {
        ProcessedMessage? message = _messageStore.GetById(id);

        if (message == null)
        {
            return NotFound();
        }

        return Ok(message);
    }

    /// <summary>
    /// Очистить историю
    /// </summary>
    [HttpDelete]
    public IActionResult ClearHistory()
    {
        _messageStore.Clear();
        return NoContent();
    }

    #endregion

}
