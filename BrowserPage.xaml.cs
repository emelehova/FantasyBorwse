using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FhBrowser
{
    public partial class BrowserPage : ContentPage
    {
        // --- Настройки из вашего Program.cs ---
        private const string StartUrl = "https://fantasy-hub.ru/dashboard";
        private bool _mobileMode = false;
        private string _defaultUA = "";
        private const string MobileUA = "Mozilla/5.0 (Linux; Android 12; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Mobile Safari/537.36";
        private Process? _caffeinateProcess;

        // --- Флаги, которые передавались через args ---
        // В MAUI аргументы передаются иначе, для примера жестко зададим их
        private readonly bool _isLite = true;
        private readonly bool _lazyMedia = true;
        private readonly bool _blockAssets = false; // В MAUI WebView нет простого API для этого
        private readonly string _wsMode = "raf"; // "micro", "raf", "ms"
        private readonly int _wsDelay = 0;

        // --- PInvoke для Windows (запрет сна) ---
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;

        public BrowserPage()
        {
            InitializeComponent();
            PreventSleep();
            
            // Загружаем WebView
            MyWebView.Source = StartUrl;
        }

        private void OnMobileToggle_Clicked(object? sender, EventArgs e)
        {
            _mobileMode = !_mobileMode;
            
            // В MAUI UserAgent нужно устанавливать *перед* навигацией
            MyWebView.UserAgent = _mobileMode ? MobileUA : _defaultUA;
            BtnMobile.Text = _mobileMode ? "Моб. версия: вкл" : "Моб. версия: выкл";

            // Меняем размер окна (только на Windows и macOS)
            #if WINDOWS || MACCATALYST
            var window = this.GetParentWindow();
            if (window != null)
            {
                if (_mobileMode)
                {
                    window.Width = 420;
                    window.Height = 800;
                }
                else
                {
                    window.Width = 1200;
                    window.Height = 800;
                }
                // TODO: Центрировать окно
            }
            #endif

            // Перезагружаем страницу, чтобы применить UserAgent
            MyWebView.Reload();
        }

        private async void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
        {
            if (e.Result == WebNavigationResult.Success)
            {
                // 1. Получаем User Agent (если еще нету)
                if (string.IsNullOrEmpty(_defaultUA))
                {
                    _defaultUA = await MyWebView.EvaluateJavaScriptAsync("navigator.userAgent");
                    
                    // Устанавливаем UserAgent (на случай, если он не был установлен до первой загрузки)
                    MyWebView.UserAgent = _mobileMode ? MobileUA : _defaultUA;
                }

                // 2. Внедряем JS-оптимизации
                if (_isLite)
                {
                    await InjectJs(jsKillAnimations);
                    await InjectJs(jsThrottleRAF);
                }
                if (_lazyMedia)
                {
                    await InjectJs(jsLazy);
                }
                
                string jsWsBatchFinal = jsWsBatch
                    .Replace("{{MODE}}", _wsMode)
                    .Replace("{{DELAY}}", _wsDelay.ToString());
                await InjectJs(jsWsBatchFinal);
            }
        }

        private async Task InjectJs(string script)
        {
            try
            {
                await MyWebView.EvaluateJavaScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка внедрения JS: {ex.Message}");
            }
        }

        private void PreventSleep()
        {
            #if WINDOWS
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
            #elif MACCATALYST
            try
            {
                var psi = new ProcessStartInfo("caffeinate", "-d -i -s")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _caffeinateProcess = Process.Start(psi);
                Debug.WriteLine("Процесс Caffeinate запущен.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Не удалось запустить caffeinate: {ex.Message}");
            }
            #endif
        }

        // --- Скрипты из вашего оригинального кода ---

        private const string jsKillAnimations = @"
(function(){
  const style = document.createElement('style');
  style.id = 'fh-lite-style';
  style.textContent = `
    * { animation: none !important; transition: none !important; text-shadow: none !important; box-shadow: none !important; }
    html, body { scroll-behavior: auto !important; overscroll-behavior: contain !important; -webkit-font-smoothing: auto !important; }
  `;
  document.documentElement.appendChild(style);
})();
";

        private const string jsThrottleRAF = @"
(function(){
  const targetFps = 30;
  const frameInterval = 1000 / targetFps;
  let last = performance.now();
  const _raf = window.requestAnimationFrame;
  window.requestAnimationFrame = function(cb){
    return _raf(function ts(now){
      if (now - last < frameInterval) { return _raf(ts); }
      last = now;
      cb(now);
    });
  };
  const _setTimeout = window.setTimeout;
  window.setTimeout = function(fn, t){
    if (typeof t === 'number' && t < 16) t = 16;
    return _setTimeout(fn, t);
  };
})();
";

        private const string jsLazy = @"
(function(){
  function tweak(el){
    if (el.tagName === 'IMG'){
      if (!el.loading) el.loading = 'lazy';
      el.decoding = 'async';
    }
    if (el.tagName === 'IFRAME'){
      if (!el.loading) el.loading = 'lazy';
    }
    if (el.tagName === 'VIDEO'){
      try{ el.preload = 'metadata'; el.autoplay = false; el.pause(); }catch(e){}
    }
  }
  const m = new MutationObserver((list)=>{
    for (const r of list){
      if (r.type === 'childList'){
        r.addedNodes && r.addedNodes.forEach(n=>{
          if (n.nodeType === 1){
            tweak(n);
            n.querySelectorAll && n.querySelectorAll('img,iframe,video').forEach(tweak);
          }
        });
      }
    }
  });
  m.observe(document.documentElement, { subtree:true, childList:true });
  document.querySelectorAll('img,iframe,video').forEach(tweak);
})();
";

        private const string jsWsBatch = @"
(function(){
  const MODE = '{{MODE}}';
  const DELAY = {{DELAY}};
  const NativeWS = window.WebSocket;
  window.WebSocket = function(url, protocols){
    const ws = new NativeWS(url, protocols);
    const handlers = new Set();
    let onhandler = null;
    function deliver(ev){
      const e = new MessageEvent('message', { data: ev.data, origin: ev.origin, lastEventId: ev.lastEventId });
      if (onhandler) { try{ onhandler.call(ws, e); }catch(_){ } }
      handlers.forEach(h=>{ try{ h.call(ws, e); }catch(_){ } });
    }
    let queue = [];
    let scheduled = false;
    function flush(){ scheduled = false; const q = queue; queue = []; for (let i=0;i<q.length;i++) deliver(q[i]); }
    function schedule(){
      if (scheduled) return;
      scheduled = true;
      if (MODE === 'raf') { requestAnimationFrame(flush); }
      else if (MODE === 'ms') { setTimeout(flush, DELAY); }
      else { Promise.resolve().then(flush); }
    }
    ws.addEventListener('message', function(ev){ queue.push(ev); schedule(); });
    Object.defineProperty(ws, 'onmessage', {
      get(){ return onhandler; },
      set(h){ onhandler = (typeof h === 'function') ? h : null; }
    });
    const oAdd = ws.addEventListener.bind(ws);
    ws.addEventListener = function(type, handler, options){
      if (type === 'message' && typeof handler === 'function'){ handlers.add(handler); return; }
      return oAdd(type, handler, options);
    };
    const oRem = ws.removeEventListener.bind(ws);
    ws.removeEventListener = function(type, handler, options){
      if (type === 'message' && typeof handler === 'function'){ handlers.delete(handler); return; }
      return oRem(type, handler, options);
    };
    return ws;
  };
})();
";
    }
}
