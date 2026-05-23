using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace Axphi.ViewModels
{
    public abstract partial class TimeLineItemViewModelBase : ObservableObject, IDisposable
    {
        
        public abstract int Tick { get; set; }

        public abstract Point Location { get; }


        
        private double _pixelPerTick;
        public double PixelPerTick
        {
            get => _pixelPerTick;
            set => SetProperty(ref _pixelPerTick, value);
        }

        private double _zoom;
        public double Zoom
        {
            get => _zoom;
            set => SetProperty(ref _zoom, value);
        }

        [ObservableProperty]
        private bool _isVisible = true;

        public abstract void UpdateVisuals();

        public virtual void Dispose() { }
    }
}