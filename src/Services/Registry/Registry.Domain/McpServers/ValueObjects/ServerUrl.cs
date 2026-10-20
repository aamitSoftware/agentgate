namespace Registry.Domain.McpServers;

public sealed class ServerUrl : StringValueObject
{
    private ServerUrl(string value) => Value = value;

    public static ServerUrl Create(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException($"'{value}' is not an absolute http(s) URL.");
        return new ServerUrl(value);
    }
}
