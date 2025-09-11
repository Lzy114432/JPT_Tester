namespace Ewan.Model.Production
{
    /// <summary>
    /// 自动生产流程IO定义
    /// </summary>
    public static class AutoProductionIO
    {
        #region 输入信号 - 传感器检测

        /// <summary>
        /// 上相机检测到料片信号
        /// </summary>
        public const string UpperCameraDetected = "UpperCamera_PartDetected";

        /// <summary>
        /// 机械手抓取完成信号
        /// </summary>
        public const string RobotGrabCompleted = "Robot_GrabCompleted";

        /// <summary>
        /// 下相机精定位完成信号
        /// </summary>
        public const string LowerCameraPositioned = "LowerCamera_PositionCompleted";

        /// <summary>
        /// 机械手移动到扫码区完成信号
        /// </summary>
        public const string RobotMoveToScanCompleted = "Robot_MoveToScanCompleted";

        /// <summary>
        /// 扫码许可信号
        /// </summary>
        public const string ScanPermissionGranted = "Scan_PermissionGranted";

        /// <summary>
        /// 分料完成信号
        /// </summary>
        public const string SortingCompleted = "Sorting_Completed";

        /// <summary>
        /// 气缸整料完成信号
        /// </summary>
        public const string CylinderOrganizeCompleted = "Cylinder_OrganizeCompleted";

        /// <summary>
        /// 小车取料请求信号
        /// </summary>
        public const string CartPickupRequest = "Cart_PickupRequest";

        /// <summary>
        /// 小车取料完成信号
        /// </summary>
        public const string CartPickupCompleted = "Cart_PickupCompleted";

        #endregion

        #region 输出信号 - 控制指令

        /// <summary>
        /// 发送机械手抓取信号
        /// </summary>
        public const string SendRobotGrabSignal = "Send_RobotGrabSignal";

        /// <summary>
        /// 发送下相机拍照信号
        /// </summary>
        public const string SendLowerCameraSignal = "Send_LowerCameraSignal";

        /// <summary>
        /// 发送机械手定位信号
        /// </summary>
        public const string SendRobotPositionSignal = "Send_RobotPositionSignal";

        /// <summary>
        /// 发送扫码到位信号
        /// </summary>
        public const string SendScanReadySignal = "Send_ScanReadySignal";

        /// <summary>
        /// 发送扫码完成信号
        /// </summary>
        public const string SendScanCompleteSignal = "Send_ScanCompleteSignal";

        /// <summary>
        /// 发送分料指令信号
        /// </summary>
        public const string SendSortingSignal = "Send_SortingSignal";

        /// <summary>
        /// 发送气缸整料信号
        /// </summary>
        public const string SendCylinderOrganizeSignal = "Send_CylinderOrganizeSignal";

        /// <summary>
        /// 发送小车取料许可信号
        /// </summary>
        public const string SendCartPickupPermission = "Send_CartPickupPermission";

        #endregion

        #region 流程控制信号

        /// <summary>
        /// 系统启用信号
        /// </summary>
        public const string SystemEnable = "System_Enable";

        /// <summary>
        /// 主流程启动信号
        /// </summary>
        public const string MainFlowStart = "MainFlow_Start";

        /// <summary>
        /// 主流程停止信号
        /// </summary>
        public const string MainFlowStop = "MainFlow_Stop";

        /// <summary>
        /// 主流程暂停信号
        /// </summary>
        public const string MainFlowPause = "MainFlow_Pause";

        /// <summary>
        /// 主流程复位信号
        /// </summary>
        public const string MainFlowReset = "MainFlow_Reset";

        /// <summary>
        /// 小车流程启动信号
        /// </summary>
        public const string CartFlowStart = "CartFlow_Start";

        /// <summary>
        /// 小车流程停止信号
        /// </summary>
        public const string CartFlowStop = "CartFlow_Stop";

        /// <summary>
        /// 急停信号
        /// </summary>
        public const string EmergencyStop = "Emergency_Stop";

        /// <summary>
        /// 急停复位信号
        /// </summary>
        public const string EmergencyReset = "Emergency_Reset";

        #endregion

        #region 急停控制信号

        /// <summary>
        /// 机械手急停输出
        /// </summary>
        public const string RobotEmergencyStop = "Robot_EmergencyStop";

        /// <summary>
        /// 机械手电源关闭
        /// </summary>
        public const string RobotPowerOff = "Robot_PowerOff";

        /// <summary>
        /// 皮带线停止信号
        /// </summary>
        public const string ConveyorStop = "Conveyor_Stop";

        /// <summary>
        /// 皮带线电源关闭
        /// </summary>
        public const string ConveyorPowerOff = "Conveyor_PowerOff";

        /// <summary>
        /// 气缸急停信号
        /// </summary>
        public const string CylinderEmergencyStop = "Cylinder_EmergencyStop";

        /// <summary>
        /// 扫码设备停止信号
        /// </summary>
        public const string ScannerStop = "Scanner_Stop";

        /// <summary>
        /// 所有设备总急停信号
        /// </summary>
        public const string AllDevicesEmergencyStop = "AllDevices_EmergencyStop";

        #endregion

        #region 状态指示信号

        /// <summary>
        /// 主流程运行状态指示
        /// </summary>
        public const string MainFlowRunning = "Status_MainFlowRunning";

        /// <summary>
        /// 小车流程运行状态指示
        /// </summary>
        public const string CartFlowRunning = "Status_CartFlowRunning";

        /// <summary>
        /// 系统就绪状态指示
        /// </summary>
        public const string SystemReady = "Status_SystemReady";

        /// <summary>
        /// 系统故障状态指示
        /// </summary>
        public const string SystemFault = "Status_SystemFault";

        /// <summary>
        /// 机械手忙碌状态指示
        /// </summary>
        public const string RobotBusy = "Status_RobotBusy";

        #endregion

        #region 料仓相关信号

        /// <summary>
        /// 料仓1有料信号
        /// </summary>
        public const string Bin1HasPart = "Bin1_HasPart";

        /// <summary>
        /// 料仓2有料信号
        /// </summary>
        public const string Bin2HasPart = "Bin2_HasPart";

        /// <summary>
        /// 料仓3有料信号
        /// </summary>
        public const string Bin3HasPart = "Bin3_HasPart";

        /// <summary>
        /// 料仓4有料信号
        /// </summary>
        public const string Bin4HasPart = "Bin4_HasPart";

        /// <summary>
        /// 料仓1满料信号
        /// </summary>
        public const string Bin1Full = "Bin1_Full";

        /// <summary>
        /// 料仓2满料信号
        /// </summary>
        public const string Bin2Full = "Bin2_Full";

        /// <summary>
        /// 料仓3满料信号
        /// </summary>
        public const string Bin3Full = "Bin3_Full";

        /// <summary>
        /// 料仓4满料信号
        /// </summary>
        public const string Bin4Full = "Bin4_Full";

        #endregion

        #region 小车相关信号

        /// <summary>
        /// 小车位置1到位信号
        /// </summary>
        public const string CartPosition1Ready = "Cart_Position1Ready";

        /// <summary>
        /// 小车位置2到位信号
        /// </summary>
        public const string CartPosition2Ready = "Cart_Position2Ready";

        /// <summary>
        /// 小车位置3到位信号
        /// </summary>
        public const string CartPosition3Ready = "Cart_Position3Ready";

        /// <summary>
        /// 小车位置4到位信号
        /// </summary>
        public const string CartPosition4Ready = "Cart_Position4Ready";

        /// <summary>
        /// 小车请求料号1信号
        /// </summary>
        public const string CartRequestPart1 = "Cart_RequestPart1";

        /// <summary>
        /// 小车请求料号2信号
        /// </summary>
        public const string CartRequestPart2 = "Cart_RequestPart2";

        /// <summary>
        /// 小车请求料号3信号
        /// </summary>
        public const string CartRequestPart3 = "Cart_RequestPart3";

        /// <summary>
        /// 小车请求料号4信号
        /// </summary>
        public const string CartRequestPart4 = "Cart_RequestPart4";

        #endregion

        #region 料仓升降相关信号

        /// <summary>
        /// 料仓1升降感应器 - 检测是否有料
        /// </summary>
        public const string Bin1ElevatorSensor = "Bin1_ElevatorSensor";

        /// <summary>
        /// 料仓2升降感应器 - 检测是否有料
        /// </summary>
        public const string Bin2ElevatorSensor = "Bin2_ElevatorSensor";

        /// <summary>
        /// 料仓3升降感应器 - 检测是否有料
        /// </summary>
        public const string Bin3ElevatorSensor = "Bin3_ElevatorSensor";

        /// <summary>
        /// 料仓1升降轴到位信号
        /// </summary>
        public const string Bin1ElevatorInPosition = "Bin1_ElevatorInPosition";

        /// <summary>
        /// 料仓2升降轴到位信号
        /// </summary>
        public const string Bin2ElevatorInPosition = "Bin2_ElevatorInPosition";

        /// <summary>
        /// 料仓3升降轴到位信号
        /// </summary>
        public const string Bin3ElevatorInPosition = "Bin3_ElevatorInPosition";

        /// <summary>
        /// 料仓1升降轴故障信号
        /// </summary>
        public const string Bin1ElevatorFault = "Bin1_ElevatorFault";

        /// <summary>
        /// 料仓2升降轴故障信号
        /// </summary>
        public const string Bin2ElevatorFault = "Bin2_ElevatorFault";

        /// <summary>
        /// 料仓3升降轴故障信号
        /// </summary>
        public const string Bin3ElevatorFault = "Bin3_ElevatorFault";

        /// <summary>
        /// 料仓升降系统启用信号
        /// </summary>
        public const string BinElevatorSystemEnable = "BinElevator_SystemEnable";

        #endregion
    }
}