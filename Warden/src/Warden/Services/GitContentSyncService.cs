using System.Diagnostics;
using System.Text;
using Warden.Configuration;

namespace Warden.Services;

// opt-in (Git:Enabled): clones content/ from Git:Url on startup if it isn't a checkout yet, then
// `git pull --ff-only` on the Git:Cron schedule. Credentials (Git:Username/Password) are passed to git
// via an env-provided http.extraheader, never embedded in the remote URL or a process argument.
public sealed class GitContentSyncService(GitSyncOptions options, string contentRoot, ILogger<GitContentSyncService> logger) : BackgroundService
{
    private const int PullTimeoutSeconds = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        var isCheckout = Directory.Exists(Path.Combine(contentRoot, ".git"));
        if (!isCheckout)
        {
            if (string.IsNullOrWhiteSpace(options.Url))
            {
                logger.LogWarning("Git:Enabled is true but {ContentRoot} has no .git folder and Git:Url is not set; git sync is disabled", contentRoot);
                return;
            }

            if (!await RunGitAsync(["clone", options.Url, contentRoot], Path.GetTempPath(), stoppingToken))
            {
                logger.LogWarning("git clone of {Url} into {ContentRoot} failed; git sync is disabled", options.Url, contentRoot);
                return;
            }
        }

        if (!CronSchedule.TryParse(options.Cron, out var cron))
        {
            logger.LogWarning("Git:Cron '{Cron}' is not a valid cron expression; git sync is disabled", options.Cron);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = cron.NextOccurrence(DateTime.Now) - DateTime.Now;
            try { await Task.Delay(delay, stoppingToken); } catch (OperationCanceledException) { break; }

            await RunGitAsync(["pull", "--ff-only"], contentRoot, stoppingToken);
        }
    }

    private async Task<bool> RunGitAsync(string[] args, string workingDirectory, CancellationToken ct)
    {
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            if (!string.IsNullOrEmpty(options.Password))
            {
                var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
                startInfo.Environment["GIT_CONFIG_COUNT"] = "1";
                startInfo.Environment["GIT_CONFIG_KEY_0"] = "http.extraheader";
                startInfo.Environment["GIT_CONFIG_VALUE_0"] = $"AUTHORIZATION: basic {basicAuth}";
            }

            process = new Process { StartInfo = startInfo };
            process.Start();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(PullTimeoutSeconds));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();

            var command = string.Join(' ', args);
            if (process.ExitCode != 0)
            {
                logger.LogWarning("git {Command} failed in {ContentRoot} ({ExitCode}): {Error}", command, workingDirectory, process.ExitCode, stderr);
                return false;
            }

            if (!stdout.Contains("Already up to date", StringComparison.OrdinalIgnoreCase))
                logger.LogInformation("git {Command} in {ContentRoot}: {Output}", command, workingDirectory, stdout);
            else
                logger.LogDebug("git {Command}: {ContentRoot} already up to date", command, workingDirectory);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // a bad network blip or merge conflict must never take the site down
            logger.LogWarning(ex, "git {Command} failed unexpectedly in {ContentRoot}", string.Join(' ', args), workingDirectory);
            return false;
        }
        finally
        {
            // a timed-out run leaves the process running past our wait; make sure it doesn't linger
            if (process is { HasExited: false })
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            process?.Dispose();
        }
    }
}

// minimal 5-field (minute hour day-of-month month day-of-week) cron expression: parses "*", "*/n",
// "a-b", "a-b/n" and comma lists per field, then finds the next match by scanning minute-by-minute.
internal sealed class CronSchedule(
    HashSet<int> minutes, HashSet<int> hours, HashSet<int> days, HashSet<int> months, HashSet<int> weekDays,
    bool dayIsWildcard, bool weekDayIsWildcard)
{
    private static readonly TimeSpan MaxLookahead = TimeSpan.FromDays(4 * 366);

    private HashSet<int> Minutes => minutes;
    private HashSet<int> Hours => hours;
    private HashSet<int> Days => days;
    private HashSet<int> Months => months;
    private HashSet<int> WeekDays => weekDays;
    private bool DayIsWildcard => dayIsWildcard;
    private bool WeekDayIsWildcard => weekDayIsWildcard;

    public static bool TryParse(string expression, out CronSchedule schedule)
    {
        schedule = null!;
        var fields = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
            return false;

        if (!TryParseField(fields[0], 0, 59, out var minutes)) return false;
        if (!TryParseField(fields[1], 0, 23, out var hours)) return false;
        if (!TryParseField(fields[2], 1, 31, out var days)) return false;
        if (!TryParseField(fields[3], 1, 12, out var months)) return false;
        if (!TryParseField(fields[4], 0, 6, out var weekDays)) return false;

        schedule = new CronSchedule(minutes, hours, days, months, weekDays, fields[2] == "*", fields[4] == "*");
        return true;
    }

    private static bool TryParseField(string field, int min, int max, out HashSet<int> values)
    {
        values = [];
        foreach (var part in field.Split(','))
        {
            var (range, stepText) = part.Split('/') is [var r, var s] ? (r, s) : (part, "1");
            if (!int.TryParse(stepText, out var step) || step < 1)
                return false;

            int from, to;
            if (range == "*") { from = min; to = max; }
            else if (range.Split('-') is [var a, var b] && int.TryParse(a, out from) && int.TryParse(b, out to)) { }
            else if (int.TryParse(range, out from)) { to = from; }
            else return false;

            if (from < min || to > max || from > to)
                return false;

            for (var v = from; v <= to; v += step)
                values.Add(v);
        }
        return values.Count > 0;
    }

    public DateTime NextOccurrence(DateTime after)
    {
        var candidate = new DateTime(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0).AddMinutes(1);
        var deadline = after + MaxLookahead;
        while (candidate < deadline)
        {
            // POSIX cron rule: when both day-of-month and day-of-week are restricted, either may match (OR);
            // when only one is restricted, that one alone decides.
            var dayMatches = (DayIsWildcard, WeekDayIsWildcard) switch
            {
                (true, true) => true,
                (true, false) => WeekDays.Contains((int)candidate.DayOfWeek),
                (false, true) => Days.Contains(candidate.Day),
                (false, false) => Days.Contains(candidate.Day) || WeekDays.Contains((int)candidate.DayOfWeek),
            };

            if (Months.Contains(candidate.Month) && dayMatches && Hours.Contains(candidate.Hour) && Minutes.Contains(candidate.Minute))
                return candidate;

            candidate = candidate.AddMinutes(1);
        }
        throw new InvalidOperationException("cron expression never matches within the lookahead window");
    }
}
