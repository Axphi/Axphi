using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace Axphi.ViewModels
{
    public abstract partial class TimeLineItemViewModelBase : ObservableObject, IDisposable
    {
        // 🌟 修正为 int
        public abstract int Tick { get; }

        public abstract Point Location { get; }

        [ObservableProperty]
        private bool _isVisible = true;

        public abstract void UpdateVisuals();

        public virtual void Dispose() { }
    }
}