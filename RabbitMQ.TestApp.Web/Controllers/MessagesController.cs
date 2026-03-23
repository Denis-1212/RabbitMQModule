namespace RabbitMQ.TestApp.Web.Controllers;

using Microsoft.AspNetCore.Mvc;

using Models;

using Services;

[ApiController]
[Route("api/[controller]")]
public class MessagesController(
    IMessageService messageService,
    IMessageStore messageStore,
    ILogger<MessagesController> logger)
    : ControllerBase
{

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
            GenericMessage message = await messageService.SendMessageAsync(request);

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
            logger.LogError(ex, "Ошибка при отправке сообщения");
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
        IReadOnlyList<ProcessedMessage> messages = messageStore.GetRecent(Math.Min(count, 100));
        return Ok(messages);
    }

    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    [HttpGet("{id}")]
    public IActionResult GetMessage(string id)
    {
        ProcessedMessage? message = messageStore.GetById(id);

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
        messageStore.Clear();
        return NoContent();
    }

    #endregion

}
