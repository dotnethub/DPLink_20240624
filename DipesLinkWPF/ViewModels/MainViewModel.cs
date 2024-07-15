﻿using DipesLink.Extensions;
using DipesLink.Languages;
using DipesLink.Models;
using DipesLink.Views.Extension;
using DipesLink.Views.Models;
using DipesLink.Views.UserControls.MainUc;
using IPCSharedMemory;
using Microsoft.VisualBasic;
using SharedProgram.Models;
using SharedProgram.Shared;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using static DipesLink.Views.Enums.ViewEnums;
using static IPCSharedMemory.Datatypes.Enums;
using static SharedProgram.DataTypes.CommonDataType;
using Application = System.Windows.Application;

namespace DipesLink.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {

       
        public event EventHandler? OnSomethingHappened;
        protected virtual void RaiseSomethingHappened(EventArgs e)
        {
            OnSomethingHappened?.Invoke(this, e);
        }
        #region SingletonInit

       
        public static MainViewModel GetIntance()
        {
            _instance ??= new MainViewModel();
            return _instance;
        }
        #endregion

        public MainViewModel()
        {

#if DEBUG
            IsDebugMode = true;
#else
        IsDebugMode = false;
#endif

            EventRegister();
            ViewModelSharedFunctions.LoadSetting();
            _NumberOfStation = ViewModelSharedValues.Settings.NumberOfStation;
            StationSelectedIndex = _NumberOfStation > 0 ? _NumberOfStation - 1 : StationSelectedIndex;
            InitDir();
            InitJobConnectionSettings();
            InitStations(_NumberOfStation);

           _updateTimer ??= InitializeDispatcherTimer();
        }

        private void EventRegister()
        {
            ViewModelSharedEvents.OnEnableUIChange += EnableUIChange;
        }

        private void EnableUIChange(object? sender, bool isEnable)
        {
            if (sender is null) return;
            UIEnableControlByLoadingDb((int)sender, isEnable);
        }

        private void InitInstanceIPC(int index)
        {
            listIPCUIToDevice1MB.Add(new(index, "UIToDeviceSharedMemory_DT", SharedValues.SIZE_1MB));
        }

        private void InitDir()
        {

            //Create Account Database Directory
            if (!Directory.Exists(SharedPaths.PathAccountsDb))
            {
                Directory.CreateDirectory(SharedPaths.PathAccountsDb);
            }

            for (int i = 0; i < _NumberOfStation; i++)
            {
                SharedPaths.InitCommonPathByIndex(i);
            }
        }

        private void InitStations(int numberOfStation)
        {
            for (int i = 0; i < numberOfStation; i++)
            {
                InitInstanceIPC(i);
                ListenDeviceTransferDataAsync(i);
                InitTabStationUI(i);
                GetCurrentJobDetail(i);
            }
        }

        public void ListenDeviceTransferDataAsync(int stationIndex)
        {
            CreateMultiObjects(stationIndex);

            Task.Run(() => ListenDatabase(stationIndex));

            Task.Run(() => ListenProcess(stationIndex));

            Task.Run(() => ListenDetectModel(stationIndex));

            Task.Run(() => GetOperationStatusAsync(stationIndex));

            Task.Run(() => GetStatisticsAsync(stationIndex));

            Task.Run(() => DevicesStatusChangeAsync(stationIndex));

            Task.Run(() => GetCurrentPrintedCodeAsync(stationIndex));

            Task.Run(() => GetCheckedCodeAsync(stationIndex));

            Task.Run(() => GetCameraDataAsync(stationIndex));

            Task.Run(() => GetCheckedStatistics(stationIndex));
        }

        private void CreateMultiObjects(int i)
        {
            int deviceTransferIDProc = ViewModelSharedFunctions.InitDeviceTransfer(i);
            string t = ViewModelSharedValues.Settings.Language == "vi-VN" ? $"Trạm {i + 1}" : $"Station {i + 1}";
            JobList.Add(new JobOverview() { DeviceTransferID = deviceTransferIDProc, Index = i, JobTitleName = t }); // Job List Creation
            JobDeviceStatusList.Add(new JobDeviceStatus() { Index = i, Name = $"Devices{i + 1}" }); // Device Status List Creation
            PrinterStateList.Add(new PrinterState() { Name = $"{i + 1}:", State = "" }); // Printer State List Creation

        }

        public void InitTabStationUI(int stationIndex)
        {
            var userControl = new JobDetails() { DataContext = JobList[stationIndex] };

            string t = ViewModelSharedValues.Settings.Language == "vi-VN" ? $"Trạm {stationIndex + 1}" : $"Station {stationIndex + 1}";

            TabStation.Add(new TabItemModel() { Header = $"{t}", Content = userControl });
        }

