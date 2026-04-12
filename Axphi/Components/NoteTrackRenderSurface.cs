using Axphi.ViewModels;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Axphi.Components;

public sealed class NoteTrackRenderSurface : FrameworkElement
{
    public static readonly DependencyProperty NotesProperty = DependencyProperty.Register(
        nameof(Notes),
        typeof(IEnumerable),
        typeof(NoteTrackRenderSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnNotesChanged));

    public static readonly DependencyProperty TimelineProperty = DependencyProperty.Register(
        nameof(Timeline),
        typeof(TimelineViewModel),
        typeof(NoteTrackRenderSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LeftPaddingProperty = DependencyProperty.Register(
        nameof(LeftPadding),
        typeof(double),
        typeof(NoteTrackRenderSurface),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly HashSet<INotifyPropertyChanged> _trackedNotes = new();
    private INotifyCollectionChanged? _notesCollection;

    private static readonly Brush HoldBodyBrush = CreateBrush("#6651B4FF");
    private static readonly Brush HoldTailBrush = CreateBrush("#88FFFFFF");
    private static readonly Brush NoteNormalBrush = CreateBrush("#1ccbe9");
    private static readonly Brush NoteSelectedBrush = CreateBrush("#28E857");
    private static readonly Pen NoteSelectedPen = CreatePen("#FFFFFFFF", 1.5);

    public IEnumerable? Notes
    {
        get => (IEnumerable?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public TimelineViewModel? Timeline
    {
        get => (TimelineViewModel?)GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    public double LeftPadding
    {
        get => (double)GetValue(LeftPaddingProperty);
        set => SetValue(LeftPaddingProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (Notes == null || Timeline == null)
        {
            return;
        }

        foreach (object noteObject in Notes)
        {
            if (noteObject is not NoteViewModel note)
            {
                continue;
            }

            DrawNote(dc, note, Timeline, LeftPadding);
        }
    }

    private static void DrawNote(DrawingContext dc, NoteViewModel note, TimelineViewModel timeline, double leftPadding)
    {
        double x = leftPadding + timeline.TickToPixel(note.HitTime);
        double y = note.PixelY;

        if (note.CurrentNoteKind == Axphi.Data.NoteKind.Hold)
        {
            var holdBodyRect = new Rect(x, y + 7, Math.Max(0, note.UIHoldPixelWidth), 6);
            dc.DrawRectangle(HoldBodyBrush, null, holdBodyRect);

            var tailRect = new Rect(x + note.UIHoldPixelWidth - 3, y + 5, 6, 10);
            dc.DrawRectangle(HoldTailBrush, null, tailRect);
        }

        var diamond = CreateDiamondGeometry(new Point(x, y + 10), 4);
        Brush fill = note.IsSelected ? NoteSelectedBrush : NoteNormalBrush;
        Pen? stroke = note.IsSelected ? NoteSelectedPen : null;
        dc.DrawGeometry(fill, stroke, diamond);
    }

    private static Geometry CreateDiamondGeometry(Point center, double halfSize)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - halfSize), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(center.X + halfSize, center.Y), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(center.X, center.Y + halfSize), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(center.X - halfSize, center.Y), isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(string hex, double thickness)
    {
        var pen = new Pen(CreateBrush(hex), thickness);
        pen.Freeze();
        return pen;
    }

    private static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NoteTrackRenderSurface surface)
        {
            return;
        }

        surface.DetachNotes(e.OldValue as IEnumerable);
        surface.AttachNotes(e.NewValue as IEnumerable);
        surface.InvalidateVisual();
    }

    private void AttachNotes(IEnumerable? notes)
    {
        if (notes is INotifyCollectionChanged notifyCollection)
        {
            _notesCollection = notifyCollection;
            _notesCollection.CollectionChanged += NotesCollectionChanged;
        }

        if (notes == null)
        {
            return;
        }

        foreach (object note in notes)
        {
            TrackNote(note);
        }
    }

    private void DetachNotes(IEnumerable? notes)
    {
        if (_notesCollection != null)
        {
            _notesCollection.CollectionChanged -= NotesCollectionChanged;
            _notesCollection = null;
        }

        foreach (var note in _trackedNotes)
        {
            note.PropertyChanged -= NotePropertyChanged;
        }

        _trackedNotes.Clear();
    }

    private void NotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (object note in e.OldItems)
            {
                UntrackNote(note);
            }
        }

        if (e.NewItems != null)
        {
            foreach (object note in e.NewItems)
            {
                TrackNote(note);
            }
        }

        InvalidateVisual();
    }

    private void TrackNote(object note)
    {
        if (note is not INotifyPropertyChanged notifyPropertyChanged)
        {
            return;
        }

        if (_trackedNotes.Add(notifyPropertyChanged))
        {
            notifyPropertyChanged.PropertyChanged += NotePropertyChanged;
        }
    }

    private void UntrackNote(object note)
    {
        if (note is not INotifyPropertyChanged notifyPropertyChanged)
        {
            return;
        }

        if (_trackedNotes.Remove(notifyPropertyChanged))
        {
            notifyPropertyChanged.PropertyChanged -= NotePropertyChanged;
        }
    }

    private void NotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NoteViewModel.HitTime):
            case nameof(NoteViewModel.PixelY):
            case nameof(NoteViewModel.UIHoldPixelWidth):
            case nameof(NoteViewModel.CurrentNoteKind):
            case nameof(NoteViewModel.IsSelected):
                InvalidateVisual();
                break;
        }
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        if (VisualParent == null)
        {
            DetachNotes(Notes);
        }
    }
}
