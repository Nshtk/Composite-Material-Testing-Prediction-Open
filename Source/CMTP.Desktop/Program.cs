using System;
using System.IO;

using Avalonia;

using FCGR.Common.Utilities;

using CMTP.Avalonia.Views;
using CMTP.Avalonia.Managers;

namespace FCGR.Desktop;

internal class Program
{
	public static AppBuilder BuildAvaloniaApp() //Avalonia configuration, don't remove; also used by visual designer.
	{
		return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont(); //TODO... OS specific platform options .With(new Win32PlatformOptions() {RenderingMode=Win32RenderingMode. UseWindowsUIComposition=false});
	}

	[STAThread] //Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant code before AppMain is called: things aren't initialized yet and stuff might break.
	public static void Main(string[] args)   //NOTE Main cannot be async (but can use task.GetAwaiter().GetResult() if needed?)
	{
		try
		{
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
			AppManager.Logger.logMessage($"Exiting normally.", MESSAGE_SEVERITY.COMMON);     //Unreachable code if the excetion is thrown.
		}
		catch (Exception ex)
		{
			AppManager.Logger.logMessage($"Exiting unexpectendly with the following exception: {ex.Message}.{Environment.NewLine}Stacktrace:{ex.StackTrace}", MESSAGE_SEVERITY.CRITICAL);
		}
		finally
		{
			AppManager.Dispose();
		}
#if RELEASE
		try
		{
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
			Logger.logMessage($"Exiting normally.", MESSAGE_SEVERITY.COMMON);	//Unreachable code if the excetion is thrown.
		}
		catch (Exception ex)
		{
			Logger.logMessage($"Exiting unexpectendly with the following exception: {ex.Message}.{Environment.NewLine}Stacktrace:{ex.StackTrace}", MESSAGE_SEVERITY.CRITICAL);
		}
		finally
		{
			if (ServerManager.Is_Server_Running)
				ServerManager.stopServerAsync();
		}
#endif
	}
}
