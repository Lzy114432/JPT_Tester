using Ewan.Core.Security;
using Ewan.Core.Logger;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MarkingMachineFeeder.Viewmodel
{
    public class LoginWindowViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private readonly SecurityManager _securityManager;

        private string _title = "系统登录";
        private string _applicationName = "MarkingMachineFeeder";
        private string _loginHeaderText = "请输入您的登录信息";
        private string _usernameLabel = "用户名";
        private string _passwordLabel = "密码";
        private string _loginButtonText = "登录";
        private string _userInfoText = "选择用户并输入对应密码登录，默认密码均为：123456";
        
        private string _username = "";
        private string _selectedUser = "";
        private List<string> _availableUsers;
        private string _errorMessage = "";
        private bool _hasError = false;

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public string ApplicationName
        {
            get { return _applicationName; }
            set { SetProperty(ref _applicationName, value); }
        }

        public string LoginHeaderText
        {
            get { return _loginHeaderText; }
            set { SetProperty(ref _loginHeaderText, value); }
        }

        public string UsernameLabel
        {
            get { return _usernameLabel; }
            set { SetProperty(ref _usernameLabel, value); }
        }

        public string PasswordLabel
        {
            get { return _passwordLabel; }
            set { SetProperty(ref _passwordLabel, value); }
        }

        public string LoginButtonText
        {
            get { return _loginButtonText; }
            set { SetProperty(ref _loginButtonText, value); }
        }

        public string UserInfoText
        {
            get { return _userInfoText; }
            set { SetProperty(ref _userInfoText, value); }
        }

        public string Username
        {
            get { return _selectedUser; }
            set 
            { 
                SetProperty(ref _selectedUser, value);
                ClearError();
                LoginCommand.RaiseCanExecuteChanged(); // 触发命令可执行状态更新
            }
        }

        public List<string> AvailableUsers
        {
            get { return _availableUsers; }
            set { SetProperty(ref _availableUsers, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        public bool HasError
        {
            get { return _hasError; }
            set { SetProperty(ref _hasError, value); }
        }

        public DelegateCommand<PasswordBox> LoginCommand { get; }

        /// <summary>
        /// 登录成功事件
        /// </summary>
        public event EventHandler LoginSuccessful;

        public LoginWindowViewModel()
        {
            _securityManager = SecurityManager.Instance();
            LoginCommand = new DelegateCommand<PasswordBox>(ExecuteLogin, CanExecuteLogin);
            
            // 初始化可用用户列表
            InitializeAvailableUsers();
            
            UpdateUITexts();
        }

        private void ExecuteLogin(PasswordBox passwordBox)
        {
            ClearError();

            if (string.IsNullOrWhiteSpace(Username))
            {
                SetError("请输入用户名");
                return;
            }

            if (passwordBox == null || string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                SetError("请输入密码");
                return;
            }

            try
            {
                if (_securityManager.Authenticate(Username, passwordBox.Password))
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.LoginSuccessful, Username);
                    LoginSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    SetError("用户名或密码错误");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.LoginError, ex.Message);
                SetError($"登录时发生错误: {ex.Message}");
            }
        }

        private bool CanExecuteLogin(PasswordBox passwordBox)
        {
            return !string.IsNullOrWhiteSpace(_selectedUser);
        }

        private void InitializeAvailableUsers()
        {
            // 预定义的用户列表
            AvailableUsers = new List<string>
            {
                "admin",
                "engineer", 
                "operator"
            };
            
            // 设置默认选中用户
            if (AvailableUsers.Count > 0)
            {
                Username = AvailableUsers[2]; // 默认选择 operator
            }
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            HasError = !string.IsNullOrEmpty(message);
        }

        private void ClearError()
        {
            ErrorMessage = "";
            HasError = false;
        }

        private void UpdateUITexts()
        {
            // 这里可以根据当前语言更新UI文本
            // 暂时使用中文硬编码，后续可以改为资源文件
        }
    }
}