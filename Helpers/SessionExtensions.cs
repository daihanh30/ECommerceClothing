using System.Text.Json; // Dùng thư viện có sẵn, không cần cài thêm
using Microsoft.AspNetCore.Http;

namespace ECommerceClothing.Helpers
{
    public static class SessionExtensions
    { 
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }
         
        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}