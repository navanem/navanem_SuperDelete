//Copyright 2015 Marcel Nita (marcel.nita@gmail.com)
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using SuperDelete.App.Services;
using SuperDelete.Core;
using SuperDelete.Core.Abstractions;
using SuperDelete.Core.Models;

namespace SuperDelete.App.ViewModels
{
    /// <summary>
    /// The single view model behind the main window. Orchestrates analysis and deletion against the
    /// shared Core engine, keeps the UI responsive, and exposes everything the view binds to.
    /// </summary>
    public sealed class MainViewModel : ViewModelBase
    {
        private const int MaxRecentPaths = 10;

        private readonly IDeletionService _deletionService;
        private readonly IPathAnalyzer _analyzer;
        private readonly IDialogService _dialogService;
        private readonly Action<bool> _applyTheme;

        // Live counters written by the worker thread, sampled by a UI timer (see DelegateProgress).
        private readonly DispatcherTimer _sampleTimer;
        private volatile int _liveFiles;
        private volatile int _liveFolders;
        private volatile string _liveActivity = string.Empty;

        private CancellationTokenSource? _cts;

        public MainViewModel(
            IDeletionService deletionService,
            IPathAnalyzer analyzer,
            IDialogService dialogService,
            Action<bool> applyTheme)
        {
            _deletionService = deletionService;
            _analyzer = analyzer;
            _dialogService = dialogService;
            _applyTheme = applyTheme;

            BrowseFileCommand = new RelayCommand(BrowseFile, () => !IsBusy);
            BrowseFolderCommand = new RelayCommand(BrowseFolder, () => !IsBusy);
            AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanOperate);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanOperate);
            CancelCommand = new RelayCommand(Cancel, () => IsBusy);
            ClearLogCommand = new RelayCommand(() => { Log = string.Empty; }, () => Log.Length > 0);

            _sampleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _sampleTimer.Tick += (_, _) => SampleLiveProgress();

