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
        
        private CultureInfo _currentCulture = CultureInfo.CurrentCulture;
        
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
            var savedLanguage = GetSavedLanguage();
            if (!string.IsNullOrEmpty(savedLanguage))
            {
                try
                {
                    SetCulture(savedLanguage);
                }
                catch (Exception ex)
                {
                    _uiLogger.Warn("文化管理器初始化错误: {0}", ex.Message);
                    SetCulture("en-US");
                }
            }
            return base.Init();
        }

        public void SetCulture(string cultureName)
        {
            try
            {
                var culture = new CultureInfo(cultureName);
                CurrentCulture = culture;
                SaveLanguage(cultureName);
                _uiLogger.Info("文化已更改为 {0}", cultureName);
            }
            catch (CultureNotFoundException ex)
            {
                _uiLogger.Error("无效的文化: {0}", cultureName, ex.Message);
                throw;
            }
        }

        public void SetCulture(CultureInfo culture)
        {
            CurrentCulture = culture ?? throw new ArgumentNullException(nameof(culture));
            SaveLanguage(culture.Name);
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
            try
            {
                return Properties.Settings.Default.Language;
            }
            catch
            {
                return "en-US";
            }
        }

        private void SaveLanguage(string cultureName)
        {
            try
            {
                Properties.Settings.Default.Language = cultureName;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                _uiLogger.Warn("保存语言设置错误: {0}", ex.Message);
            }
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