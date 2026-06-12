namespace CandidateAttendanceApp.ViewModels;

public class PaginatedList<T>
{
    public List<T> Items { get; }
    public int CurrentPage { get; }
    public int TotalPages { get; }
    public int PageSize { get; }
    public int TotalRecords { get; }

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public PaginatedList(List<T> items, int totalRecords, int page, int pageSize)
    {
        TotalRecords = totalRecords;
        PageSize = pageSize;
        TotalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);
        CurrentPage = Math.Max(1, Math.Min(page, Math.Max(1, TotalPages)));
        Items = items;
    }

    public static PaginatedList<T> Create(List<T> source, int page, int pageSize = 10)
    {
        var totalRecords = source.Count;
        var totalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);
        var normalizedPage = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
        var items = source.Skip((normalizedPage - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedList<T>(items, totalRecords, normalizedPage, pageSize);
    }
}
