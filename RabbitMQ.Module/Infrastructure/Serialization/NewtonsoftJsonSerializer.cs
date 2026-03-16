namespace RabbitMQ.Module.Infrastructure.Serialization;

using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

public class NewtonsoftJsonSerializer : IMessageSerializer
{

    #region Fields

    private readonly JsonSerializerSettings _settings;

    #endregion

    #region Constructors

    public NewtonsoftJsonSerializer()
    {
        _settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto, // Важно для сохранения типов
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        _settings.Converters.Add(new StringEnumConverter());
    }

    #endregion

    #region Methods

    public byte[] Serialize<T>(T obj)
    {
        string json = JsonConvert.SerializeObject(obj, _settings);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json, _settings)!;
    }

    public object Deserialize(byte[] data, Type type)
    {
        string json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject(json, type, _settings)!;
    }

    #endregion

}
