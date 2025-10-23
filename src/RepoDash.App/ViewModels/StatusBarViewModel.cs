using System;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RepoDash.App.ViewModels;

public sealed partial class StatusBarViewModel : ObservableObject
{
    private int _progressTotal;
    private int _progressCompleted;
    private long _operationSequence;
    private long _currentOperationId;
    private readonly object _summaryLock = new();
    private SummarySnapshot _snapshot = SummarySnapshot.Empty;
    private readonly DispatcherTimer _lastRefreshTimer;
    private DateTimeOffset? _lastRefreshUtc;

    [ObservableProperty]
    private string _message = "Ready";

    [ObservableProperty]
    private bool _isProgressVisible;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorSummary;

    [ObservableProperty]
    private string? _errorDetails;

    [ObservableProperty]
    private bool _isErrorExpanded;

    public StatusBarViewModel()
    {
        _lastRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1),
            IsEnabled = false
        };
        _lastRefreshTimer.Tick += (_, __) => RefreshLastRefreshDisplay();
    }

    public long BeginProgress(string message, int total)
    {
        var opId = Interlocked.Increment(ref _operationSequence);
        Interlocked.Exchange(ref _currentOperationId, opId);

        _progressTotal = Math.Max(total, 0);
        Interlocked.Exchange(ref _progressCompleted, 0);

        Message = message;
        IsProgressIndeterminate = _progressTotal == 0;
        ProgressValue = 0;
        IsProgressVisible = true;
        NotifyProgressDetailsChanged();

        return opId;
    }

    public long BeginIndeterminate(string message)
    {
        var opId = BeginProgress(message, 0);
        IsProgressIndeterminate = true;
        return opId;
    }

    public void ReportProgressIncrement(long operationId)
    {
        if (operationId != Volatile.Read(ref _currentOperationId)) return;
        if (_progressTotal <= 0) return;

        var completed = Interlocked.Increment(ref _progressCompleted);
        var ratio = Math.Clamp(completed / (double)_progressTotal, 0, 1);
        ProgressValue = ratio;
        NotifyProgressDetailsChanged();
    }

    public void ReportProgressAbsolute(int completed, long operationId)
    {
        if (operationId != Volatile.Read(ref _currentOperationId)) return;
        if (_progressTotal <= 0) return;

        Interlocked.Exchange(ref _progressCompleted, completed);
        var ratio = Math.Clamp(completed / (double)_progressTotal, 0, 1);
        ProgressValue = ratio;
        NotifyProgressDetailsChanged();
    }

    public void CompleteProgress(string? message, long operationId)
    {
        if (operationId != Volatile.Read(ref _currentOperationId)) return;

        if (_progressTotal > 0)
        {
            ProgressValue = 1;
            Interlocked.Exchange(ref _progressCompleted, _progressTotal);
        }

        _progressTotal = 0;
        Interlocked.Exchange(ref _progressCompleted, 0);
        Interlocked.Exchange(ref _currentOperationId, 0);

        IsProgressVisible = false;
        IsProgressIndeterminate = false;
        NotifyProgressDetailsChanged();

        if (!string.IsNullOrWhiteSpace(message))
        {
            Message = message!;
        }
        else if (!HasError)
        {
            Message = "Ready";
        }
    }

    public void ShowError(string summary, string details)
    {
        ErrorSummary = summary;
        ErrorDetails = details;
        HasError = true;
        IsErrorExpanded = false;

        Message = summary;
    }

    public void ClearError()
    {
        HasError = false;
        ErrorSummary = null;
        ErrorDetails = null;
        IsErrorExpanded = false;

        if (!IsProgressVisible)
        {
            Message = "Ready";
        }
    }

    public string ProgressDisplay
        => _progressTotal > 0
            ? $"{Math.Min(Volatile.Read(ref _progressCompleted), _progressTotal)}/{_progressTotal}"
            : string.Empty;

    public bool IsProgressDetailsVisible => _progressTotal > 0 && !IsProgressIndeterminate;

    public string SummaryDisplay
    {
        get
        {
            var snap = _snapshot;
            return $"Repositories: {snap.Total}, Up to date: {snap.UpToDate}, Behind: {snap.Behind}, Ahead: {snap.Ahead}, Unknown: {snap.Unknown}";
        }
    }

    public string LastRefreshDisplay
    {
        get
        {
            if (_lastRefreshUtc is null)
                return "Last refresh: not yet";

            var elapsed = DateTimeOffset.UtcNow - _lastRefreshUtc.Value;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

            string relative = elapsed switch
            {
                var t when t < TimeSpan.FromMinutes(1) => "just now",
                var t when t < TimeSpan.FromHours(1) => FormatUnits(t.TotalMinutes, "minute"),
                var t when t < TimeSpan.FromDays(1) => FormatUnits(t.TotalHours, "hour"),
                var t when t < TimeSpan.FromDays(7) => FormatUnits(t.TotalDays, "day"),
                _ => _lastRefreshUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            };

            return $"Last refresh: {relative}";
        }
    }

    public string? LastRefreshToolTip
    {
        get
        {
            if (_lastRefreshUtc is null) return "Repositories have not been refreshed yet.";

            var local = _lastRefreshUtc.Value.ToLocalTime();
            var elapsed = DateTimeOffset.UtcNow - _lastRefreshUtc.Value;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

            return $"{local:yyyy-MM-dd HH:mm:ss} ({FormatRelative(elapsed)})";
        }
    }

    public void SetLastRefresh(DateTimeOffset? timestampUtc)
    {
        _lastRefreshUtc = timestampUtc;
        RefreshLastRefreshDisplay();

        if (_lastRefreshUtc is not null)
        {
            if (!_lastRefreshTimer.IsEnabled)
            {
                // align next tick to the upcoming minute boundary for smoother updates
                var now = DateTimeOffset.Now;
                var msUntilNextMinute = (int)(60000 - (now.Second * 1000 + now.Millisecond));
                _lastRefreshTimer.Interval = TimeSpan.FromMilliseconds(msUntilNextMinute);
                _lastRefreshTimer.Start();
            }
        }
        else
        {
            if (_lastRefreshTimer.IsEnabled)
            {
                _lastRefreshTimer.Stop();
                _lastRefreshTimer.Interval = TimeSpan.FromMinutes(1);
            }
        }
    }

    public void UpdateSummary(int total, int upToDate, int behind, int ahead, int unknown)
    {
        lock (_summaryLock)
        {
            _snapshot = new SummarySnapshot(total, upToDate, behind, ahead, unknown);
        }
        OnPropertyChanged(nameof(SummaryDisplay));
    }

    [RelayCommand]
    private void ToggleErrorDetails()
    {
        if (!HasError) return;
        IsErrorExpanded = !IsErrorExpanded;
    }

    [RelayCommand]
    private void DismissError()
    {
        ClearError();
    }

    private void NotifyProgressDetailsChanged()
    {
        OnPropertyChanged(nameof(ProgressDisplay));
        OnPropertyChanged(nameof(IsProgressDetailsVisible));
    }

    private void RefreshLastRefreshDisplay()
    {
        OnPropertyChanged(nameof(LastRefreshDisplay));
        OnPropertyChanged(nameof(LastRefreshToolTip));
        if (_lastRefreshTimer.IsEnabled && _lastRefreshTimer.Interval != TimeSpan.FromMinutes(1))
        {
            _lastRefreshTimer.Interval = TimeSpan.FromMinutes(1);
        }
    }

    private static string FormatUnits(double value, string unit)
    {
        var rounded = Math.Max(1, (int)Math.Round(value));
        return rounded == 1 ? $"1 {unit} ago" : $"{rounded} {unit}s ago";
    }

    private static string FormatRelative(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return FormatUnits(elapsed.TotalMinutes, "minute");
        if (elapsed < TimeSpan.FromDays(1)) return FormatUnits(elapsed.TotalHours, "hour");
        return FormatUnits(elapsed.TotalDays, "day");
    }

    private readonly record struct SummarySnapshot(int Total, int UpToDate, int Behind, int Ahead, int Unknown)
    {
        public static SummarySnapshot Empty { get; } = new(0, 0, 0, 0, 0);
    }
}
