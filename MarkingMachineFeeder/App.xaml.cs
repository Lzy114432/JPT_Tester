using System.IO;
using System.Windows;
using Ewan.BusinessBonding;
using Ewan.Core.Logger;
using Ewan.Core.Culture;
using log4net;
using log4net.Config;
using Prism.Mvvm;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));

        protected override void OnStartup(StartupEventArgs e)
        {
            // 手动配置log4net
            var logConfigFile = new FileInfo("log4net.config");
            if (logConfigFile.Exists)
            {
                XmlConfigurator.Configure(logConfigFile);
                _uiLogger.Info(() => Ewan.Resources.LogMessages.Log4netConfigLoaded);
            }
            else
            {
                BasicConfigurator.Configure();
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.Log4netConfigNotFound);
            }

            // 初始化Ewan.BusinessBonding MainController (包含所有Managers)
            if (MainController.Instance().Initialize())
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.MainControllerInitialized);
            }
            else
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.MainControllerInitializationFailed);
            }

            // 手动配置Prism ViewModelLocator
            ConfigureViewModelLocator();

            base.OnStartup(e);
        }

        private void ConfigureViewModelLocator()
        {
            // 配置ViewModel到View的映射
            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
            ViewModelLocationProvider.Register<Windows.LogWindow, LogWindowViewModel>();
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ViewModelLocatorConfigured);
        }
    }
}