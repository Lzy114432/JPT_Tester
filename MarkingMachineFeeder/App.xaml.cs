using System.IO;
using System.Windows;
using Ewan.BusinessBonding;
using Ewan.Core.Logger;
using Ewan.Core.Culture;
using Ewan.Core.Security;
using log4net;
using log4net.Config;
using Prism.Mvvm;
using MarkingMachineFeeder.Viewmodel;
using MarkingMachineFeeder.Windows;

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
                MessageBox.Show("系统初始化失败，程序将退出。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 默认以操作员身份登录
            var securityManager = SecurityManager.Instance();
            if (!securityManager.Authenticate("operator", "123456"))
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.LoginError, "默认操作员登录失败");
                MessageBox.Show("默认用户登录失败，程序将退出。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemInitialized);

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