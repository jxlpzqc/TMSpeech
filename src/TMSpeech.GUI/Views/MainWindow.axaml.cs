using Avalonia.Controls;
using TMSpeech.GUI.ViewModels;

namespace TMSpeech.GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    #region Borderless Window Drag and Resize

    private const double RESIZE_SIZE = 16;

    private void Window_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var p = e.GetPosition(this);
        var isLeft = (p.X < RESIZE_SIZE);
        var isRight = (p.X > this.Width - RESIZE_SIZE);
        var isTop = (p.Y < RESIZE_SIZE);
        var isBottom = (p.Y > this.Height - RESIZE_SIZE);

        if (isLeft && isTop) BeginResizeDrag(WindowEdge.NorthWest, e);
        else if (isLeft && isBottom) BeginResizeDrag(WindowEdge.SouthWest, e);
        else if (isRight && isTop) BeginResizeDrag(WindowEdge.NorthEast, e);
        else if (isRight && isBottom) BeginResizeDrag(WindowEdge.SouthEast, e);
        else if (isLeft) BeginResizeDrag(WindowEdge.West, e);
        else if (isTop) BeginResizeDrag(WindowEdge.North, e);
        else if (isBottom) BeginResizeDrag(WindowEdge.South, e);
        else if (isRight) BeginResizeDrag(WindowEdge.East, e);
        else BeginMoveDrag(e);
    }

    private void Window_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (e.Source != mainGrid)
        {
            this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
            return;
        }

        var p = e.GetPosition(this);
        var isLeft = (p.X < RESIZE_SIZE);
        var isRight = (p.X > this.Width - RESIZE_SIZE);
        var isTop = (p.Y < RESIZE_SIZE);
        var isBottom = (p.Y > this.Height - RESIZE_SIZE);

        if (isLeft && isTop) this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.TopLeftCorner);
        else if (isLeft && isBottom)
            this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.BottomLeftCorner);
        else if (isRight && isTop)
            this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.TopRightCorner);
        else if (isRight && isBottom)
            this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.BottomRightCorner);
        else if (isLeft) this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.LeftSide);
        else if (isTop) this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.TopSide);
        else if (isBottom) this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.BottomSide);
        else if (isRight) this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.RightSide);
        else this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
    }

    #endregion

    MainViewModel ViewModel => (MainViewModel)DataContext;
    
    private void SettingsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Topmost = false;
        new ConfigWindow().Show();
        this.Topmost = true;
    }

    private void HistoryButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Topmost = false;
        new HistoryWindow().Show();
        this.Topmost = true;
    }
}