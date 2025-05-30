using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using CommunityToolkit.Mvvm.Input;

using CMTP.Avalonia.ViewModels.Windows;
using FCGR.Common.Utilities;
using CMTP.Avalonia.Managers;
using CMTP.Avalonia.Views.Windows;

namespace CMTP.Avalonia.Views;

/// <summary>
///		App view.
/// </summary>
public class App : Application  //NOTE To access instance use App.Current
{
	#region Fields
	#region Fields.Avalonia
	private RelayCommand _command_open_settings;
	private RelayCommand _command_exit;
	#endregion
	#endregion
	#region Properties
	#region Properties.Avalonia
	public IRelayCommand Command_Open_Settings
	{
		get
		{
			if (_command_open_settings == null)
				_command_open_settings = new RelayCommand(showSettingsWindow, () => { return ProjectManager.Is_Project_Opened; });
			return _command_open_settings;
		}
	}
	public IRelayCommand Command_Exit
	{
		get
		{
			if (_command_exit == null)
				_command_exit = new RelayCommand(exit);
			return _command_exit;
		}
	}
	#endregion
	#endregion
	public App()
	{
		DataContext = this;
	}
	#region Methods
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}
	public override void OnFrameworkInitializationCompleted()
	{
		BindingPlugins.DataValidators.RemoveAt(0);

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
			desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
		}
		base.OnFrameworkInitializationCompleted();
	}
	public void showProjectWindow()
	{
		ProjectWindow project_window = new();
		project_window.ShowDialog(AppManager.Windows[nameof(ViewModelMainWindow)]);  //Modal window
		project_window.Closing += (sender, e) =>
		{
			if (!ProjectManager.Is_Project_Opened)
				AppManager.Logger.printMessage("Проект не открыт.", MESSAGE_SEVERITY.WARNING);
			AppManager.Windows.TryRemove(nameof(ViewModelProjectWindow), out Window _);
		};
		AppManager.Windows.TryAdd(nameof(ViewModelProjectWindow), project_window);
	}
	public void showSettingsWindow()
	{
		SettingsWindow settings_window = new SettingsWindow();
		settings_window.ShowDialog(AppManager.Windows[nameof(ViewModelMainWindow)]);
		settings_window.Closing += (sender, e) =>
		{
			Task.Run(AppManager.Settings_Manager.saveSettingsAsync);
			AppManager.Windows.TryRemove(nameof(ViewModelSettingsWindow), out Window _);
		};
		AppManager.Windows.TryAdd(nameof(ViewModelSettingsWindow), settings_window);
	}
	public void exit()
	{
		AppManager.Windows[nameof(ViewModelMainWindow)].Close();    ///Terminate program on MainWindow close <see cref="App.OnFrameworkInitializationCompleted"/>
	}
	#endregion
}
