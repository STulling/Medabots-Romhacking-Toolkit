using System.Windows;
namespace Medabots.Rom.WPFEditor;

public partial class EventStructuralActionsControl : System.Windows.Controls.UserControl
{
    public static readonly RoutedEvent InsertNopBeforeClickEvent = EventManager.RegisterRoutedEvent(
        nameof(InsertNopBeforeClick),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EventStructuralActionsControl));

    public static readonly RoutedEvent InsertNopAfterClickEvent = EventManager.RegisterRoutedEvent(
        nameof(InsertNopAfterClick),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EventStructuralActionsControl));

    public static readonly RoutedEvent InsertSelectedOperationBeforeClickEvent = EventManager.RegisterRoutedEvent(
        nameof(InsertSelectedOperationBeforeClick),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EventStructuralActionsControl));

    public static readonly RoutedEvent InsertSelectedOperationAfterClickEvent = EventManager.RegisterRoutedEvent(
        nameof(InsertSelectedOperationAfterClick),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EventStructuralActionsControl));

    public static readonly RoutedEvent MoveUpClickEvent = EventManager.RegisterRoutedEvent(
        nameof(MoveUpClick),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EventStructuralActionsControl));

    public static readonly RoutedEvent MoveDownClickEvent = EventManager.RegisterRoutedEvent(
        nameof(MoveDownClick),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EventStructuralActionsControl));

    public static readonly RoutedEvent DeleteClickEvent = EventManager.RegisterRoutedEvent(
        nameof(DeleteClick),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EventStructuralActionsControl));

    public EventStructuralActionsControl()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler InsertNopBeforeClick
    {
        add => AddHandler(InsertNopBeforeClickEvent, value);
        remove => RemoveHandler(InsertNopBeforeClickEvent, value);
    }

    public event RoutedEventHandler InsertNopAfterClick
    {
        add => AddHandler(InsertNopAfterClickEvent, value);
        remove => RemoveHandler(InsertNopAfterClickEvent, value);
    }

    public event RoutedEventHandler InsertSelectedOperationBeforeClick
    {
        add => AddHandler(InsertSelectedOperationBeforeClickEvent, value);
        remove => RemoveHandler(InsertSelectedOperationBeforeClickEvent, value);
    }

    public event RoutedEventHandler InsertSelectedOperationAfterClick
    {
        add => AddHandler(InsertSelectedOperationAfterClickEvent, value);
        remove => RemoveHandler(InsertSelectedOperationAfterClickEvent, value);
    }

    public event RoutedEventHandler MoveUpClick
    {
        add => AddHandler(MoveUpClickEvent, value);
        remove => RemoveHandler(MoveUpClickEvent, value);
    }

    public event RoutedEventHandler MoveDownClick
    {
        add => AddHandler(MoveDownClickEvent, value);
        remove => RemoveHandler(MoveDownClickEvent, value);
    }

    public event RoutedEventHandler DeleteClick
    {
        add => AddHandler(DeleteClickEvent, value);
        remove => RemoveHandler(DeleteClickEvent, value);
    }

    private void OnInsertNopBeforeClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(InsertNopBeforeClickEvent));

    private void OnInsertNopAfterClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(InsertNopAfterClickEvent));

    private void OnInsertSelectedOperationBeforeClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(InsertSelectedOperationBeforeClickEvent));

    private void OnInsertSelectedOperationAfterClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(InsertSelectedOperationAfterClickEvent));

    private void OnMoveUpClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MoveUpClickEvent));

    private void OnMoveDownClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(MoveDownClickEvent));

    private void OnDeleteClicked(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(DeleteClickEvent));
}
