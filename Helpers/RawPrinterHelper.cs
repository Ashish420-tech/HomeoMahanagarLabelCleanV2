using System;
using System.Runtime.InteropServices;
using System.Printing;
using System.Configuration;

namespace HomeoMahanagarLabelCleanV2.Helpers
{
    public static class RawPrinterHelper
{
    [DllImport("winspool.Drv", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool OpenPrinter(
        string pPrinterName,
        out IntPtr phPrinter,
        IntPtr pDefault);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool WritePrinter(
        IntPtr hPrinter,
        IntPtr pBytes,
        int dwCount,
        out int dwWritten);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    // 🔹 STEP 1: Validate & get printer name
    public static string GetLabelPrinterName()
    {
        // prefer stored app state setting
        string printerName = null;
        try
        {
            printerName = HomeoMahanagarLabelCleanV2.Services.AppState.Storage.LabelPrinterName;
        }
        catch { }

        if (string.IsNullOrWhiteSpace(printerName))
        {
            // fallback to app.config
            try
            {
                printerName = ConfigurationManager.AppSettings["LabelPrinterName"];
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(printerName))
            throw new Exception("Label printer name is not configured.");

        LocalPrintServer server = new LocalPrintServer();

        foreach (PrintQueue pq in server.GetPrintQueues())
        {
            if (pq.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase))
            {
                pq.Refresh();

                if (pq.IsOffline)
                    throw new Exception($"Printer '{printerName}' is OFFLINE.");

                if (pq.IsPaused)
                    throw new Exception($"Printer '{printerName}' is PAUSED.");

                return pq.Name;
            }
        }



        throw new Exception($"Printer '{printerName}' NOT FOUND on this system.");
    }

    public static void SetLabelPrinterName(string name)
    {
        HomeoMahanagarLabelCleanV2.Services.AppState.Storage.LabelPrinterName = name;
        HomeoMahanagarLabelCleanV2.Services.AppState.Save();
    }

    // 🔹 STEP 2: RAW printing with full error handling
    public static void SendStringToPrinter(string data)
    {
        string printerName = GetLabelPrinterName();

        if (!OpenPrinter(printerName, out IntPtr printerHandle, IntPtr.Zero))
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"Failed to open printer '{printerName}'. Win32 Error: {error}");
        }

        try
        {
            IntPtr pBytes = Marshal.StringToCoTaskMemAnsi(data);

            bool success = WritePrinter(
                printerHandle,
                pBytes,
                data.Length,
                out int bytesWritten);

            Marshal.FreeCoTaskMem(pBytes);

            if (!success || bytesWritten == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to write to printer. Win32 Error: {error}");
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }
}

}
