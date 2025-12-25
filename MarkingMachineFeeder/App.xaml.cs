using Ewan.BusinessBonding;
using Ewan.Core.Logger;
using Ewan.Core.Security;
using Ewan.LogManager.Logger;
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
        private readonly UILogger _uiLogger = new UILogger();
        private readonly AppLogger _appLogger = AppLogger.Instance;

        protected override void OnStartup(StartupEventArgs e)
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            // 初始化日志系统
            Ewan.LogManager.Logger.LogManager.Initialize("log4net.config");
            _appLogger.Info("Log4net配置已加载");


            // 初始化Ewan.BusinessBonding MainController (包含所有Managers)
            if (MainController.Instance().Initialize())
            {
                _appLogger.Info("MainController初始化成功");
            }
            else
            {
                _appLogger.Error("MainController初始化失败");
                MessageBox.Show("系统初始化失败，程序将退出。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 默认以操作员身份登录
            var securityManager = SecurityManager.Instance();
            if (!securityManager.Authenticate("operator", "1"))
            {
                _appLogger.Error("登录错误: 默认操作员登录失败");
                MessageBox.Show("默认用户登录失败，程序将退出。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            _uiLogger.Info("系统初始化成功");

            // 启动流程
            var streamController = StreamController.Instance();
            streamController.StartRun();
            _uiLogger.Info("流处理已启动");

            // 手动配置Prism ViewModelLocator
            ConfigureViewModelLocator();

            base.OnStartup(e);
        }

        private void ConfigureViewModelLocator()
        {
            // 配置ViewModel到View的映射
            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
            ViewModelLocationProvider.Register<Windows.LogWindow, LogWindowViewModel>();
            
            _uiLogger.Info("ViewModelLocator已配置");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var controlService = SystemControlService.Instance();
                controlService.EnsurePauseRecoveryBeforeShutdown();
            }
            catch (System.Exception ex)
            {
                _uiLogger.ErrorRaw("关闭前执行停止复原脉冲失败: {0}", ex.Message);
            }

            // 停止所有流程
            try
            {
                var streamController = StreamController.Instance();
                streamController.StopRun();
                _uiLogger.Info("流处理已停止");
            }
            catch (System.Exception ex)
            {
                _uiLogger.ErrorRaw("关闭前停止流程失败: {0}", ex.Message);
            }

            // 关闭时必须销毁所有 Manager（包含 MesManager），否则部分后台线程会导致进程静默驻留
            try
            {
                MainController.Instance().Destroy();
                _uiLogger.Info("资源释放完成");
            }
            catch (System.Exception ex)
            {
                _uiLogger.ErrorRaw("关闭前释放资源失败: {0}", ex.Message);
            }

            // 调用基类方法
            base.OnExit(e);
        }
    }
}
