using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.Input;

using FCGR.Common.Libraries;

namespace FCGR.CommonAvalonia.MVVM.Controls;

public abstract class MenuActionBase : Model
{
	private readonly Predicate<object?> _is_visible;
	protected readonly Predicate<object?> _command_can_execute;

	public string Title
	{
		get;
		private set;
	}
	public Bitmap? Icon
	{
		get;
		private set;
	}
	public AvaloniaList<MenuActionBase> Actions_Submenu
	{
		get;
		set;
	}
	public bool? Is_Visible
	{
		get { return _is_visible(null); }
	}
	public object? Command_Parameter
	{
		get;
		private set;
	}
	public MenuItemToggleType Toggle_Type
	{
		get;
		private set;
	}
	public bool Is_Checked
	{
		get;
		set;
	}

	protected MenuActionBase(string title, Predicate<object?> is_visible, Predicate<object?>? can_execute, object? parameter, Bitmap? icon, MenuItemToggleType toggle_type)
	{
		Actions_Submenu = new AvaloniaList<MenuActionBase>();
		Title = title;
		Icon = icon;
		_command_can_execute = can_execute;
		_is_visible = is_visible;
		Command_Parameter = parameter;
		Toggle_Type=toggle_type;
	}

	public abstract void notifyCanExecuteChanged();	//REVIEW neccessity?
}

public class MenuAction : MenuActionBase
{
	private readonly Action<object?> _command_execute;
	private RelayCommand<object>? _command;

	public IRelayCommand Command
	{
		get
		{
			if (_command == null)
				_command = _command_can_execute==null ? new RelayCommand<object>(_command_execute) : new RelayCommand<object>(_command_execute, _command_can_execute);
			return _command;
		}
	}

	public MenuAction(string title, Predicate<object?> is_visible, Action<object?> execute, Predicate<object?>? can_execute=null, object? parameter = null, Bitmap? icon = null, MenuItemToggleType toggle_type= MenuItemToggleType.None) : base(title, is_visible, can_execute, parameter, icon, toggle_type)
	{
		_command_execute = execute;
	}

	public override void notifyCanExecuteChanged()
	{
		_command?.NotifyCanExecuteChanged();
	}
}

public class MenuActionAsync : MenuActionBase
{
	private readonly Func<object?, Task> _command_execute;
	private AsyncRelayCommand<object>? _command;

	public IAsyncRelayCommand Command
	{
		get
		{
			if (_command == null)
				_command = _command_can_execute==null ? new AsyncRelayCommand<object>(_command_execute) : new AsyncRelayCommand<object>(_command_execute, _command_can_execute);
			return _command;
		}
	}

	public MenuActionAsync(string title, Predicate<object?> is_visible, Func<object?, Task> execute, Predicate<object?>? can_execute=null, object? parameter = null, Bitmap? icon = null, MenuItemToggleType toggle_type = MenuItemToggleType.None) : base(title, is_visible, can_execute, parameter, icon, toggle_type)
	{
		_command_execute = execute;
	}

	public override void notifyCanExecuteChanged()
	{
		_command?.NotifyCanExecuteChanged();
	}
}
