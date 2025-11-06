using System;
using System.Globalization;
using System.Threading;
using Ewan.Core.Attribute;

namespace Ewan.Core.Culture
{
    [Manager(Priority = 1)]
    public class CultureManager : BaseManager<CultureManager>
    {
        public event EventHandler<CultureChangedEventArgs> CultureChanged;
        
        private readonly CultureInfo _defaultCulture = CultureInfo.GetCultureInfo("zh-CN");
        private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("zh-CN");
        
        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            private set
            {
                if (_currentCulture != value)
                {
                    var oldCulture = _currentCulture;
                    _currentCulture = value;
                    ApplyCulture();
                    OnCultureChanged(new CultureChangedEventArgs(oldCulture, value));
                }
            }
        }

        public override bool Init()
        {
            _currentCulture = _defaultCulture;
            ApplyCulture();
            _uiLogger.Info("文化已设置为 {0}", _defaultCulture.Name);
            return base.Init();
        }

        public void SetCulture(string cultureName)
        {
            try
            {
                if (!string.Equals(cultureName, _defaultCulture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _uiLogger.Warn("忽略不受支持的文化设置: {0}", cultureName);
                    return;
                }

                CurrentCulture = _defaultCulture;
                _uiLogger.Info("文化已固定为 {0}", _defaultCulture.Name);
            }
            catch (CultureNotFoundException ex)
            {
                _uiLogger.Error("无效的文化: {0}", cultureName, ex.Message);
                throw;
            }
        }

        public void SetCulture(CultureInfo culture)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            if (!string.Equals(culture.Name, _defaultCulture.Name, StringComparison.OrdinalIgnoreCase))
            {
                _uiLogger.Warn("忽略不受支持的文化设置: {0}", culture.Name);
                return;
            }

            CurrentCulture = _defaultCulture;
        }

        private void ApplyCulture()
        {
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            CultureInfo.DefaultThreadCurrentCulture = _currentCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;
        }

        private void OnCultureChanged(CultureChangedEventArgs e)
        {
            CultureChanged?.Invoke(this, e);
        }

        private string GetSavedLanguage()
        {
            return _defaultCulture.Name;
        }

        private void SaveLanguage(string cultureName)
        {
            // 国际化已禁用，不再持久化语言信息
        }
    }

    public class CultureChangedEventArgs : EventArgs
    {
        public CultureInfo OldCulture { get; }
        public CultureInfo NewCulture { get; }

        public CultureChangedEventArgs(CultureInfo oldCulture, CultureInfo newCulture)
        {
            OldCulture = oldCulture;
            NewCulture = newCulture;
        }
    }
}