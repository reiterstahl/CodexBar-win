namespace CodexBar.Windows.Tray;

public enum AccountSortOrder
{
    Original,
    AvailableFirst,
    MostQuota,
}

/// <summary>Actual remaining quota, never the exhausted meter's reset-wait percentage.</summary>
public readonly record struct AccountQuotaRank(int Group, double LimitingRemaining, double OtherRemaining)
{
    public static AccountQuotaRank FromRemaining(double? session, double? weekly, bool hasError = false)
    {
        double[] known = new[] { session, weekly }
            .Where(value => value.HasValue && double.IsFinite(value.Value))
            .Select(value => Math.Clamp(value!.Value, 0, 100))
            .ToArray();
        if (hasError || known.Length == 0)
        {
            return new AccountQuotaRank(hasError ? 3 : 2, 0, 0);
        }

        double limiting = known.Min();
        return new AccountQuotaRank(limiting <= 0 ? 1 : 0, limiting, known.Max());
    }
}

/// <summary>Stable, provider-independent ordering shared with cross-platform tests.</summary>
public static class AccountOrdering
{
    public static AccountSortOrder ResolveOrder(string? order, bool legacyAvailableFirst)
    {
        return ThemeCatalog.ParseOption(order,
            legacyAvailableFirst ? AccountSortOrder.AvailableFirst : AccountSortOrder.Original);
    }

    public static IEnumerable<T> Order<T>(
        IEnumerable<T> accounts,
        AccountSortOrder order,
        Func<T, AccountQuotaRank> quota)
    {
        if (order == AccountSortOrder.Original)
        {
            return accounts;
        }

        var ranked = accounts.Select(account => (Account: account, Rank: quota(account)));
        var availableFirst = ranked.OrderBy(item => item.Rank.Group);
        return (order == AccountSortOrder.MostQuota
            ? availableFirst.ThenByDescending(item => item.Rank.LimitingRemaining)
                .ThenByDescending(item => item.Rank.OtherRemaining)
            : availableFirst)
            .Select(item => item.Account);
    }
}
