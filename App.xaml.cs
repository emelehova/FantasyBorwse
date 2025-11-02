using System.Runtime.InteropServices;

namespace FhBrowser
{
    public partial class App : Application
    {
        // Для проверки единственного экземпляра на Windows
        private static Mutex? _mutex;

        public App()
        {
            InitializeComponent();

            // Создаем главную страницу
            MainPage = new BrowserPage();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Проверка на один экземпляр
            if (!IsSingleInstance())
            {
                // Если приложение уже запущено, выходим
                // TODO: Можно добавить логику для активации существующего окна
                Application.Current?.Quit();
                return null!; // Возвращаем null, так как приложение закроется
            }
            
            var window = base.CreateWindow(activationState);
            
            // Настройка размеров и заголовка окна
            window.Title = "FH Browser";
            window.Width = 1200;
            window.Height = 800;

            return window;
        }

        private static bool IsSingleInstance()
        {
            #if WINDOWS
            _mutex = new Mutex(true, "FH_MIN_BROWSER_SINGLE_INSTANCE", out bool isNew);
            return isNew;
            #elif MACCATALYST
            // На macOS Mutex(name) не работает для межпроцессной блокировки.
            // Для macOS это более сложная задача, часто используют файл-лок или URL-схемы.
            // Для простоты пока считаем, что на macOS можно запустить несколько копий.
            return true;
            #else
            return true;
            #endif
        }
    }
}
