using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.Controls;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<IEnumerable<TextInfo>> ItemsSourceProperty =
        AvaloniaProperty.Register<HistoryView, IEnumerable<TextInfo>>(
            "ItemsSource");

    public IEnumerable<TextInfo> ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private (int, int) _selectionStart = (-1, 0);
    private (int, int) _selectionEnd = (-1, 0);
    private bool _isPointerPressed;

    private (int, int) GetIndexFromPointerEvent(PointerEventArgs e)
    {
        var hitTestControl = list.InputHitTest(e.GetPosition(list));
        if (hitTestControl is not DockPanel dockPanel) return (-1, 0);
        if (dockPanel.Children[1] is not SelectableTextBlock textblock) return (-1, 0);
        var parent = dockPanel.GetVisualParent();
        if (parent is not Control parentControl) return (-1, 0);
        var index = list.IndexFromContainer(parentControl);
        var pos = e.GetPosition(textblock);
        var element = textblock.TextLayout.HitTestPoint(pos);
        var charIndex = element.TextPosition;
        return (index, charIndex);
    }

    private ((int, int), (int, int)) GetLessAndGreater()
    {
        (int, int) less, greater;

        #region get less and greater

        if (_selectionStart.Item1 < _selectionEnd.Item1)
        {
            less = _selectionStart;
            greater = _selectionEnd;
        }
        else if (_selectionStart.Item1 > _selectionEnd.Item1)
        {
            less = _selectionEnd;
            greater = _selectionStart;
        }
        else
        {
            if (_selectionStart.Item2 < _selectionEnd.Item2)
            {
                less = _selectionStart;
                greater = _selectionEnd;
            }
            else
            {
                less = _selectionEnd;
                greater = _selectionStart;
            }
        }

        #endregion

        return (less, greater);
    }

    private void RenderSelection()
    {
        if (_selectionStart.Item1 == -1 || _selectionEnd.Item1 == -1)
        {
            for (int i = 0; i < list.ItemCount; i++)
            {
                if (list.ContainerFromIndex(i) is not ContentPresenter cp) continue;
                if (cp.Child is not DockPanel dockPanel) continue;
                if (dockPanel.Children[1] is not SelectableTextBlock textblock) continue;
                textblock.SelectionStart = 0;
                textblock.SelectionEnd = 0;
            }

            return;
        }

        var (less, greater) = GetLessAndGreater();

        for (var i = 0; i < list.ItemCount; i++)
        {
            if (list.ContainerFromIndex(i) is not ContentPresenter cp) continue;
            if (cp.Child is not DockPanel dockPanel) continue;
            if (dockPanel.Children[1] is not SelectableTextBlock textblock) continue;

            if (i < less.Item1 || i > greater.Item1)
            {
                textblock.SelectionStart = 0;
                textblock.SelectionEnd = 0;
                continue;
            }

            var start = i == less.Item1 ? less.Item2 : 0;
            var end = i == greater.Item1 ? greater.Item2 : textblock.Text.Length;
            textblock.SelectionStart = start;
            textblock.SelectionEnd = end;
        }
    }


    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Console.WriteLine($"{_selectionStart.Item1},{_selectionStart.Item2}");
        _selectionStart = GetIndexFromPointerEvent(e);
        _selectionEnd = (-1, 0);
        _isPointerPressed = true;
        RenderSelection();
    }

    private void InputElement_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPointerPressed = false;
    }

    private void InputElement_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerPressed) return;
        var p = GetIndexFromPointerEvent(e);
        if (p.Item1 == -1) return;
        _selectionEnd = p;
        RenderSelection();
    }

    public async void Copy()
    {
        string copyText = "";

        var (less, greater) = GetLessAndGreater();

        for (int i = less.Item1; i <= greater.Item1; i++)
        {
            if (list.ContainerFromIndex(i) is not ContentPresenter cp) continue;
            if (cp.Child is not DockPanel dockPanel) continue;
            if (dockPanel.Children[1] is not SelectableTextBlock textblock) continue;
            if (textblock.Text == null) continue;
            var start = i == less.Item1 ? less.Item2 : 0;
            var end = i == greater.Item1 ? greater.Item2 : textblock.Text.Length;
            copyText += textblock.Text.Substring(start, end - start) + "\n";
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        await clipboard.SetTextAsync(copyText);
    }

    public async void SelectAll()
    {
        _selectionStart = (0, 0);
        _selectionEnd = (list.ItemCount - 1, ItemsSource.Last().Text.Length);
        RenderSelection();
    }

    private void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        Copy();
    }

    private void SelectAll_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectAll();
    }
}