        private bool CheckJobExisting(int index, out JobModel? job)
        {
            JobModel? jobModel;
            string? selectedJobName = SharedFunctions.GetSelectedJobNameList(index).FirstOrDefault();
            jobModel = SharedFunctions.GetJob(selectedJobName, index);
            if (jobModel == null)
            {
                job = jobModel;
                return false;
            }
            job = jobModel;
            return true;
        }

        internal void GetCurrentJobDetail(int index)
        {
            JobModel? jobModel;
            if (!CheckJobExisting(index, out jobModel))
            {
                jobModel = new();
            }
            JobList[index].Name = jobModel.Name;
            JobList[index].PrinterSeries = jobModel.PrinterSeries;
            JobList[index].JobType = jobModel.JobType;
            JobList[index].CompareType = jobModel.CompareType;
            JobList[index].StaticText = jobModel.StaticText;
            JobList[index].DatabasePath = jobModel.DatabasePath;
            JobList[index].DataCompareFormat = jobModel.DataCompareFormat;
            JobList[index].TotalRecDb = jobModel.TotalRecDb;
            JobList[index].PrinterTemplate = jobModel.PrinterTemplate;
            JobList[index].CameraSeries = jobModel.CameraSeries;
            JobList[index].ImageExportPath = jobModel.ImageExportPath;
            JobList[index].PODFormat = jobModel.PODFormat;
            // thinh them status
            //JobList[index].OperationStatus = jobModel.OperationStatus;

            // Events for Button Start/Stop/Trigger
            JobList[index].StartButtonCommand -= StartButtonCommandEventHandler;
            JobList[index].PauseButtonCommand -= PauseButtonCommandEventHandler;
            JobList[index].StopButtonCommand -= StopButtonCommandEventHandler;
            JobList[index].TriggerButtonCommand -= TriggerButtonCommandEventHandler;
            JobList[index].OnPercentageChange -= PercentageChangeHandler;
            JobList[index].OnReprint -= ReprintHandler;
            JobList[index].OnLoadDb -= LoadDbEventHandler;
            JobList[index].OnExportButtonCommand -= ExportButtonCommandHandler;

            JobList[index].StartButtonCommand += StartButtonCommandEventHandler;
            JobList[index].PauseButtonCommand += PauseButtonCommandEventHandler;
            JobList[index].StopButtonCommand += StopButtonCommandEventHandler;
            JobList[index].TriggerButtonCommand += TriggerButtonCommandEventHandler;
            JobList[index].OnPercentageChange += PercentageChangeHandler;
            JobList[index].OnReprint += ReprintHandler;
            JobList[index].OnLoadDb += LoadDbEventHandler;
            JobList[index].OnExportButtonCommand += ExportButtonCommandHandler;
            JobList[index].SimulateButtonCommand += SimulateButtonCommandHandler;

        }

      

        private void LoadDbEventHandler(object? sender, EventArgs e)
        {
            if (sender is int index)
            {
                ActionButtonProcess(index, ActionButtonType.LoadDB);
                //  Debug.WriteLine("Load DB for Job " + index);
            }
        }

        private void ReprintHandler(object? sender, EventArgs e)
        {
            if (sender is int stationIndex)
            {
                try
                {
                    byte[] indexBytes = SharedFunctions.StringToFixedLengthByteArray(stationIndex.ToString(), 1);
                    byte[] actionTypeBytes = SharedFunctions.StringToFixedLengthByteArray(((int)ActionButtonType.Reprint).ToString(), 1);
                    byte[] combineBytes = SharedFunctions.CombineArrays(indexBytes, actionTypeBytes);
                    MemoryTransfer.SendActionButtonToDevice(listIPCUIToDevice1MB[stationIndex], stationIndex, combineBytes);
                }
                catch (Exception) { }
            }
        }

        private void PercentageChangeHandler(object? sender, EventArgs e)
        {
            if (sender is int stationIndex)
            {
                UpdatePercentForCircleChart(stationIndex);
            }
        }


        #region GET DATABASE


        private async void ListenDatabase(int stationIndex)
        {
            using IPCSharedHelper ipc = new(stationIndex, "DeviceToUISharedMemory_DB", SharedValues.SIZE_200MB, isReceiver: true);
            while (true)
            {
                bool isCompleteDequeue = ipc.MessageQueue.TryDequeue(out byte[]? result);
                if (result != null && isCompleteDequeue)
                {
                    switch (result[0])
                    {
                        case (byte)SharedMemoryCommandType.DeviceCommand:
                            switch (result[2])
                            {
                                case (byte)SharedMemoryType.DatabaseList:
                                    //Debug.WriteLine($"DB Size: {result.Length}");
                                    GetDatabaseList(stationIndex, result);
                                    break;
                                case (byte)SharedMemoryType.CheckedList:
                                    GetCheckedList(stationIndex, result);
                                    break;
                            }
                            break;
                    }
                }
                await Task.Delay(100);
            }
        }

