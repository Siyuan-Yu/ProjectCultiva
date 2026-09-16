using System.Windows;
using System.Windows.Threading;
using SurfaceAuthoring.EditorCommon;
namespace WorldComposer;
public partial class App : Application
{
    public App(){DispatcherUnhandledException+=OnDispatcherUnhandledException;AppDomain.CurrentDomain.UnhandledException+=OnDomainUnhandledException;TaskScheduler.UnobservedTaskException+=OnUnobservedTaskException;}
    protected override void OnStartup(StartupEventArgs e){base.OnStartup(e);try{var window=new MainWindow();MainWindow=window;window.Show();}catch(Exception ex){EditorCrashReporter.Report("WorldComposer",ex);Shutdown(-1);}}
    private void OnDispatcherUnhandledException(object sender,DispatcherUnhandledExceptionEventArgs e){EditorCrashReporter.Report("WorldComposer",e.Exception);e.Handled=true;Shutdown(-1);}
    private static void OnDomainUnhandledException(object sender,UnhandledExceptionEventArgs e){EditorCrashReporter.Report("WorldComposer",e.ExceptionObject as Exception??new Exception("发生未知未处理错误。"));}
    private static void OnUnobservedTaskException(object? sender,UnobservedTaskExceptionEventArgs e){EditorCrashReporter.Report("WorldComposer",e.Exception);e.SetObserved();}
}
