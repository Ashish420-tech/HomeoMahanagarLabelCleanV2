using System.Configuration;
using System.Data;
using System.Windows;
using System;
using System.Threading.Tasks;
using HomeoMahanagarLabelCleanV2.Logging;

namespace HomeoMahanagarLabelCleanV2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ensure logging directories/files exist as early as possible so startup errors are captured
            try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Initialize(); } catch { }
            try { HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log("Application starting"); } catch { }

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            // First-run: if no medicines present in storage, try to seed from remedies.xlsx next to exe
            try
            {
                var storage = Services.AppState.Storage;
                if ((storage.Medicines == null || storage.Medicines.Count == 0))
                {
                    var exeFolder = AppDomain.CurrentDomain.BaseDirectory;
                    var seedPath = System.IO.Path.Combine(exeFolder, "remedies.xlsx");
                    if (System.IO.File.Exists(seedPath))
                    {
                        var list = Helpers.ExcelHelper.ReadExcel(seedPath);
                        if (list != null && list.Count > 0)
                        {
                            storage.Medicines = new System.Collections.Generic.List<Models.Medicine>();
                            foreach (var m in list)
                            {
                                storage.Medicines.Add(new Models.Medicine
                                {
                                    LatinName = m.LatinName,
                                    CommonName = m.CommonName
                                });
                            }
                            Services.AppState.Save();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logging.AppLogger.Log(ex);
            }

            // Main window is created via StartupUri in App.xaml
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            AppLogger.Log(e.Exception);
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLogger.Log(ex);
                Services.DiagnosticsService.Capture(ex);
            }
        }

        private void App_DispatcherUnhandledException(object? sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Log(e.Exception);
            Services.DiagnosticsService.Capture(e.Exception);
            var paths = string.Join("\n", AppLogger.GetLogPaths());
            MessageBox.Show(e.Exception.Message + "\n\nSee logs:\n" + paths, "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }

}
