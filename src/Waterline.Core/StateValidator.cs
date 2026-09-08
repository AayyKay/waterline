namespace Waterline.Core;

public static class StateValidator
{
    private static readonly HashSet<int> SupportedIntervals = [30, 45, 60, 90, 120];

    public static IReadOnlyList<string> Validate(WaterlineState state)
    {
        var errors = new List<string>();
        if (state.SchemaVersion is <= 0 or > StateSchema.CurrentVersion)
            errors.Add($"Unsupported schema version {state.SchemaVersion}.");
        if (state.Settings is null)
        {
            errors.Add("Settings are required.");
        }
        else
        {
            if (!double.IsFinite(state.Settings.DailyGoalOz) || state.Settings.DailyGoalOz is < 8 or > 512)
                errors.Add("Daily goal must be between 8 and 512 oz.");
            if (!SupportedIntervals.Contains(state.Settings.ReminderIntervalMinutes))
                errors.Add("Reminder interval is not supported.");
            if (state.Settings.WorkdayStart < TimeSpan.Zero ||
                state.Settings.WorkdayEnd > TimeSpan.FromDays(1) ||
                state.Settings.WorkdayStart >= state.Settings.WorkdayEnd)
                errors.Add("Workday schedule must be a valid same-day range.");
            if (state.Settings.ReminderDays is null || state.Settings.ReminderDays.Any(day => !Enum.IsDefined(day)))
                errors.Add("Reminder days contain an invalid value.");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        if (state.Drinks is null)
        {
            errors.Add("The drink collection is required.");
        }
        else
        {
            foreach (var entry in state.Drinks.Cast<DrinkEntry?>())
            {
                if (entry is null)
                {
                    errors.Add("Drink entries cannot be null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.Id) || !identifiers.Add(entry.Id))
                    errors.Add("Drink identifiers must be present and unique.");
                if (!IsValidAmount(entry.AmountOz))
                    errors.Add($"Drink {entry.Id} has an invalid amount.");
            }
        }
        if (state.Desktop is null) errors.Add("Desktop state is required.");
        if (state.Runtime is null) errors.Add("Runtime state is required.");
        return errors;
    }

    public static bool IsValidAmount(double amountOz) =>
        double.IsFinite(amountOz) && Math.Round(amountOz, 1) is >= .1 and <= 64;
}