            AppendLog("Ready. Select or drop a file/folder, then Analyze or Delete.");
        }

        // ===== Commands =====
        public RelayCommand BrowseFileCommand { get; }
        public RelayCommand BrowseFolderCommand { get; }
        public AsyncRelayCommand AnalyzeCommand { get; }
        public AsyncRelayCommand DeleteCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ClearLogCommand { get; }

        // ===== Inputs =====
        private string _selectedPath = string.Empty;
        public string SelectedPath
        {
            get => _selectedPath;
            set
            {
                if (SetProperty(ref _selectedPath, value ?? string.Empty))
                {
                    // A new target invalidates the previous analysis.
                    Analysis = null;
                    RaiseCommandStates();
                }
            }
        }

        private bool _previewOnly;
        public bool PreviewOnly
        {
            get => _previewOnly;
            set => SetProperty(ref _previewOnly, value);
        }

        private bool _bypassAcl;
        public bool BypassAcl
        {
            get => _bypassAcl;
            set
            {
                if (SetProperty(ref _bypassAcl, value))
                {
                    OnPropertyChanged(nameof(ShowBypassAclWarning));
                }
            }
        }

        private bool _showDiagnostics;
        public bool ShowDiagnostics
        {
            get => _showDiagnostics;
            set => SetProperty(ref _showDiagnostics, value);
        }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    _applyTheme(value);
                }
            }
        }

        // ===== Recent paths (in-memory for this session) =====
        public ObservableCollection<string> RecentPaths { get; } = new ObservableCollection<string>();

        private string? _selectedRecentPath;
        public string? SelectedRecentPath
        {
            get => _selectedRecentPath;
            set
            {
                if (SetProperty(ref _selectedRecentPath, value) && !string.IsNullOrEmpty(value))
                {
                    SelectedPath = value!;
                }
            }
        }

        // ===== Busy / progress state =====
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                    RaiseCommandStates();
                }
            }
        }

        public bool IsIdle => !IsBusy;

        private int _filesProcessed;
        public int FilesProcessed
        {
            get => _filesProcessed;
            private set { if (SetProperty(ref _filesProcessed, value)) OnPropertyChanged(nameof(ProgressSummary)); }
        }

        private int _foldersProcessed;
        public int FoldersProcessed
        {
            get => _foldersProcessed;
            private set { if (SetProperty(ref _foldersProcessed, value)) OnPropertyChanged(nameof(ProgressSummary)); }
        }

        public string ProgressSummary => $"{FilesProcessed} files · {FoldersProcessed} folders";

        private string _currentActivity = string.Empty;
        public string CurrentActivity
        {
            get => _currentActivity;
            private set => SetProperty(ref _currentActivity, value);
        }

        // ===== Status banner =====
        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        private StatusKind _status = StatusKind.None;
        public StatusKind Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        // ===== Log =====
        private string _log = string.Empty;
        public string Log
        {
            get => _log;
            private set
            {
                if (SetProperty(ref _log, value))
                {
                    ClearLogCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // ===== Diagnostics (collapsible technical detail) =====
        private string? _diagnosticDetails;
        public string? DiagnosticDetails
        {
            get => _diagnosticDetails;
            private set
            {
                if (SetProperty(ref _diagnosticDetails, value))
                {
                    OnPropertyChanged(nameof(HasDiagnostics));
                }
            }
        }

        public bool HasDiagnostics => !string.IsNullOrEmpty(DiagnosticDetails);

        // ===== Analysis summary =====
        private PathAnalysis? _analysis;
        public PathAnalysis? Analysis
        {
            get => _analysis;
            private set
            {
                if (SetProperty(ref _analysis, value))
                {
                    OnPropertyChanged(nameof(HasAnalysis));
                    OnPropertyChanged(nameof(SummaryType));
                    OnPropertyChanged(nameof(SummaryExists));
                    OnPropertyChanged(nameof(SummaryPathLength));
                    OnPropertyChanged(nameof(SummaryItems));
                    OnPropertyChanged(nameof(ExceedsMaxPath));
                    OnPropertyChanged(nameof(HasAccessIssues));
                    OnPropertyChanged(nameof(AccessIssueDetail));
                    OnPropertyChanged(nameof(ShowRecursiveWarning));
                    OnPropertyChanged(nameof(RecursiveWarningText));
                }
            }
        }

        public bool HasAnalysis => Analysis != null;
        public string SummaryType => Analysis == null ? "—" : !Analysis.Exists ? "Not found" : Analysis.IsDirectory ? "Folder" : "File";
        public string SummaryExists => Analysis == null ? "—" : Analysis.Exists ? "Yes" : "No — nothing to delete";
        public string SummaryPathLength => Analysis == null ? "—"
            : $"{Analysis.PathLength} characters" + (Analysis.ExceedsMaxPath ? "  (exceeds the 260-char limit)" : string.Empty);
        public string SummaryItems
        {
            get
            {
                if (Analysis == null || !Analysis.Exists) return "—";
                if (!Analysis.IsDirectory) return "1 file";
                return $"{Analysis.TotalItems} items  ({Analysis.FileCount} files, {Analysis.FolderCount} sub-folders)";
            }
        }
        public bool ExceedsMaxPath => Analysis?.ExceedsMaxPath ?? false;
        public bool HasAccessIssues => Analysis?.HasAccessIssues ?? false;
        public string? AccessIssueDetail => Analysis?.AccessIssueDetail;

        public bool ShowRecursiveWarning => Analysis is { Exists: true, IsDirectory: true } && Analysis.TotalItems > 1;
        public string RecursiveWarningText => Analysis == null
            ? string.Empty
            : $"This will permanently delete {Analysis.TotalItems} items, including every sub-folder. This cannot be undone.";

        public bool ShowBypassAclWarning => BypassAcl;

        // ===== Command implementations =====
        private bool CanOperate() => !IsBusy && !string.IsNullOrWhiteSpace(SelectedPath);

        private void RaiseCommandStates()
        {
            BrowseFileCommand.RaiseCanExecuteChanged();
            BrowseFolderCommand.RaiseCanExecuteChanged();
            AnalyzeCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
        }

        private void BrowseFile()
        {
            var path = _dialogService.BrowseForFile();
            if (path != null) SelectedPath = path;
        }

        private void BrowseFolder()
        {
            var path = _dialogService.BrowseForFolder();
            if (path != null) SelectedPath = path;
        }

        private async Task AnalyzeAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPath)) return;

            BeginOperation();
            SetStatus(StatusKind.Info, "Analyzing…");
            AppendLog($"Analyzing: {SelectedPath}");

            try
            {
                _cts = new CancellationTokenSource();
                var analysis = await _analyzer.AnalyzeAsync(SelectedPath, _cts.Token);
                Analysis = analysis;

                if (!analysis.Exists)
                {
                    SetStatus(StatusKind.Warning, "Path not found");
                    AppendLog($"Not found: {analysis.FullPath}");
                }
                else
                {
                    SetStatus(StatusKind.Info, "Analysis complete — review the summary before deleting.");
                    AppendLog($"Found {SummaryType.ToLowerInvariant()} · {SummaryItems} · {SummaryPathLength}");
                    if (analysis.HasAccessIssues)
                    {
                        AppendLog($"Note: {analysis.AccessIssueDetail}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus(StatusKind.Warning, "Analysis cancelled");
                AppendLog("Analysis cancelled.");
            }
            catch (Exception ex)
            {
                SetStatus(StatusKind.Error, "Analysis failed");
                AppendLog($"Analysis failed: {ex.Message}");
                DiagnosticDetails = ex.ToString();
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task DeleteAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPath)) return;

            var options = new DeletionOptions { BypassAcl = BypassAcl, PreviewOnly = PreviewOnly };

            // A real deletion always asks for explicit confirmation. A preview makes no changes, so it
            // runs straight away.
            if (!PreviewOnly)
            {
                string countLine = HasAnalysis && Analysis!.Exists
                    ? $"\n\nItems to delete: {SummaryItems}"
                    : string.Empty;

                string confirmMessage =
                    $"Permanently delete this {(Analysis?.IsDirectory == true ? "folder and all its contents" : "item")}?\n\n" +
                    $"{SelectedPath}{countLine}\n\n" +
                    (BypassAcl ? "Bypass ACL is ENABLED (administrator rights required).\n\n" : string.Empty) +
                    "This action cannot be undone.";

                if (!_dialogService.ConfirmDeletion("Confirm deletion", confirmMessage))
                {
                    AppendLog("Deletion cancelled by user.");
                    return;
                }
            }

            BeginOperation();
            DiagnosticDetails = null;
            ResetLiveCounters();
            _sampleTimer.Start();

            SetStatus(StatusKind.Info, PreviewOnly ? "Previewing (no changes will be made)…" : "Deleting…");
            AppendLog(PreviewOnly
                ? $"Preview started (dry run, nothing will be deleted): {SelectedPath}"
                : $"Deletion started: {SelectedPath}{(BypassAcl ? "  [Bypass ACL]" : string.Empty)}");

            AddRecentPath(SelectedPath);

            try
            {
                _cts = new CancellationTokenSource();
                var progress = new DelegateProgress<DeletionProgress>(OnEngineProgress);

                DeletionResult result = await _deletionService.DeleteAsync(SelectedPath, options, progress, _cts.Token);

                _sampleTimer.Stop();
                SampleLiveProgress(); // flush final counts
                ApplyResult(result);
            }
            catch (Exception ex)
            {
                _sampleTimer.Stop();
                SetStatus(StatusKind.Error, "Unexpected error");
                AppendLog($"Unexpected error: {ex.Message}");
                DiagnosticDetails = ex.ToString();
            }
            finally
            {
                EndOperation();
                CurrentActivity = string.Empty;
            }
        }

        private void ApplyResult(DeletionResult result)
        {
            switch (result.Status)
            {
                case DeletionStatus.Success:
                    SetStatus(StatusKind.Success, $"Success — deleted {result.FilesProcessed} files and {result.FoldersProcessed} folders.");
                    AppendLog($"Done in {result.Elapsed:hh\\:mm\\:ss\\.fff}. Deleted {result.FilesProcessed} files and {result.FoldersProcessed} folders.");
                    Analysis = null; // the target is gone now
                    break;

                case DeletionStatus.PreviewCompleted:
                    SetStatus(StatusKind.Info, $"Preview complete — would delete {result.FilesProcessed} files and {result.FoldersProcessed} folders.");
                    AppendLog($"Preview complete in {result.Elapsed:hh\\:mm\\:ss\\.fff}. Would delete {result.FilesProcessed} files and {result.FoldersProcessed} folders. Nothing was changed.");
                    break;

                case DeletionStatus.Cancelled:
                    SetStatus(StatusKind.Warning, $"Cancelled after deleting {result.FilesProcessed} files and {result.FoldersProcessed} folders.");
                    AppendLog($"Cancelled. Deleted {result.FilesProcessed} files and {result.FoldersProcessed} folders before stopping.");
                    Analysis = null;
                    break;

                case DeletionStatus.Partial:
                    SetStatus(StatusKind.Error, $"Partial — deleted {result.FilesProcessed} files and {result.FoldersProcessed} folders, then failed.");
                    AppendLog($"Partial failure: {result.ErrorMessage}");
                    AppendLog($"Deleted {result.FilesProcessed} files and {result.FoldersProcessed} folders before the error.");
                    if (result.Exception != null) DiagnosticDetails = result.Exception.ToString();
                    break;

                case DeletionStatus.Failed:
                    SetStatus(StatusKind.Error, "Failed — nothing was deleted.");
                    AppendLog($"Failed: {result.ErrorMessage}");
                    if (result.Exception != null) DiagnosticDetails = result.Exception.ToString();
                    break;
            }
        }

        private void Cancel()
        {
            if (_cts is { IsCancellationRequested: false })
            {
                AppendLog("Cancellation requested…");
                _cts.Cancel();
            }
        }

        // ===== Progress plumbing =====
        private void OnEngineProgress(DeletionProgress p)
        {
            // Runs on the worker thread: only touch the volatile fields, never bound properties.
            _liveFiles = p.FilesProcessed;
            _liveFolders = p.FoldersProcessed;
            _liveActivity = p.Path;
        }

        private void SampleLiveProgress()
        {
            FilesProcessed = _liveFiles;
            FoldersProcessed = _liveFolders;
            var activity = _liveActivity;
            if (activity.Length > 0)
            {
                CurrentActivity = (PreviewOnly ? "Scanning: " : "Deleting: ") + PathUtils.Shorten(activity);
            }
        }

        private void ResetLiveCounters()
        {
            _liveFiles = 0;
            _liveFolders = 0;
            _liveActivity = string.Empty;
            FilesProcessed = 0;
            FoldersProcessed = 0;
            CurrentActivity = string.Empty;
        }

        // ===== Helpers =====
        private void BeginOperation()
        {
            IsBusy = true;
        }

        private void EndOperation()
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }

        private void SetStatus(StatusKind kind, string text)
        {
            Status = kind;
            StatusText = text;
        }

        private void AddRecentPath(string path)
        {
            if (RecentPaths.Contains(path))
            {
                RecentPaths.Remove(path);
            }
            RecentPaths.Insert(0, path);
            while (RecentPaths.Count > MaxRecentPaths)
            {
                RecentPaths.RemoveAt(RecentPaths.Count - 1);
            }
        }

        private void AppendLog(string line)
        {
            var stamp = DateTime.Now.ToString("HH:mm:ss");
            var sb = new StringBuilder(Log);
            if (sb.Length > 0) sb.Append(Environment.NewLine);
            sb.Append('[').Append(stamp).Append("]  ").Append(line);
            Log = sb.ToString();
        }
    }
}