        private void GetDatabaseList(int stationIndex, byte[] result)
        {
            try
            {
                if (JobList[stationIndex].IsDBExist)
                {
                    // Debug.WriteLine("Dont Reload Database for job: " + +stationIndex);
                    JobList[stationIndex].IsShowLoadingDB = Visibility.Collapsed;
                    //  JobList[stationIndex].IsStartButtonEnable = true;
                    ViewModelSharedEvents.OnEnableUIChangeHandler(stationIndex, false);
                    //      JobList[stationIndex].EnableUI = true;
                    return;
                }
                // Debug.WriteLine("Reload Database for job: " + stationIndex);
                //JobList[stationIndex].IsShowLoadingDB = Visibility.Visible;
                // JobList[stationIndex].IsStartButtonEnable = false;
                ViewModelSharedEvents.OnEnableUIChangeHandler(stationIndex, false);
                //  JobList[stationIndex].EnableUI = false;
                //ViewModelSharedEvents.OnLoadingTableStatusChangeHandler();
                byte[] listBytes = result.Skip(3).ToArray();
                List<string[]>? listDatabase = DataConverter.FromByteArray<List<string[]>>(listBytes);
                List<(List<string[]>, int)> dbInfo = new(1);
                if (listDatabase != null)
                {
                    int firstWaiting = listDatabase.IndexOf(listDatabase.Find(x => x[x.Length - 1] == "Waiting"));
                    int totalCode = listDatabase.Count;
                    int currentPage = (listDatabase.Count > _MaxDatabaseLine) ? (firstWaiting > 0 ? firstWaiting / _MaxDatabaseLine : (firstWaiting == 0 ? 0 : totalCode / _MaxDatabaseLine - 1)) : 0;

                    JobList[stationIndex].CurrentPage = currentPage;
                    JobList[stationIndex].CurrentIndexDB = firstWaiting - 1;
                    dbInfo.Add((listDatabase, currentPage));
                    JobList[stationIndex].RaiseLoadCompleteDatabase(dbInfo);
                    JobList[stationIndex].IsDBExist = true;
                }
                if (listDatabase.Count == 0)
                {
                    JobList[stationIndex].IsShowLoadingDB = Visibility.Collapsed;
                    ViewModelSharedEvents.OnEnableUIChangeHandler(stationIndex, true);
                    CusAlert.Show($"Station {stationIndex}:No data found", ImageStyleMessageBox.Info, true);
                }

            }
            catch (Exception)
            {
#if DEBUG
                Console.WriteLine("Get Db Error !");
#endif
            }
        }

        private void GetCheckedList(int stationIndex, byte[] result)
        {
            try
            {
                byte[] listBytes = result.Skip(3).ToArray();
                List<string[]>? listChecked = DataConverter.FromByteArray<List<string[]>>(listBytes);
                if (listChecked != null)
                {
                    JobList[stationIndex].RaiseLoadCompleteCheckedDatabase(listChecked);
                    JobList[stationIndex].IsShowLoadingChecked = Visibility.Collapsed;
                }
            }
            catch (Exception)
            {
#if DEBUG
                Console.WriteLine("GetCheckedList Error !");
#endif
            }
        }

        #endregion END GET DATABASE

        #region  GET PRINTING PARAMS AND STATUS


        private async void ListenProcess(int stationIndex)
        {
            using IPCSharedHelper ipc = new(stationIndex, "DeviceToUISharedMemory_DT", SharedValues.SIZE_1MB, isReceiver: true);
            while (true)
            {
                while (ipc.MessageQueue.TryDequeue(out byte[]? result))
                {
                    //SharedFunctions.PrintDebugMessage($"Queue print Data {stationIndex}: " + ipc.MessageQueue.Count().ToString());
                     await Task.Run(() => ProcessItem(result, stationIndex));
                }
                await Task.Delay(1);
            }
        }
        private DispatcherTimer _updateTimer;
        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(500);
        private DispatcherTimer InitializeDispatcherTimer()
        {
            var dispatcherTimer = new DispatcherTimer
            {
                Interval = _updateInterval
            };
            dispatcherTimer.Tick += (sender, args) => PeriodUIUpdate();
            dispatcherTimer.Start();
            return dispatcherTimer;
        }

        private void PeriodUIUpdate()
        {
            foreach (var job in JobList)
            {
                job.CycleTimePOD = job.CycleTimePOD_Store;
            }
        }

