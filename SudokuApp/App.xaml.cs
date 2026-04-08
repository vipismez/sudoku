using System.Windows;

namespace SudokuApp;

/// <summary>
/// 应用入口，负责挂接全局异常日志。
/// </summary>
public partial class App : Application
{
	public App()
	{
		// 桌面应用异常如果没有统一收口，调试和问题复现会非常困难。
		AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
		DispatcherUnhandledException += AppOnDispatcherUnhandledException;
		TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		AppLogger.Info($"应用启动。日志文件: {AppLogger.CurrentLogFilePath}");
	}

	private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			AppLogger.Error("AppDomain 未处理异常", ex);
		}
		else
		{
			AppLogger.Error("AppDomain 未处理异常，异常对象无法转换为 Exception");
		}
	}

	private void AppOnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		AppLogger.Error("UI 线程未处理异常", e.Exception);
	}

	private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		AppLogger.Error("Task 未观察异常", e.Exception);
		e.SetObserved();
	}
}

