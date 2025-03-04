using System.Windows;
using System.Linq;

namespace WpfBrowser
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            mainWindow.Show();

            // 명령줄 인자 처리
            if (e.Args.Length >= 2 && e.Args[0] == "--url")
            {
                // URL 인자가 있으면 새 탭에서 열기
                mainWindow.AddNewTab(e.Args[1]);
            }
        }
    }
} 