using Axphi.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Threading;

namespace Axphi.ViewModels
{
    public partial class TrackExpressionSlot : ObservableObject
    {
        private readonly bool _expectsVector;
        private readonly Func<bool> _readEnabled;
        private readonly Action<bool> _writeEnabled;
        private readonly Func<string> _readText;
        private readonly Action<string> _writeText;
        private readonly Func<string, string?> _validateText;
        private readonly Action _afterToggle;
        private readonly Action _afterTextCommitted;
        private readonly DispatcherTimer _commitTimer;

        public TrackExpressionSlot(
            string title,
            string placeholder,
            bool expectsVector,
            Func<bool> readEnabled,
            Action<bool> writeEnabled,
            Func<string> readText,
            Action<string> writeText,
            Func<string, string?> validateText,
            Action afterToggle,
            Action afterTextCommitted)
        {
            Title = title;
            Placeholder = placeholder;
            _expectsVector = expectsVector;
            _readEnabled = readEnabled;
            _writeEnabled = writeEnabled;
            _readText = readText;
            _writeText = writeText;
            _validateText = validateText;
            _afterToggle = afterToggle;
            _afterTextCommitted = afterTextCommitted;
            _commitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _commitTimer.Tick += CommitTimer_Tick;

            _isEnabled = _readEnabled();
            _text = _readText() ?? string.Empty;
            ValidateCore();
        }

        public string Title { get; }

        public string Placeholder { get; }

        public bool HasError => !string.IsNullOrWhiteSpace(Error);

        public string Summary => HasError
            ? Error
            : IsEnabled
                ? (string.IsNullOrWhiteSpace(Text) ? "请输入 JS 表达式" : "表达式已启用")
                : "按住 Alt 点击蓝色方块启用表达式";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private bool _isEnabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private string _text = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private string _error = string.Empty;

        partial void OnIsEnabledChanged(bool value)
        {
            _writeEnabled(value);
            _commitTimer.Stop();
            ValidateCore();
            _afterToggle();
        }

        partial void OnTextChanged(string value)
        {
            _writeText(value ?? string.Empty);
            ScheduleCommit();
        }

        public void CommitNow()
        {
            _commitTimer.Stop();
            ValidateCore();
            _afterTextCommitted();
        }

        private void ScheduleCommit()
        {
            _commitTimer.Stop();
            _commitTimer.Start();
        }

        private void CommitTimer_Tick(object? sender, EventArgs e)
        {
            CommitNow();
        }

        private void ValidateCore()
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(Text))
            {
                Error = string.Empty;
                return;
            }

            Error = _validateText(Text) ?? string.Empty;
        }
    }
}