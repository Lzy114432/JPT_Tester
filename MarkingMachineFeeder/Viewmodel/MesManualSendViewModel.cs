using Ewan.Core.Mes;
using Ewan.Core.ScanCode;
using Ewan.Model.System;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace MarkingMachineFeeder.Viewmodel
{
    public enum RingLineAction
    {
        FeedingQianLiaocang,
        FeedingQianLiaocangSuccess,
        UnloadingQianLiaocang,
        FeedingZhongLiaocang,
        UnloadingZhongLiaocang,
        FeedingQingxihongganji,
        FeedingHouLiaocang,
        UnloadingHouLiaocang
    }

    public sealed class RingLineActionOption
    {
        public RingLineActionOption(string text, RingLineAction action)
        {
            Text = text;
            Action = action;
        }

        public string Text { get; }
        public RingLineAction Action { get; }

        public override string ToString()
        {
            return Text;
        }
    }

    public class MesManualSendViewModel : BindableBase, IDisposable
    {
        private readonly MesManager _mesManager;
        private IDisposable _feedingQianLiaocangResponseSubscription;

        private bool _mesEnabled;
        private string _brokerHost;
        private int _brokerPort;
        private string _clientId;
        private string _userName;
        private string _password;
        private bool _cleanSession;
        private int _keepAliveSeconds;
        private string _ringLineDeviceId;
        private string _ringLineDeviceCode;

        private bool _isConnected;
        private bool _isRingLineInitialized;
        private bool _isResponseSubscribed;
        private string _brokerEndpointText;

        private RingLineActionOption _selectedAction;
        private string _plateCode;
        private string _billNoWip;
        private string _feedingLiaokuangCode;

        private string _sendLogText;
        private string _receiveLogText;

        public MesManualSendViewModel()
        {
            _mesManager = MesManager.Instance();

            ActionOptions = new ObservableCollection<RingLineActionOption>
            {
                new RingLineActionOption("前料仓上料 FeedingQianLiaocang", RingLineAction.FeedingQianLiaocang),
                new RingLineActionOption("前料仓上料成功 FeedingQianLiaocangSuccess", RingLineAction.FeedingQianLiaocangSuccess),
                new RingLineActionOption("前料仓卸料 UnloadingQianLiaocang", RingLineAction.UnloadingQianLiaocang),
                new RingLineActionOption("中料仓上料 FeedingZhongLiaocang", RingLineAction.FeedingZhongLiaocang),
                new RingLineActionOption("中料仓卸料 UnloadingZhongLiaocang", RingLineAction.UnloadingZhongLiaocang),
                new RingLineActionOption("清洗烘干机上料 FeedingQingxihongganji", RingLineAction.FeedingQingxihongganji),
                new RingLineActionOption("后料仓上料 FeedingHouLiaocang", RingLineAction.FeedingHouLiaocang),
                new RingLineActionOption("后料仓卸料 UnloadingHouLiaocang", RingLineAction.UnloadingHouLiaocang)
            };
            SelectedAction = ActionOptions.Count > 0 ? ActionOptions[0] : null;

            RefreshCommand = new DelegateCommand(ExecuteRefresh);
            ConnectCommand = new DelegateCommand(ExecuteConnect);
            DisconnectCommand = new DelegateCommand(ExecuteDisconnect);
            InitializeRingLineCommand = new DelegateCommand(ExecuteInitializeRingLine);
            SubscribeResponseCommand = new DelegateCommand(ExecuteSubscribeResponse);
            UnsubscribeResponseCommand = new DelegateCommand(ExecuteUnsubscribeResponse);
            TriggerScanCommand = new DelegateCommand(ExecuteTriggerScan);
            SendCommand = new DelegateCommand(ExecuteSend);
            ClearLogsCommand = new DelegateCommand(ExecuteClearLogs);

            ExecuteRefresh();
            AppendReceiveLog("提示：本窗口用于手动发送MES上行；下行订阅仅用于调试展示。");
        }

        public ObservableCollection<RingLineActionOption> ActionOptions { get; }

        public RingLineActionOption SelectedAction
        {
            get => _selectedAction;
            set => SetProperty(ref _selectedAction, value);
        }

        public bool MesEnabled
        {
            get => _mesEnabled;
            set => SetProperty(ref _mesEnabled, value);
        }

        public string BrokerHost
        {
            get => _brokerHost;
            set => SetProperty(ref _brokerHost, value);
        }

        public int BrokerPort
        {
            get => _brokerPort;
            set => SetProperty(ref _brokerPort, value);
        }

        public string ClientId
        {
            get => _clientId;
            set => SetProperty(ref _clientId, value);
        }

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public bool CleanSession
        {
            get => _cleanSession;
            set => SetProperty(ref _cleanSession, value);
        }

        public int KeepAliveSeconds
        {
            get => _keepAliveSeconds;
            set => SetProperty(ref _keepAliveSeconds, value);
        }

        public string RingLineDeviceId
        {
            get => _ringLineDeviceId;
            set => SetProperty(ref _ringLineDeviceId, value);
        }

        public string RingLineDeviceCode
        {
            get => _ringLineDeviceCode;
            set => SetProperty(ref _ringLineDeviceCode, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    RaisePropertyChanged(nameof(ConnectionStatusText));
                }
            }
        }

        public bool IsRingLineInitialized
        {
            get => _isRingLineInitialized;
            set
            {
                if (SetProperty(ref _isRingLineInitialized, value))
                {
                    RaisePropertyChanged(nameof(RingLineStatusText));
                }
            }
        }

        public bool IsResponseSubscribed
        {
            get => _isResponseSubscribed;
            set
            {
                if (SetProperty(ref _isResponseSubscribed, value))
                {
                    RaisePropertyChanged(nameof(ResponseSubscribeStatusText));
                }
            }
        }

        public string ConnectionStatusText => IsConnected ? "已连接" : "未连接";
        public string RingLineStatusText => IsRingLineInitialized ? "已初始化" : "未初始化";
        public string ResponseSubscribeStatusText => IsResponseSubscribed ? "已订阅" : "未订阅";

        public string BrokerEndpointText
        {
            get => _brokerEndpointText;
            set => SetProperty(ref _brokerEndpointText, value);
        }

        public string PlateCode
        {
            get => _plateCode;
            set => SetProperty(ref _plateCode, value);
        }

        public string BillNoWip
        {
            get => _billNoWip;
            set => SetProperty(ref _billNoWip, value);
        }

        public string FeedingLiaokuangCode
        {
            get => _feedingLiaokuangCode;
            set => SetProperty(ref _feedingLiaokuangCode, value);
        }

        public string SendLogText
        {
            get => _sendLogText;
            set => SetProperty(ref _sendLogText, value);
        }

        public string ReceiveLogText
        {
            get => _receiveLogText;
            set => SetProperty(ref _receiveLogText, value);
        }

        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand InitializeRingLineCommand { get; }
        public DelegateCommand SubscribeResponseCommand { get; }
        public DelegateCommand UnsubscribeResponseCommand { get; }
        public DelegateCommand TriggerScanCommand { get; }
        public DelegateCommand SendCommand { get; }
        public DelegateCommand ClearLogsCommand { get; }

        private void ExecuteRefresh()
        {
            try
            {
                SystemParametersManager.Instance.Reload();
                var parameters = SystemParametersManager.Instance.Parameters;

                MesEnabled = parameters != null && parameters.MesEnabled;
                BrokerHost = parameters?.MesBrokerHost ?? string.Empty;
                BrokerPort = parameters?.MesBrokerPort ?? 0;
                ClientId = parameters?.MesClientId ?? string.Empty;
                UserName = parameters?.MesUserName ?? string.Empty;
                Password = parameters?.MesPassword ?? string.Empty;
                CleanSession = parameters != null && parameters.MesCleanSession;
                KeepAliveSeconds = parameters?.MesKeepAliveSeconds ?? 0;
                RingLineDeviceId = parameters?.MesRingLineDeviceId ?? string.Empty;
                RingLineDeviceCode = parameters?.MesRingLineDeviceCode ?? string.Empty;

                AppendReceiveLog("已刷新MES参数配置。");
            }
            catch (Exception ex)
            {
                AppendReceiveLog("刷新配置异常: " + ex.Message);
            }
            finally
            {
                UpdateStatus();
            }
        }

        private void ExecuteConnect()
        {
            try
            {
                if (!_mesManager.ConfigureFromSystemParameters(connect: false))
                {
                    AppendReceiveLog("MES未启用或配置无效，请先在【系统参数设置】中启用并配置。");
                    return;
                }

                var ok = _mesManager.Connect();
                AppendReceiveLog(ok ? "MES连接成功。" : "MES连接失败，请检查Broker/网络。");
            }
            catch (Exception ex)
            {
                AppendReceiveLog("MES连接异常: " + ex.Message);
            }
            finally
            {
                UpdateStatus();
            }
        }

        private void ExecuteDisconnect()
        {
            try
            {
                _mesManager.Disconnect();
                AppendReceiveLog("MES已断开连接。");
            }
            catch (Exception ex)
            {
                AppendReceiveLog("断开连接异常: " + ex.Message);
            }
            finally
            {
                UpdateStatus();
            }
        }

        private void ExecuteInitializeRingLine()
        {
            try
            {
                var ok = _mesManager.InitializeRingLine(RingLineDeviceId, RingLineDeviceCode);
                AppendReceiveLog(ok ? "环线服务初始化成功。" : "环线服务初始化失败，请检查DeviceId/DeviceCode。");
            }
            catch (Exception ex)
            {
                AppendReceiveLog("环线服务初始化异常: " + ex.Message);
            }
            finally
            {
                UpdateStatus();
            }
        }

        private void ExecuteSubscribeResponse()
        {
            try
            {
                if (IsResponseSubscribed)
                {
                    AppendReceiveLog("已订阅，无需重复订阅。");
                    return;
                }

                if (!_mesManager.IsRingLineInitialized)
                {
                    if (!_mesManager.InitializeRingLine(RingLineDeviceId, RingLineDeviceCode))
                    {
                        AppendReceiveLog("订阅失败：环线服务未初始化。");
                        UpdateStatus();
                        return;
                    }
                }

                _feedingQianLiaocangResponseSubscription = _mesManager.OnFeedingQianLiaocangResponseText(msg =>
                {
                    AppendReceiveLog("[前料仓上料响应] " + msg);
                });

                IsResponseSubscribed = true;
                AppendReceiveLog("订阅成功：前料仓上料响应。");
            }
            catch (Exception ex)
            {
                AppendReceiveLog("订阅异常: " + ex.Message);
            }
            finally
            {
                UpdateStatus();
            }
        }

        private void ExecuteUnsubscribeResponse()
        {
            try
            {
                _feedingQianLiaocangResponseSubscription?.Dispose();
                _feedingQianLiaocangResponseSubscription = null;
                IsResponseSubscribed = false;
                AppendReceiveLog("已取消订阅。");
            }
            catch (Exception ex)
            {
                AppendReceiveLog("取消订阅异常: " + ex.Message);
            }
            finally
            {
                UpdateStatus();
            }
        }

        private async void ExecuteTriggerScan()
        {
            try
            {
                AppendReceiveLog("触发扫码中...");
                var code = await Task.Run(() => DLManager.Instance().TriggerScan());

                if (string.IsNullOrWhiteSpace(code))
                {
                    AppendReceiveLog("扫码无结果/失败。");
                    return;
                }

                PlateCode = code.Trim();
                AppendReceiveLog("扫码结果: " + PlateCode);
            }
            catch (Exception ex)
            {
                AppendReceiveLog("触发扫码异常: " + ex.Message);
            }
        }

        private async void ExecuteSend()
        {
            try
            {
                if (!_mesManager.IsConnected)
                {
                    if (!_mesManager.ConfigureFromSystemParameters(connect: false) || !_mesManager.Connect())
                    {
                        AppendReceiveLog("发送失败：MES未连接。");
                        UpdateStatus();
                        return;
                    }
                }

                if (!_mesManager.IsRingLineInitialized)
                {
                    if (!_mesManager.InitializeRingLine(RingLineDeviceId, RingLineDeviceCode))
                    {
                        AppendReceiveLog("发送失败：环线服务未初始化。");
                        UpdateStatus();
                        return;
                    }
                }

                var action = SelectedAction?.Action ?? RingLineAction.FeedingQianLiaocang;

                string plateCode = (PlateCode ?? string.Empty).Trim();
                string billNoWip = (BillNoWip ?? string.Empty).Trim();
                string feedingCode = (FeedingLiaokuangCode ?? string.Empty).Trim();

                ushort msgId;
                switch (action)
                {
                    case RingLineAction.FeedingQianLiaocang:
                        RequireNotEmpty(plateCode, "PlateCode");
                        msgId = await _mesManager.PublishFeedingQianLiaocangAsync(plateCode, billNoWip);
                        AppendSendLog($"前料仓上料 已发送, MessageId={msgId}, PlateCode={plateCode}, BillNoWip={billNoWip}");
                        break;

                    case RingLineAction.FeedingQianLiaocangSuccess:
                        RequireNotEmpty(plateCode, "PlateCode");
                        RequireNotEmpty(feedingCode, "FeedingLiaokuangCode");
                        msgId = await _mesManager.PublishFeedingQianLiaocangSuccessAsync(plateCode, feedingCode);
                        AppendSendLog($"前料仓上料成功 已发送, MessageId={msgId}, PlateCode={plateCode}, FeedingLiaokuangCode={feedingCode}");
                        break;

                    case RingLineAction.UnloadingQianLiaocang:
                        RequireNotEmpty(plateCode, "PlateCode");
                        RequireNotEmpty(feedingCode, "FeedingLiaokuangCode");
                        msgId = await _mesManager.PublishUnloadingQianLiaocangAsync(plateCode, feedingCode);
                        AppendSendLog($"前料仓卸料 已发送, MessageId={msgId}, PlateCode={plateCode}, FeedingLiaokuangCode={feedingCode}");
                        break;

                    case RingLineAction.FeedingZhongLiaocang:
                        RequireNotEmpty(plateCode, "PlateCode");
                        RequireNotEmpty(feedingCode, "FeedingLiaokuangCode");
                        msgId = await _mesManager.PublishFeedingZhongLiaocangAsync(plateCode, feedingCode);
                        AppendSendLog($"中料仓上料 已发送, MessageId={msgId}, PlateCode={plateCode}, FeedingLiaokuangCode={feedingCode}");
                        break;

                    case RingLineAction.UnloadingZhongLiaocang:
                        RequireNotEmpty(plateCode, "PlateCode");
                        RequireNotEmpty(feedingCode, "FeedingLiaokuangCode");
                        msgId = await _mesManager.PublishUnloadingZhongLiaocangAsync(plateCode, feedingCode);
                        AppendSendLog($"中料仓卸料 已发送, MessageId={msgId}, PlateCode={plateCode}, FeedingLiaokuangCode={feedingCode}");
                        break;

                    case RingLineAction.FeedingQingxihongganji:
                        RequireNotEmpty(plateCode, "PlateCode");
                        msgId = await _mesManager.PublishFeedingQingxihongganjiAsync(plateCode);
                        AppendSendLog($"清洗烘干机上料 已发送, MessageId={msgId}, PlateCode={plateCode}");
                        break;

                    case RingLineAction.FeedingHouLiaocang:
                        RequireNotEmpty(plateCode, "PlateCode");
                        RequireNotEmpty(feedingCode, "FeedingLiaokuangCode");
                        msgId = await _mesManager.PublishFeedingHouLiaocangAsync(plateCode, feedingCode);
                        AppendSendLog($"后料仓上料 已发送, MessageId={msgId}, PlateCode={plateCode}, FeedingLiaokuangCode={feedingCode}");
                        break;

                    case RingLineAction.UnloadingHouLiaocang:
                        RequireNotEmpty(plateCode, "PlateCode");
                        RequireNotEmpty(feedingCode, "FeedingLiaokuangCode");
                        msgId = await _mesManager.PublishUnloadingHouLiaocangAsync(plateCode, feedingCode);
                        AppendSendLog($"后料仓卸料 已发送, MessageId={msgId}, PlateCode={plateCode}, FeedingLiaokuangCode={feedingCode}");
                        break;

                    default:
                        AppendReceiveLog("未知操作类型，发送已取消。");
                        return;
                }

                UpdateStatus();
            }
            catch (Exception ex)
            {
                AppendReceiveLog("发送异常: " + ex.Message);
            }
        }

        private void ExecuteClearLogs()
        {
            SendLogText = string.Empty;
            ReceiveLogText = string.Empty;
            AppendReceiveLog("日志已清空。");
        }

        private void UpdateStatus()
        {
            try
            {
                IsConnected = _mesManager.IsConnected;
                IsRingLineInitialized = _mesManager.IsRingLineInitialized;
                BrokerEndpointText = _mesManager.BrokerEndpointText;
            }
            catch
            {
            }
        }

        private void AppendSendLog(string message)
        {
            AppendLog(message, isSend: true);
        }

        private void AppendReceiveLog(string message)
        {
            AppendLog(message, isSend: false);
        }

        private void AppendLog(string message, bool isSend)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => AppendLog(message, isSend)));
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (isSend)
            {
                SendLogText = AppendLine(SendLogText, line);
            }
            else
            {
                ReceiveLogText = AppendLine(ReceiveLogText, line);
            }
        }

        private static string AppendLine(string text, string line)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return line;
            }

            return text + Environment.NewLine + line;
        }

        private static void RequireNotEmpty(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(field + " 不能为空");
            }
        }

        public void Dispose()
        {
            try
            {
                _feedingQianLiaocangResponseSubscription?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _feedingQianLiaocangResponseSubscription = null;
            }
        }
    }
}

