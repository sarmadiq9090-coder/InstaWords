using System;
using InstaWords.App.Services;

namespace InstaWords.App
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            using (var instance = SingleInstance.Acquire())
            {
                if (!instance.IsFirst)
                {
                    // Already running (maybe hidden in the tray): bring that window up instead.
                    instance.SignalFirst();
                    return 0;
                }
                var app = new App { StartHidden = StartupService.IsStartupLaunch(args) };
                instance.Listen(() => app.Dispatcher.BeginInvoke(new Action(() => app.ShowMainWindow())));
                app.InitializeComponent();
                return app.Run();
            }
        }
    }
}
