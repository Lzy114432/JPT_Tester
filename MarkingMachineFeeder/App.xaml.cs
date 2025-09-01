using Ewan.BusinessBonding;
using Ewan.Core.Logger;
using Ewan.Core.Security;
using MarkingMachineFeeder.Viewmodel;
using Prism.Mvvm;
using System.IO;
using System.Windows;

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
            // 初始化日志系统
            Ewan.LogManager.Logger.LogManager.Initialize("log4net.config");
            
            // 设置 IOLogger 的资源类型
            Ewan.LogManager.Logger.IOLogger.Instance.SetResourceType(typeof(Ewan.Resources.LogMessages));
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.Log4netConfigLoaded);


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
            if (!securityManager.Authenticate("operator", "1"))
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.LoginError, "默认操作员登录失败");
                MessageBox.Show("默认用户登录失败，程序将退出。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemInitialized);

            // 启动流程
            var streamController = StreamController.Instance();
            streamController.StartRun();
            _uiLogger.Info(() => Ewan.Resources.LogMessages.StreamProcessStarted);

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

        protected override void OnExit(ExitEventArgs e)
        {
            // 停止所有流程
            var streamController = StreamController.Instance();
            streamController.StopRun();
            _uiLogger.Info(() => Ewan.Resources.LogMessages.StreamProcessStopped);

            // 调用基类方法
            base.OnExit(e);
        }
    }
}