namespace SkyPilot;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.ThreadException += (s, e) =>
        {
            MessageBox.Show($"Thread Error:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "SkyPilot Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Fatal Error:\n{ex?.Message}\n\n{ex?.StackTrace}",
                "SkyPilot Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.Run(new UI.MainForm());
    }
}