        private void ProcessItem(byte[] result, int stationIndex)
        {
            switch (result[0])
            {
                case (byte)SharedMemoryCommandType.DeviceCommand:

                    switch (result[2]) // Shared Memory Type
                    {
                        // Camera Status
                        case (byte)SharedMemoryType.CameraInfo:
                            JobList[stationIndex].CameraInfo = DataConverter.FromByteArray<CameraInfos>(result.Skip(3).ToArray());
                            UpdateCameraInfo(stationIndex);
                            // Debug.WriteLine($"Tram {stationIndex} : {JobList[stationIndex]?.CameraInfo?.Info?.Name}");
                            break;

                        // Printer Status
                        case (byte)SharedMemoryType.PrinterStatus:
                            JobList[stationIndex].PrinterStsBytes = result[3];
                            break;

                        // Controller Status
                        case (byte)SharedMemoryType.ControllerStatus:
                            JobList[stationIndex].ControllerStsBytes = result[3];
                            break;

                        // Printer Template
                        case (byte)SharedMemoryType.PrinterTemplate:
                            GetPrinterTemplateName(result);
                            break;

                        // Statistics (Sent/Received/Printed number)
                        case (byte)SharedMemoryType.StatisticsCounterSent:
                            JobList[stationIndex].QueueSentNumberBytes.Enqueue(result.Skip(3).ToArray());
                            break;
                        case (byte)SharedMemoryType.StatisticsCounterReceived:
                            JobList[stationIndex].QueueReceivedNumberBytes.Enqueue(result.Skip(3).ToArray());
                            break;
                        case (byte)SharedMemoryType.StatisticsCounterPrinted:
                            JobList[stationIndex].QueuePrintedNumberBytes.Enqueue(result.Skip(3).ToArray());
                            break;

                        //Current Index and Current Page in Database
                        case (byte)SharedMemoryType.CurrentPosDb:
                            GetCurrentPosDb(stationIndex, result);
                            break;

                        // Printed code
                        case (byte)SharedMemoryType.PrintedCodeRaw:
                            JobList[stationIndex].QueueCurrentPrintedCode.Enqueue(result.Skip(3).ToArray());
                            break;

                        case (byte)SharedMemoryType.JobMessageStatus:
                            byte[] notifyType = result.Skip(3).ToArray();
                            try
                            {
                                NotifyType ntp = DataConverter.FromByteArray<NotifyType>(notifyType);
                                if (ntp != NotifyType.Unk) JobMessageStatusProcess(stationIndex, ntp);
                            }
                            catch (Exception) { }
                            break;

                        case (byte)SharedMemoryType.JobOperationStatus:
                            byte[] resOper = result.Skip(3).ToArray();
                            try
                            {
                                var stsBtn = DataConverter.FromByteArray<OperationStatus>(resOper);
                                JobList[stationIndex].OperationStatus = stsBtn;
                            }
                            catch (Exception) { }
                            break;
                        case (byte)SharedMemoryType.ControllerResponseMess:
                            GetControllerMessageResponseAsync(stationIndex, result);
                            break;
                        case (byte)SharedMemoryType.LoadingStatus:
                            ShowLoadingImage(stationIndex);
                            break;
                        case (byte)SharedMemoryType.RestartStatus:
                            //RestartDetect(stationIndex);
                            break;
                        case (byte)SharedMemoryType.CycleTimePOD:
                            JobList[stationIndex].CycleTimePOD_Store = DataConverter.FromByteArray<double>(result.Skip(3).ToArray()).ToString() + " ms";
                            break;
                    }
                    break;

                case 1:
                    break;

                default:
                    break;
            }
        }



        private void ShowLoadingImage(int stationIndex)
        {
            JobList[stationIndex].IsShowLoadingDB = Visibility.Visible;
            JobList[stationIndex].IsShowLoadingChecked = Visibility.Visible;
            // JobList[stationIndex].IsStartButtonEnable = false;
            ViewModelSharedEvents.OnEnableUIChangeHandler(stationIndex, false);
            //  JobList[stationIndex].EnableUI = false;
        }

