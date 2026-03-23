namespace RabbitMQ.TestApp.Web.Services;

using Models;

public interface IMessageStore
{

    #region Methods

    void Add(ProcessedMessage message);
    IReadOnlyList<ProcessedMessage> GetRecent(int count);
    ProcessedMessage? GetById(string id);
    void Clear();

    #endregion

}
