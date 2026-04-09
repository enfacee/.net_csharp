public static class ReflectionService
{
    public static void CopyFrom<TIn, TOut>(this TIn output, TOut input)
    {
        foreach (var sourceProperty in typeof(TOut).GetProperties())
        {
            if (!sourceProperty.CanRead)
                continue;

            var targetProperty = typeof(TIn).GetProperties().FirstOrDefault(x =>
                x.Name == sourceProperty.Name && x.PropertyType.IsAssignableFrom(sourceProperty.PropertyType));
            if (targetProperty is null || !targetProperty.CanWrite || IsInitOnly(targetProperty))
                continue;

            targetProperty.SetValue(output, sourceProperty.GetValue(input));
        }
    }

    private static bool IsInitOnly(System.Reflection.PropertyInfo propertyInfo) => propertyInfo.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) ?? false;
}
