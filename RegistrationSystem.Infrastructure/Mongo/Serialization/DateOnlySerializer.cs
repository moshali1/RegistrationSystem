using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace RegistrationSystem.Infrastructure.Mongo.Serialization;

public class DateOnlySerializer : StructSerializerBase<DateOnly>
{
    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        DateOnly value)
    {
        var dateTime = value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var millis = BsonUtils.ToMillisecondsSinceEpoch(dateTime);
        context.Writer.WriteDateTime(millis);
    }

    public override DateOnly Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args)
    {
        var millis = context.Reader.ReadDateTime();
        var dateTime = BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(millis);

        return DateOnly.FromDateTime(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
    }
}
