public static class ReflectionService
{
    public static void CopyFrom<TInput, TOutput>(this TInput output, TOutput input)
    {
        foreach (var sourceProperty in typeof(TOutput).GetProperties())
        {
            if (!sourceProperty.CanRead)
                continue;

            var targetProperty = typeof(TInput).GetProperties().FirstOrDefault(x =>
                x.Name == sourceProperty.Name && x.PropertyType.IsAssignableFrom(sourceProperty.PropertyType));
            if (targetProperty is null || !targetProperty.CanWrite || IsInitOnly(targetProperty))
                continue;

            targetProperty.SetValue(output, sourceProperty.GetValue(input));
        }
    }

    private static bool IsInitOnly(System.Reflection.PropertyInfo propertyInfo) => propertyInfo.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) ?? false;
    
    public static TOutput CreateFrom <TInput, TOutput> (this TInput input)
        where TOutput: class, new()
    {
        var created  = new TOutput();
        created.CopyFrom(input);
        return created;
    }
}
