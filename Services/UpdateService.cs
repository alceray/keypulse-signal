using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace KeyPulse.Services;

public class UpdateService : IDisposable
{
    private const string GITHUB_OWNER = "alceray";
    private const string GITHUB_REPO = "keypulse-signal";
    private const string GITHUB_API_URL = $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly AppTimerService _appTimerService;
    private readonly CancellationTokenSource _lifetimeCts = new();

    private static readonly TimeSpan UpdateCheckTimeout = TimeSpan.FromSeconds(10);

    private string? _latestVersion;
    private bool _updateAvailable;
    private bool _started;
    private bool _disposed;
    private bool _networkFailureLogged;
    private int _checkInFlight;

    private static string GetGitHubReleaseTagUrl(string version) =>
        $"https://github.com/{GITHUB_OWNER}/{GITHUB_REPO}/releases/tag/v{version}";

    public event Action<UpdateAvailableEventArgs>? UpdateStatusChanged;

    public UpdateService(AppTimerService appTimerService)
    {
        _appTimerService = appTimerService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "KeyPulse-Signal-Update-Checker");
    }

    public string CurrentVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version?.Major}.{version?.Minor}.{version?.Build}";
        }
    }

    public string? LatestVersion => _latestVersion;

    public bool UpdateAvailable => _updateAvailable;

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        Log.Information("Update checker started");

        _ = CheckForUpdatesAsync();
        _appTimerService.DailyTick += OnDailyTick;
    }

    private void OnDailyTick(object? sender, EventArgs e) => _ = CheckForUpdatesAsync();

    public async Task CheckForUpdatesAsync()
    {
        if (_disposed)
            return;

        if (Interlocked.CompareExchange(ref _checkInFlight, 1, 0) != 0)
            return;

        try
        {
            using var timeoutCts = new CancellationTokenSource(UpdateCheckTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token, timeoutCts.Token);

            using var response = await _httpClient.GetAsync(GITHUB_API_URL, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check request failed: {StatusCode}", response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release?.TagName == null)
            {
                Log.Warning("Update check returned invalid release data");
                return;
            }

            var latestVersion = release.TagName.TrimStart('v');
            _latestVersion = latestVersion;
            if (_networkFailureLogged)
            {
                _networkFailureLogged = false;
                Log.Information("Update check recovered after transient network failure");
            }

            var updateAvailable = IsNewerVersion(CurrentVersion, latestVersion);

            Log.Debug(
                "Update check result: Current=v{Current}, Latest=v{Latest}, UpdateAvailable={Available}",
                CurrentVersion,
                latestVersion,
                updateAvailable
            );

            if (updateAvailable != _updateAvailable)
            {
                _updateAvailable = updateAvailable;
                UpdateStatusChanged?.Invoke(
                    new UpdateAvailableEventArgs { Available = updateAvailable, LatestVersion = latestVersion }
                );
            }
        }
        catch (OperationCanceledException) when (!_lifetimeCts.IsCancellationRequested)
        {
            LogTransientNetworkFailure(
                "Update check timed out after {TimeoutSeconds} seconds",
                UpdateCheckTimeout.TotalSeconds
            );
        }
        catch (HttpRequestException ex) when (IsTransientNetworkFailure(ex))
        {
            LogTransientNetworkFailure("Update check skipped due to transient network issue: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update check failed");
        }
        finally
        {
            Interlocked.Exchange(ref _checkInFlight, 0);
        }
    }

    public void InstallUpdate()
    {
        if (!_updateAvailable || _latestVersion == null)
        {
            Log.Warning("No update available to install");
            return;
        }

        try
        {
            var downloadUrl = GetGitHubReleaseTagUrl(_latestVersion);
            Process.Start(new ProcessStartInfo { FileName = downloadUrl, UseShellExecute = true });
            Log.Information("Opened update download page for version v{LatestVersion}", _latestVersion);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open update download page for version v{LatestVersion}", _latestVersion);
        }
    }

    private static bool IsNewerVersion(string current, string latest)
    {
        try
        {
            var currentParts = current.Split('.').Select(int.Parse).ToArray();
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();

            for (var i = 0; i < Math.Max(currentParts.Length, latestParts.Length); i++)
            {
                var currPart = i < currentParts.Length ? currentParts[i] : 0;
                var latestPart = i < latestParts.Length ? latestParts[i] : 0;

                if (latestPart > currPart)
                    return true;
                if (latestPart < currPart)
                    return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Version comparison failed: {Current} vs {Latest}", current, latest);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            Log.Debug("Update checker dispose skipped because it was already disposed");
            return;
        }

        _disposed = true;
        _appTimerService.DailyTick -= OnDailyTick;
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _httpClient.Dispose();
        Log.Information("Update checker disposed");
    }

    private void LogTransientNetworkFailure(string messageTemplate, object detail)
    {
        if (!_networkFailureLogged)
        {
            _networkFailureLogged = true;
            Log.Warning(messageTemplate, detail);
            return;
        }

        Log.Debug(messageTemplate, detail);
    }

    private static bool IsTransientNetworkFailure(HttpRequestException ex)
    {
        if (ex.HttpRequestError == HttpRequestError.NameResolutionError)
            return true;

        if (ex.InnerException is SocketException socketEx)
            return socketEx.SocketErrorCode == SocketError.HostNotFound
                || socketEx.SocketErrorCode == SocketError.TryAgain;

        return false;
    }

    public class UpdateAvailableEventArgs
    {
        public bool Available { get; set; }
        public string? LatestVersion { get; set; }
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
    }
}
