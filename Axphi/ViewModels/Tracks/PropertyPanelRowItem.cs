using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;
using System.Windows.Input;

namespace Axphi.ViewModels;

public enum PropertyEditorKind
{
    NoteKind,
    Anchor,
    Position,
    Scale,
    Rotation,
    RotationPlain,
    Opacity,
    Speed
}

public sealed class PropertyPanelRowItem : ObservableObject
{
    private readonly Func<ICommand?>? _addKeyframeCommandAccessor;
    private readonly Func<TrackExpressionSlot?>? _expressionSlotAccessor;
    private readonly Func<Visibility>? _keyframeButtonVisibilityAccessor;

    public PropertyPanelRowItem(
        string title,
        PropertyEditorKind editorKind,
        ICommand? addKeyframeCommand,
        TrackExpressionSlot? expressionSlot,
        Visibility keyframeButtonVisibility = Visibility.Visible,
        Visibility expressionIndicatorVisibility = Visibility.Visible,
        GridLength? expressionColumnWidth = null)
        : this(
            title,
            editorKind,
            () => addKeyframeCommand,
            () => expressionSlot,
            () => keyframeButtonVisibility,
            expressionIndicatorVisibility,
            expressionColumnWidth)
    {
    }

    public PropertyPanelRowItem(
        string title,
        PropertyEditorKind editorKind,
        Func<ICommand?>? addKeyframeCommandAccessor,
        Func<TrackExpressionSlot?>? expressionSlotAccessor,
        Func<Visibility>? keyframeButtonVisibilityAccessor,
        Visibility expressionIndicatorVisibility = Visibility.Visible,
        GridLength? expressionColumnWidth = null)
    {
        Title = title;
        EditorKind = editorKind;
        _addKeyframeCommandAccessor = addKeyframeCommandAccessor;
        _expressionSlotAccessor = expressionSlotAccessor;
        _keyframeButtonVisibilityAccessor = keyframeButtonVisibilityAccessor;
        ExpressionIndicatorVisibility = expressionIndicatorVisibility;
        ExpressionColumnWidth = expressionColumnWidth ?? new GridLength(14);
    }

    public string Title { get; }

    public PropertyEditorKind EditorKind { get; }

    public ICommand? AddKeyframeCommand => _addKeyframeCommandAccessor?.Invoke();

    public TrackExpressionSlot? ExpressionSlot => _expressionSlotAccessor?.Invoke();

    public Visibility KeyframeButtonVisibility => _keyframeButtonVisibilityAccessor?.Invoke() ?? Visibility.Visible;

    public Visibility ExpressionIndicatorVisibility { get; }

    public GridLength ExpressionColumnWidth { get; }

    public void RefreshBindings()
    {
        OnPropertyChanged(nameof(AddKeyframeCommand));
        OnPropertyChanged(nameof(ExpressionSlot));
        OnPropertyChanged(nameof(KeyframeButtonVisibility));
    }
}
