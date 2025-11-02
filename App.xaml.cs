using System.Runtime.InteropServices;

namespace FhBrowser
{
    public partial class App : Application
    {
        private static Mutex? _mutex;

        public App()
        {
            InitializeComponent();

            // --- ИСПРАВЛЕНИЕ ---
            // Проверяем до того, как что-либо создастся
            if (!IsSingleInstance())
            {
                // Если приложение уже запущено, нам нужно вежливо его закрыть.
                // Мы не можем просто прервать конструктор, поэтому создаем пустую страницу,
                // чтобы приложение не упало, и немедленно даем команду на выход.
                MainPage = new ContentPage(); 
                Application.Current?.Quit();
                return;
            }
            // --- КОНЕЦ ИСПРАВЛЕНИЯ ---

            // Создаем главную страницу, если это первый экземпляр
            MainPage = new BrowserPage();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
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
            // Эта логика будет работать только при сборке под Windows
            _mutex = new Mutex(true, "FH_MIN_BROWSER_SINGLE_INSTANCE", out bool isNew);
            return isNew;
            #elif MACCATALYST
            // Для macOS эта логика Mutex не работает, требуется другой подход.
            // Пока просто разрешаем несколько экземпляров.
            return true;
            #else
            return true;
            #endif
        }
    }
}
