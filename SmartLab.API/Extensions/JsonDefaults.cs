using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SmartLab.API.Extensions;

/// <summary>JSON settings for responses written outside MVC (middleware, auth events, health): camelCase, Vietnamese unescaped.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
}
