namespace Reported.Models;

public sealed record ReportGroup(
    string GroupKey,
    string DisplayName,
    int Count);
