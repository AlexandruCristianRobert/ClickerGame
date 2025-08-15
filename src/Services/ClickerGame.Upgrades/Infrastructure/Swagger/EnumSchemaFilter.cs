using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;

namespace ClickerGame.Upgrades.Infrastructure.Swagger
{
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                schema.Enum.Clear();
                var enumNames = Enum.GetNames(context.Type);
                var enumValues = Enum.GetValues(context.Type);

                for (int i = 0; i < enumNames.Length; i++)
                {
                    var enumName = enumNames[i];
                    var enumValue = (int)enumValues.GetValue(i)!;

                    // Get description from DescriptionAttribute if available
                    var field = context.Type.GetField(enumName);
                    var descriptionAttribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .Cast<DescriptionAttribute>()
                        .FirstOrDefault();

                    var description = descriptionAttribute?.Description ?? enumName;

                    schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString($"{enumValue} - {enumName}: {description}"));
                }
            }
        }
    }
}