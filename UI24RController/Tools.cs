using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using System.Threading.Tasks;

namespace UI24RController
{
    public class MyClassTypeResolver<T> : DefaultJsonTypeInfoResolver
    {
        public static JsonSerializerOptions GetSerializerOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.TypeInfoResolver = new MyClassTypeResolver<T>();
            return options;
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

            Type basePointType = typeof(T);

            return jsonTypeInfo;
        }
    }
}
