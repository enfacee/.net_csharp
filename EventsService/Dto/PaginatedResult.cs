public class PaginatedResult<T>
{
    public int TotalCount { get; set; }
    public T[] Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
}