        private void GetControllerMessageResponseAsync(int stationIndex, byte[] result)
        {
            try
            {
                var res = result.Skip(3).ToArray();
                var mess = DataConverter.FromByteArray<string>(res);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectParamsList[stationIndex].ResponseMessList.Insert(0, mess);
                    if (ConnectParamsList[stationIndex].ResponseMessList.Count > 10)
                    {
                        ConnectParamsList[stationIndex].ResponseMessList.RemoveAt(9);
                    }
                });
            }
            catch (Exception)
            {
#if DEBUG
                Debug.WriteLine("Error: GetControllerMessageResponse");
#endif
            }
        }

        private async Task DevicesStatusChangeAsync(int stationIndex)
        {
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                while (true)
                {
                    try
                    {
                        byte printerStsBytes = JobList[stationIndex].PrinterStsBytes;
                        byte controllerStsBytes = JobList[stationIndex].ControllerStsBytes;

                        // CAMERA CONNECTION
                        if (JobList[stationIndex].CameraInfo?.ConnectionStatus == true) // Camera Status Change
                        {
                            _detectCamDisconnected = false;
                            if (JobDeviceStatusList[stationIndex].CameraStatusColor.Color != Colors.Green)
                            {
                                JobDeviceStatusList[stationIndex].CameraStatusColor = new SolidColorBrush(Colors.Green); // Camera online
                                JobDeviceStatusList[stationIndex].IsCamConnected = true;
                            }
                        }
                        else
                        {
                            JobDeviceStatusList[stationIndex].CameraStatusColor = new SolidColorBrush(Colors.Red); // Camera offline
                            JobDeviceStatusList[stationIndex].IsCamConnected = false;
                            // If Running but Camera Disconnected => Show Alert
                            if (JobList[stationIndex].OperationStatus != OperationStatus.Stopped && !_detectCamDisconnected)
                            {
                                CusAlert.Show($"Station {stationIndex}:Camera's Disconnected !", ImageStyleMessageBox.Error);
                                _detectCamDisconnected = true;
                            }
                        }

                        // PRINTER CONNECTION
                        if (printerStsBytes == (byte)PrinterStatus.Connected) // Printer Status Change
                        {
                            _detectPrinterDisconnected = false;
                            if (JobDeviceStatusList[stationIndex].PrinterStatusColor.Color != Colors.Green)
                            {
                                JobDeviceStatusList[stationIndex].PrinterStatusColor = new SolidColorBrush(Colors.Green); //Printer online
                                JobDeviceStatusList[stationIndex].IsPrinterConnected = true;
                            }
                        }
                        else
                        {
                            JobDeviceStatusList[stationIndex].PrinterStatusColor = new SolidColorBrush(Colors.Red); // Printer offline
                            JobDeviceStatusList[stationIndex].IsPrinterConnected = false;

                            if (JobList[stationIndex].OperationStatus != OperationStatus.Stopped && !_detectPrinterDisconnected)
                            {
                                CusAlert.Show($"Station {stationIndex}: Printer's Disconnected !", ImageStyleMessageBox.Error);
                                _detectPrinterDisconnected = true;
                            }
                        }

                        if (controllerStsBytes == (byte)ControllerStatus.Connected) // Controller Status Change
                        {
                            if (JobList[stationIndex].StatusStartButton)
                            {
                                SaveConnectionSetting(); // send connection setting until run job
                            }

                            if (JobDeviceStatusList[stationIndex].ControllerStatusColor.Color != Colors.Green)
                            {
                                JobDeviceStatusList[stationIndex].ControllerStatusColor = new SolidColorBrush(Colors.Green); //Controller online
                            }
                        }
                        else
                        {
                            JobDeviceStatusList[stationIndex].ControllerStatusColor = new SolidColorBrush(Colors.Red); //Controller online
                        }
                        await Task.Delay(2000);
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("DevicesStatusChange Failed !");
                    }
                }
            });
        }

        private void GetPrinterTemplateName(byte[] result)
        {
            try
            {
                var res = result.Skip(3).ToArray();
                string[]? resString = DataConverter.FromByteArray<string[]>(res); // convert to String
                CreateNewJob.TemplateListFirstFound = resString?.ToList();
                CreateNewJob.TemplateList = CreateNewJob.TemplateListFirstFound; // Update to Listview
            }
            catch (Exception)
            {
#if DEBUG
                Debug.WriteLine("GetPrinterTemplateName Error!");
#endif
            }
        }

        private async void GetStatisticsAsync(int stationIndex)
        {
            while (true)
            {
                try
                {
                    if (!JobList[stationIndex].QueueSentNumberBytes.TryDequeue(out var resultSent)) resultSent = null;
                    if (!JobList[stationIndex].QueueReceivedNumberBytes.TryDequeue(out var resultReceived)) resultReceived = null;
                    if (!JobList[stationIndex].QueuePrintedNumberBytes.TryDequeue(out var resultPrinted)) resultPrinted = null;

                    _ = Application.Current.Dispatcher.InvokeAsync(() =>
                     {
                         string nullString = "\0\0\0\0\0\0\0";
                         if (resultSent != null)
                         {
                             var numSent = Encoding.ASCII.GetString(resultSent);
                             if (numSent != nullString)
                                 JobList[stationIndex].SentDataNumber = numSent.Trim();
                         }

                         if (resultReceived != null)
                         {
                             var numReceived = Encoding.ASCII.GetString(resultReceived);
                             if (numReceived != nullString)
                                 JobList[stationIndex].ReceivedDataNumber = numReceived.Trim();
                         }

                         if (resultPrinted != null)
                         {
                             var numPrinted = Encoding.ASCII.GetString(resultPrinted);
                             if (numPrinted != nullString)
                                 JobList[stationIndex].PrintedDataNumber = numPrinted.Trim(); // todo: get old value
                         }
                     });
                }

                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine("GetStatisticsAsync Error" + ex.Message);
#endif
                }
                await Task.Delay(1);
            }
        }

        private void GetCurrentPosDb(int stationIndex, byte[] result)
        {
            try
            {
                byte[] resCurrentPos = result.Skip(3).ToArray();
                byte[] curIndexBytes = new byte[7];
                byte[] curPageIndexBytes = new byte[7];

                Array.Copy(resCurrentPos, 0, curIndexBytes, 0, 7);
                Array.Copy(resCurrentPos, curIndexBytes.Length, curPageIndexBytes, 0, 7);

                string curIndex = Encoding.ASCII.GetString(curIndexBytes).Trim();
                string curPage = Encoding.ASCII.GetString(curPageIndexBytes).Trim();

                JobList[stationIndex].CurrentIndexDB = int.Parse(curIndex);
                JobList[stationIndex].CurrentPage = int.Parse(curPage);

                //Debug.WriteLine($"\nPage: {JobList[stationIndex].CurrentIndex} and Index: {JobList[stationIndex].CurrentPage}");
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine("Get Cur Pos DB Error" + ex.Message);
#endif
            }
        }

        private async void GetCurrentPrintedCodeAsync(int stationIndex)
        {
            while (true)
            {
                while (JobList[stationIndex].QueueCurrentPrintedCode.TryDequeue(out byte[]? result))
                {
                    try
                    {
                        string[]? printedCode = DataConverter.FromByteArray<string[]>(result);
                        if (printedCode != null)
                        {
                            JobList[stationIndex].RaiseChangePrintedCode(printedCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("GetCurrentPrintedCodeAsync faild" + ex.Message);
                    }
                    await Task.Delay(1);
                }
                await Task.Delay(1);
            }
        }

        public static string GetFormattedString(string resourceKey, params object[] args)
        {
            var resourceString = Application.Current.Resources[resourceKey] as string;
            return string.Format(resourceString, args);
        }

        private static void ShowTemplateLoadError(int stationIndex)
        {
            string message = GetFormattedString("TemplateLoadError", stationIndex + 1);
            CusAlert.Show(message, ImageStyleMessageBox.Warning);
        }

        private static void JobMessageStatusProcess(int stationIndex, NotifyType nt)
        {
            switch (nt)
            {
                case NotifyType.Unk:
                    break;
                case NotifyType.CameraConnectionFail:
                    break;
                case NotifyType.PrinterConnectionFail:
                    break;
                case NotifyType.StartSync:
                    CusMsgBox.Show("The system has started !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Info);
                    break;
                case NotifyType.CompleteLoadDB:
                    break;
                case NotifyType.DatabaseUnknownError:
                    break;
                case NotifyType.PrintedStatusUnknownError:
                    break;
                case NotifyType.CheckedResultUnknownError:
                    break;
                case NotifyType.CannotAccessDatabase:
                    CusMsgBox.Show("Can not access database !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.CannotAccessCheckedResult:
                    CusMsgBox.Show("Can not access checked result !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.CannotAccessPrintedResponse:
                    CusMsgBox.Show("Can not access printed response !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.DatabaseDoNotExist:
                    CusMsgBox.Show("Database do not exist !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.CheckedResultDoNotExist:
                    // CusMsgBox.Show("Checked result do not exist !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    CusAlert.Show($"Station {stationIndex + 1}: Checked result do not exist !", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.PrintedResponseDoNotExist:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer's not connected!", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.DuplicateData:
                    CusAlert.Show($"Station {stationIndex + 1}: Input Database duplicate!", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.NoJobsSelected:
                    CusMsgBox.Show("No job selected !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.NotLoadDatabase:
                    break;
                case NotifyType.NotLoadTemplate:
                    CusAlert.Show($"Station {stationIndex + 1}: Can't load template printer!", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.NotConnectCamera:
                    CusAlert.Show($"Station {stationIndex + 1}: Camera's not connected!", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.MissingParameter:
                    CusMsgBox.Show("Missing params !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.NotConnectPrinter:
                    //CusAlert.Show($"Station {stationIndex + 1}: Printer's not connected!", ImageStyleMessageBox.Warning);
                    // thinh
                    ShowTemplateLoadError(stationIndex);
                    break;
                case NotifyType.LeastOneAction:
                    break;
                case NotifyType.MissingParameterActivation:
                    break;
                case NotifyType.MissingParameterPrinting:
                    break;
                case NotifyType.Unknown:
                    break;
                case NotifyType.ProcessCompleted:
                    CusAlert.Show($"Station {stationIndex + 1}: Process is completed!", ImageStyleMessageBox.Info);
                    // CusMsgBox.Show("Process is completed!", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Info);
                    break;
                case NotifyType.CannotCreatePodDataList:
                    break;
                case NotifyType.NotConnectServer:
                    break;
                case NotifyType.MissingParameterWarehouseInput:
                    break;
                case NotifyType.MissingParameterWarehouseOutput:
                    break;
                case NotifyType.CreatingWarehouseInputReceipt:
                    break;
                case NotifyType.PauseSystem:
                    // JobList[stationIndex].IsDBExist = true;
                    break;
                case NotifyType.DeviceDBLoaded:
                    //JobList[stationIndex].IsDBExist = true;
                    break;
                case NotifyType.StopSystem:
                    //JobList[stationIndex].IsDBExist = false;
                    break;
                case NotifyType.PrinterSuddenlyStop:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer suddenly Stop", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.StartEndPageInvalid:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer Start/End Page Invalid", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.NoPrintheadSelected:
                    CusAlert.Show($"Station {stationIndex + 1}: No Print Head Selected", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.PrinterSpeedLimit:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer speed limit", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.PrintheadDisconnected:
                    CusAlert.Show($"Station {stationIndex + 1}: Printhead Disconnected", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.UnknownPrinthead:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer Unknown Printhead", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.NoCartridges:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer No Cartridges", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.InvalidCartridges:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer Invalid Cartridges", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.OutOfInk:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer Out Of Ink", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.CartridgesLocked:
                    CusAlert.Show($"Station {stationIndex + 1}: Cartridges Locked", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.InvalidVersion:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer Invalid Version", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.IncorrectPrinthead:
                    CusAlert.Show($"Station {stationIndex + 1}: Printer Incorrect Printhead", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.ExportResultFail:
                    CusAlert.Show($"Station {stationIndex + 1}: Can't export", ImageStyleMessageBox.Warning);
                    break;
            }
        }

        private async Task GetOperationStatusAsync(int stationIndex)
        {
            try
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    bool isRunning = false;
                    bool isRunningCmp = false;
                    while (true)
                    {
                        PrinterStateList[stationIndex].State = LanguageModel.Language?[JobList[stationIndex].OperationStatus.ToString()].ToString();

                        switch (JobList[stationIndex].OperationStatus)
                        {
                            case OperationStatus.Processing:
                            case OperationStatus.Running:
                                JobList[stationIndex].StatusStartButton = false;
                                JobList[stationIndex].OperationStatus = JobList[stationIndex].OperationStatus;
                                JobList[stationIndex].StatusStopButton = true;
                                isRunningCmp = true;

                                break;
                            case OperationStatus.Stopped:
                                JobList[stationIndex].StatusStartButton = true;
                                JobList[stationIndex].OperationStatus = JobList[stationIndex].OperationStatus;
                                JobList[stationIndex].StatusStopButton = false;
                                isRunningCmp = false;
                                break;
                        }
                        if (isRunning != isRunningCmp)
                        {
                            isRunning = isRunningCmp;
                            if (isRunning) LoggerHelper.SetLogProperties(stationIndex, JobList[stationIndex].Name, "Operation", "System is Running !", EventsLogType.Warning);
                            else LoggerHelper.SetLogProperties(stationIndex, JobList[stationIndex].Name, "Operation", "System is Stopped !", EventsLogType.Info);
                        }

                        await Task.Delay(100);
                    }
                });

            }
            catch (Exception ex)
            {

#if DEBUG
                Debug.WriteLine("GetOperationStatus Error" + ex.Message);
#endif
            }
        }


        #endregion  END GET PRINTING PARAMS AND STATUS

        #region GET CHECKED DATA
        private async void ListenDetectModel(int stationIndex)
        {
            try
            {
                using IPCSharedHelper ipc = new(stationIndex, "DeviceToUISharedMemory_RD", SharedValues.SIZE_50MB, isReceiver: true);
                while (true)
                {
                    while (ipc.MessageQueue.TryDequeue(out byte[]? result))
                    {
                        //  SharedFunctions.PrintDebugMessage($"Queue Realtime Data {stationIndex}: " + ipc.MessageQueue.Count().ToString());
                          await Task.Run(() => ProcessItemDetectModel(result, stationIndex));
                        //ProcessItemDetectModel(result, stationIndex);
                       // await Task.Delay(1);
                    }
                    await Task.Delay(1);
                }
            }
            catch (Exception)
            {
#if DEBUG
                Debug.WriteLine("ListenDetectModel Fail");
#endif
            }
        }

        private void ProcessItemDetectModel(byte[] result, int stationIndex)
        {
            switch (result[0])
            {
                case (byte)SharedMemoryCommandType.DeviceCommand:
                    switch (result[2])
                    {
                        // Camera detect model
                        case (byte)SharedMemoryType.DetectModel:
                            //    DetectModel? dm = DataConverter.FromByteArray<DetectModel?>(result.Skip(3).ToArray());
                            JobList[stationIndex].QueueCameraDataDetect.Enqueue(result.Skip(3).ToArray());
                            break;

                        // Checked code
                        case (byte)SharedMemoryType.CheckedResultRaw:
                            JobList[stationIndex].QueueCurrentCheckedCode.Enqueue(result.Skip(3).ToArray());
                            break;

                        // Checked Statistics (total, passed, failed)
                        case (byte)SharedMemoryType.CheckedStatistics:
                            JobList[stationIndex].CheckedStatisticNumberBytes = result.Skip(3).ToArray();
                            break;
                    }
                    break;
            }
        }

        private async void GetCheckedCodeAsync(int stationIndex)
        {
            while (true)
            {
                while (JobList[stationIndex].QueueCurrentCheckedCode.TryDequeue(out byte[]? result))
                {
                    // SharedFunctions.PrintDebugMessage($"Queue checked code {stationIndex}: " + JobList[stationIndex].QueueCurrentCheckedCode.Count().ToString());

                    try
                    {
                        string[]? checkedCode = DataConverter.FromByteArray<string[]>(result);
                        if (checkedCode != null)
                        {
                            JobList[stationIndex].RaiseChangeCheckedCode(checkedCode);
                        }
                    }
                    catch (Exception)
                    {
                        SharedFunctions.PrintDebugMessage("GetCheckedCodeAsync Error !");
                    }
                    await Task.Delay(1);

                }
                await Task.Delay(1);
            }
        }


        private async void GetCameraDataAsync(int stationIndex)
        {
            while (true)
            {
                while (JobList[stationIndex].QueueCameraDataDetect.TryDequeue(out byte[]? result))
                {
                    try
                    {
                        //  SharedFunctions.PrintDebugMessage($"QueueCameraDataDetect {stationIndex}: " + JobList[stationIndex].QueueCameraDataDetect.Count().ToString());
                        DetectModel? dm = DataConverter.FromByteArray<DetectModel>(result);
                        Image img = SharedFunctions.GetImageFromImageByte(dm?.ImageBytes); // Image Result
                                                                                           //  SharedFunctions.SaveByteArrayToFile("byteArray.bin", dm?.ImageBytes);
                        string? currentCode = dm?.Text;
                        long? processTime = dm?.CompareTime;
                        ComparisonResult? compareStatus = dm?.CompareResult;


                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (img != null) JobList[stationIndex].ImageResult = SharedFunctions.ConvertToBitmapImage(img);
                            if (currentCode != null) JobList[stationIndex].CurrentCodeData = currentCode;
                            if (compareStatus != null) JobList[stationIndex].CompareResult = (ComparisonResult)compareStatus;
                            if (processTime != null) JobList[stationIndex].ProcessingTime = (int)processTime;
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Detect Model Fail: " + ex.Message);
                    }
                    await Task.Delay(1);
                }
                await Task.Delay(1);
            }
        }

        private void GetCheckedStatistics(int stationIndex)
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                try
                {
                    while (true)
                    {
                        var result = JobList[stationIndex].CheckedStatisticNumberBytes;
                        if (result != null)
                        {
                            // Total Checked
                            byte[] totalCheckedBytes = new byte[7];
                            Array.Copy(result, 0, totalCheckedBytes, 0, 7);

                            //// Total Passed
                            byte[] totalPassedBytes = new byte[7];
                            Array.Copy(result, totalCheckedBytes.Length, totalPassedBytes, 0, 7);
                            var totalPassed = Encoding.ASCII.GetString(totalPassedBytes).Trim();

                            // Total Fail
                            byte[] totalFailBytes = new byte[7];
                            Array.Copy(result, totalCheckedBytes.Length + totalPassedBytes.Length, totalFailBytes, 0, 7);

                            JobList[stationIndex].TotalChecked = Encoding.ASCII.GetString(totalCheckedBytes).Trim();
                            JobList[stationIndex].TotalPassed = Encoding.ASCII.GetString(totalPassedBytes).Trim();
                            JobList[stationIndex].TotalFailed = Encoding.ASCII.GetString(totalFailBytes).Trim();

                            //Update Percent
                            UpdatePercentForCircleChart(stationIndex);
                        }
                        await Task.Delay(1);
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine("GetCheckedStatistics Error" + ex.Message);
#endif
                }
            });
        }

        private void UpdatePercentForCircleChart(int stationIndex)
        {
            if (int.TryParse(JobList[stationIndex].TotalChecked, out int totalChecked))
            {
                try
                {
                    if (totalChecked >= 0&& JobList[stationIndex].CompareType== CompareType.Database) // DB MODE
                    {
                        double percent = (double)totalChecked * 100 / JobList[stationIndex].TotalRecDb;
                        percent = Math.Round(percent, 2);
                        if (percent > 100) percent = 100;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            JobList[stationIndex].CircleChart.Value = percent;
                        });
                    }
                    else  // NO DB MODE
                    {
                        _ = double.TryParse(JobList[stationIndex].TotalPassed, out double pass);
                        double percent = Math.Round(pass / (double)totalChecked * 100);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            JobList[stationIndex].CircleChart.Value = percent;
                        });
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine("UpdatePercentForCircleChart Error: " + ex.Message);
#endif
                }
            }
        }

        #endregion END GET CHECKED DATA
    }
}
