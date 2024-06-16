﻿using DipesLink.Extensions;
using DipesLink.Models;
using DipesLink.Views.Extension;
using DipesLink.Views.Models;
using DipesLink.Views.UserControls.MainUc;
using IPCSharedMemory;
using SharedProgram.Models;
using SharedProgram.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using static DipesLink.Views.Enums.ViewEnums;
using static IPCSharedMemory.Datatypes.Enums;
using static SharedProgram.DataTypes.CommonDataType;
using Application = System.Windows.Application;

namespace DipesLink.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private int _NumberOfStation;

        private int _MaxDatabaseLine = 500;

        public event EventHandler? OnSomethingHappened;
        protected virtual void RaiseSomethingHappened(EventArgs e)
        {
            OnSomethingHappened?.Invoke(this, e);
        }
        #region SingletonInit
        // Singleton init
        // Singleton init
        private static MainViewModel? _instance;
        public static int StationNumber = 4;
        public static MainViewModel GetIntance()
        {
            _instance ??= new MainViewModel();
            return _instance;
        }
        #endregion


        public MainViewModel()
        {
            InitInstanceIPC();
            ViewModelSharedFunctions.LoadSetting();
            _NumberOfStation = ViewModelSharedValues.Settings.NumberOfStation;
            StationSelectedIndex = _NumberOfStation > 0 ? _NumberOfStation - 1 : StationSelectedIndex;

            InitDir();
            InitJobConnectionSettings();

            InitStations(_NumberOfStation);

        }


        private void InitInstanceIPC()
        {
            _ipcDeviceToUISharedMemory_DT = new(JobIndex, "DeviceToUISharedMemory_DT", SharedValues.SIZE_1MB);
            _ipcUIToDeviceSharedMemory_DT = new(JobIndex, "UIToDeviceSharedMemory_DT", SharedValues.SIZE_1MB);
            //_ipcDeviceToUISharedMemory_DB = new(JobIndex, "DeviceToUISharedMemory_DB", SharedValues.SIZE_200MB);
            //_ipcDeviceToUISharedMemory_RD = new(JobIndex, "DeviceToUISharedMemory_RD", SharedValues.SIZE_100MB);
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
                ListenDeviceTransferDataAsync(i);
                InitTabStationUI(i);
                GetCurrentJobDetail(i);
            }
        }

        public void ListenDeviceTransferDataAsync(int stationIndex)
        {
            CreateMultiObjects(stationIndex);
            
            Task.Run(() => { ListenDatabase(stationIndex); });

           // Task.Run(() => { ListenCheckedResultDatabase(stationIndex); });

            Task.Run(() => { ListenProcess(stationIndex); });

            Task.Run(() => { ListenDetectModel(stationIndex); });

            Task.Run(() => { GetOperationStatus(stationIndex); });

            Task.Run(() => { GetStatisticsAsync(stationIndex); });

            Task.Run(() => { DevicesStatusChange(stationIndex); });

            Task.Run(() => { GetCurrentPrintedCodeAsync(stationIndex); });

            Task.Run(() => { GetCheckedCodeAsync(stationIndex); });

            Task.Run(() => { GetCameraData(stationIndex); });

            Task.Run(() => { GetCheckedStatistics(stationIndex); });
        }

        private void CreateMultiObjects(int i)
        {
            int deviceTransferIDProc = ViewModelSharedFunctions.InitDeviceTransfer(i);
            JobList.Add(new JobOverview() { DeviceTransferID = deviceTransferIDProc, Index = i, JobTitleName = $"Station {i + 1}" }); // Job List Creation
            JobDeviceStatusList.Add(new JobDeviceStatus() { Index = i, Name = $"Devices{i + 1}" }); // Device Status List Creation
            PrinterStateList.Add(new PrinterState() { Name = $"Printer {i}", State = "" }); // Printer State List Creation

        }

        public void InitTabStationUI(int stationIndex)
        {
            var userControl = new JobDetails() { DataContext = JobList[stationIndex] };
            TabStation.Add(new TabItemModel() { Header = $"Station {stationIndex + 1}", Content = userControl });
            

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


            // Events for Button Start/Stop/Trigger
            JobList[index].StartButtonCommand -= StartButtonCommandEventHandler;
            JobList[index].PauseButtonCommand -= PauseButtonCommandEventHandler;
            JobList[index].StopButtonCommand -= StopButtonCommandEventHandler;
            JobList[index].TriggerButtonCommand -= TriggerButtonCommandEventHandler;
            JobList[index].OnPercentageChange -= PercentageChangeHandler;
            JobList[index].OnReprint -= ReprintHandler;
            JobList[index].OnLoadDb -= LoadDbEventHandler;

            JobList[index].StartButtonCommand += StartButtonCommandEventHandler;
            JobList[index].PauseButtonCommand += PauseButtonCommandEventHandler;
            JobList[index].StopButtonCommand += StopButtonCommandEventHandler;
            JobList[index].TriggerButtonCommand += TriggerButtonCommandEventHandler;
            JobList[index].OnPercentageChange += PercentageChangeHandler;
            JobList[index].OnReprint += ReprintHandler;
            JobList[index].OnLoadDb += LoadDbEventHandler;


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
                    MemoryTransfer.SendActionButtonToDevice(_ipcDeviceToUISharedMemory_DT, stationIndex, combineBytes);
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

        //public void InitDeviceTransfer(int index)
        //{
        //    string fullPath = SharedPaths.AppPath + SharedValues.DeviceTransferName;
        //    JobModel? jm = GetJobById(index);
        //    string arguments = "";
        //    arguments += "";
        //    arguments += index; // Index 
        //    arguments += "  " + jm?.CameraIP; // Camera IP Address 
        //    arguments += "  " + jm?.PrinterIP; // Printer IP Address 
        //    arguments += "  " + jm?.PrinterPort; // Printer Port
        //    ViewModelSharedValues.Running.StationList[index].TransferID = SharedFunctions.DeviceTransferStartProcess(index, fullPath, arguments);
        //}


        #region GET DATABASE

        /// <summary>
        /// Get Printing Database from Device Transfer
        /// </summary>
        /// <param name="stationIndex"></param>
        private async void ListenDatabase(int stationIndex)
        {
            if (_ipcDeviceToUISharedMemory_DB is null)
                _ipcDeviceToUISharedMemory_DB = new(JobIndex, "DeviceToUISharedMemory_DB", SharedValues.SIZE_200MB, isReceiver: true);
            //using IPCSharedHelper ipc = new(stationIndex, "DeviceToUISharedMemory_DB", capacity: 1024 * 1024 * 200, isReceiver: true);
            while (true)
            {
                bool isCompleteDequeue = _ipcDeviceToUISharedMemory_DB.MessageQueue.TryDequeue(out byte[]? result);
                if (result != null && isCompleteDequeue)
                {
                    switch (result[0])
                    {
                        case (byte)SharedMemoryCommandType.DeviceCommand:
                            switch (result[2])
                            {
                                case (byte)SharedMemoryType.DatabaseList:
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
                    Debug.WriteLine("Dont Reload Database for job: " + +stationIndex);
                    JobList[stationIndex].IsShowLoadingDB = Visibility.Collapsed;
                    return;
                };
                Debug.WriteLine("Reload Database for job: " + stationIndex);
                JobList[stationIndex].IsShowLoadingDB = Visibility.Visible;
                byte[] listBytes = result.Skip(3).ToArray();
                List<string[]>? listDatabase = DataConverter.FromByteArray<List<string[]>>(listBytes);
                List<(List<string[]>, int)> dbInfo = new(1);
                if (listDatabase != null)
                {

                    int firstWaiting = listDatabase.IndexOf(listDatabase.Find(x => x[x.Length - 1] == "Waiting"));
                    int totalCode = listDatabase.Count;
                    int currentPage = (listDatabase.Count > _MaxDatabaseLine) ? (firstWaiting > 0 ? firstWaiting / _MaxDatabaseLine : (firstWaiting == 0 ? 0 : totalCode / _MaxDatabaseLine - 1)) : 0;

                    JobList[stationIndex].CurrentPage = currentPage;
                    JobList[stationIndex].CurrentIndex = firstWaiting - 1;
                    dbInfo.Add((listDatabase, currentPage));
                    JobList[stationIndex].RaiseLoadCompleteDatabase(dbInfo);
                    JobList[stationIndex].IsDBExist = true;
                }
            }
            catch (Exception)
            {
#if DEBUG
                Console.WriteLine("Get Db Error !");
#endif
            }
        }

        /// <summary>
        /// Get Checked list firstime to UI
        /// </summary>
        /// <param name="stationIndex"></param>
        private async void ListenCheckedResultDatabase(int stationIndex)
        {
            //if (_ipcDeviceToUISharedMemory_DB is null)
            //    _ipcDeviceToUISharedMemory_DB = new(JobIndex, "DeviceToUISharedMemory_DB", SharedValues.SIZE_200MB, isReceiver: true);
            ////using IPCSharedHelper ipc = new(stationIndex, "DeviceToUISharedMemory_CheckedDB", capacity: 1024 * 1024 * 200, isReceiver: true);
            //while (true)
            //{
            //    bool isCompleteDequeue = _ipcDeviceToUISharedMemory_DB.MessageQueue.TryDequeue(out byte[]? result);
            //    if (result != null && isCompleteDequeue)
            //    {
            //        switch (result[0])
            //        {
            //            case (byte)SharedMemoryCommandType.DeviceCommand:
            //                switch (result[2])
            //                {
            //                    case (byte)SharedMemoryType.CheckedList:
            //                        GetCheckedList(stationIndex, result);
            //                        break;
            //                }
            //                break;
            //        }
            //    }
            //    await Task.Delay(100);
            //}
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
            using IPCSharedHelper ipc = new(stationIndex, "DeviceToUISharedMemory_DT", 1024 * 1024 * 1, isReceiver: true);
            while (true)
            {

                bool isCompleteDequeue = ipc.MessageQueue.TryDequeue(out byte[]? result);
                if (stationIndex == 0)
                {
                    // Debug.WriteLine(ipc.countRec);
                }
                if (isCompleteDequeue && result != null)
                {
                    switch (result[0])
                    {
                        case (byte)SharedMemoryCommandType.DeviceCommand:

                            switch (result[2]) // Shared Memory Type
                            {
                                // Camera Status
                                case (byte)SharedMemoryType.CamStatus:
                                    JobList[stationIndex].CameraStsBytes = result[3];
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
                                    JobList[stationIndex].SentNumberBytes = result.Skip(3).ToArray();
                                    // Debug.WriteLine("Sent: " + Encoding.ASCII.GetString(JobList[stationIndex].SentNumberBytes));
                                    break;
                                case (byte)SharedMemoryType.StatisticsCounterReceived:
                                    JobList[stationIndex].ReceivedNumberBytes = result.Skip(3).ToArray();
                                    // Debug.WriteLine("Rev: " + Encoding.ASCII.GetString(JobList[stationIndex].ReceivedNumberBytes));
                                    break;
                                case (byte)SharedMemoryType.StatisticsCounterPrinted:
                                    JobList[stationIndex].PrintedNumberBytes = result.Skip(3).ToArray();
                                    // Debug.WriteLine("Printed: " + Encoding.ASCII.GetString(JobList[stationIndex].PrintedNumberBytes));
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
                                        if (ntp != NotifyType.Unk)
                                        {
                                            JobMessageStatusProcess(stationIndex, ntp);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
#if DEBUG
                                        Debug.WriteLine($"JobMessageStatus failed {ex.Message}");
#endif
                                    }

                                    break;

                                case (byte)SharedMemoryType.JobOperationStatus:
                                    byte[] resOper = result.Skip(3).ToArray();
                                    try
                                    {
                                        var stsBtn = DataConverter.FromByteArray<OperationStatus>(resOper);
                                        JobList[stationIndex].OperationStatus = stsBtn;
                                    }
                                    catch (Exception)
                                    {
#if DEBUG
                                        Debug.WriteLine("JobOperationStatus failed");
#endif
                                    }
                                    break;
                                case (byte)SharedMemoryType.ControllerResponseMess:
                                    GetControllerMessageResponse(stationIndex, result);
                                    break;
                            }
                            break;

                        case 1:
                            break;

                        default:
                            break;
                    }
                }
                await Task.Delay(5);
            }
        }

        private void GetControllerMessageResponse(int stationIndex, byte[] result)
        {
            try
            {
                var res = result.Skip(3).ToArray();
                var mess = DataConverter.FromByteArray<string>(res);
                Application.Current?.Dispatcher.Invoke(() =>
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

        private void DevicesStatusChange(int stationIndex)
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                while (true)
                {
                    try
                    {
                        byte camStsBytes = JobList[stationIndex].CameraStsBytes;
                        byte printerStsBytes = JobList[stationIndex].PrinterStsBytes;
                        byte controllerStsBytes = JobList[stationIndex].ControllerStsBytes;

                        if (camStsBytes == (byte)CameraStatus.Connected) // Camera Status Change
                        {
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
                        }

                        if (printerStsBytes == (byte)PrinterStatus.Connected) // Printer Status Change
                        {
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
                    byte[] resultSent = JobList[stationIndex].SentNumberBytes;
                    byte[] resultReceived = JobList[stationIndex].ReceivedNumberBytes;
                    byte[] resultPrinted = JobList[stationIndex].PrintedNumberBytes;

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var nullString = "\0\0\0\0\0\0\0";
                        if (resultSent != null)
                        {
                            var numSent = Encoding.ASCII.GetString(resultSent);
                            if (numSent != nullString)
                                _JobList[stationIndex].SentDataNumber = numSent.Trim();
                        }

                        if (resultReceived != null)
                        {
                            var numReceived = Encoding.ASCII.GetString(resultReceived);
                            if (numReceived != nullString)
                                _JobList[stationIndex].ReceivedDataNumber = numReceived.Trim();
                        }

                        if (resultPrinted != null)
                        {
                            var numPrinted = Encoding.ASCII.GetString(resultPrinted);
                            if (numPrinted != nullString)
                                _JobList[stationIndex].PrintedDataNumber = numPrinted.Trim(); // todo: get old value
                        }
                    });
                }

                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine("GetStatisticsAsync Error" + ex.Message);
#endif
                }
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Get Current Index and Current Page Index of Database
        /// </summary>
        /// <param name="stationIndex"></param>
        /// <param name="result"></param>
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

                JobList[stationIndex].CurrentIndex = int.Parse(curIndex);
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
                if (JobList[stationIndex].QueueCurrentPrintedCode.TryDequeue(out byte[]? result))
                {
                    // Debug.WriteLine("Ket qua Printed: "+result.Count());
                    if (result != null)
                    {
                        try
                        {
                            string[]? printedCode = DataConverter.FromByteArray<string[]>(result);
                            //if (printedCode != null)
                            //    foreach (var item in printedCode)
                            //    {
                            //        Debug.Write(item );
                            //    }
                            //Debug.WriteLine($"at {stationIndex}\n");
                            if (printedCode != null)
                            {
                                JobList[stationIndex].RaiseChangePrintedCode(printedCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("GetCurrentPrintedCodeAsync faild" + ex.Message);
                        }

                    }
                }
                await Task.Delay(1);
            }
        }

        private void JobMessageStatusProcess(int stationIndex, NotifyType nt)
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
                    CusMsgBox.Show("Checked result do not exist !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.PrintedResponseDoNotExist:
                    CusMsgBox.Show("Printed response do not exist !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.NoJobsSelected:
                    CusMsgBox.Show("No job selected !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.NotLoadDatabase:
                    break;
                case NotifyType.NotLoadTemplate:
                    break;
                case NotifyType.NotConnectCamera:
                    // CusMsgBox.Show("Camera not connected !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    CusAlert.Show($"Station {stationIndex + 1}: Camera  not connected!", ImageStyleMessageBox.Warning);
                    break;
                case NotifyType.MissingParameter:
                    CusMsgBox.Show("Missing params !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
                    break;
                case NotifyType.NotConnectPrinter:
                    CusMsgBox.Show("Printer not connected !", "Notification", ButtonStyleMessageBox.OK, ImageStyleMessageBox.Error);
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
            }
        }

        private void GetOperationStatus(int stationIndex)
        {
            try
            {
                _ = Application.Current?.Dispatcher.Invoke(async () =>
                {
                    bool isRunning = false;
                    bool isRunningCmp = false;
                    while (true)
                    {

                        PrinterStateList[stationIndex].State = JobList[stationIndex].OperationStatus.ToString();
                        switch (JobList[stationIndex].OperationStatus)
                        {
                            case OperationStatus.Processing:
                            case OperationStatus.WaitingData:
                            case OperationStatus.Running:
                                JobList[stationIndex].StatusStartButton = false;
                                JobList[stationIndex].StatusStopButton = true;
                                isRunningCmp = true;

                                break;
                            case OperationStatus.Stopped:
                                JobList[stationIndex].StatusStartButton = true;
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
                if(_ipcDeviceToUISharedMemory_RD is null)
                    _ipcDeviceToUISharedMemory_RD = new(JobIndex, "DeviceToUISharedMemory_RD", SharedValues.SIZE_100MB, isReceiver:true);
                //using IPCSharedHelper ipc = new(stationIndex, "DeviceToUISharedMemory_RD", 1024 * 1024 * 100, isReceiver: true);
                while (true)
                {
                    bool isCompleteDequeue = _ipcDeviceToUISharedMemory_RD.MessageQueue.TryDequeue(out byte[]? result);
                    if (!isCompleteDequeue)
                    {
                        await Task.Delay(1); continue;
                    }
                    if (result != null)
                    {
                        switch (result[0])
                        {
                            case (byte)SharedMemoryCommandType.DeviceCommand:
                                switch (result[2])
                                {
                                    // Camera detect model
                                    case (byte)SharedMemoryType.DetectModel:
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
                }
            }
            catch (Exception)
            {
#if DEBUG
                Debug.WriteLine("ListenDetectModel Fail");
#endif
            }
        }

        private async void GetCheckedCodeAsync(int stationIndex)
        {
            while (true)
            {
                if (JobList[stationIndex].QueueCurrentCheckedCode.TryDequeue(out byte[]? result))
                {
                    if (result != null)
                    {
                        try
                        {
                            string[]? checkedCode = DataConverter.FromByteArray<string[]>(result);
                            if (checkedCode != null)
                                foreach (var item in checkedCode)
                                {
                                    Debug.Write(item);
                                }
                            Debug.WriteLine($"at {stationIndex}\n");
                            if (checkedCode != null)
                            {
                                JobList[stationIndex].RaiseChangeCheckedCode(checkedCode);
                            }
                        }
                        catch (Exception)
                        {
#if DEBUG
                            Debug.WriteLine("GetCheckedCodeAsync Error !");
#endif  
                        }

                    }
                }
                await Task.Delay(1);
            }
        }

        private void GetCameraData(int stationIndex)
        {
            _ = Application.Current?.Dispatcher.Invoke(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (JobList[stationIndex].QueueCameraDataDetect.TryDequeue(out byte[]? result))
                        {
                            if (result != null)
                            {
                                DetectModel? dm = DataConverter.FromByteArray<DetectModel>(result);
                                System.Drawing.Image img = SharedFunctions.GetImageFromImageByte(dm?.ImageData); // Image Result
                                string? currentCode = dm?.Text;
                                long? processTime = dm?.CompareTime;
                                ComparisonResult? compareStatus = dm?.CompareResult;
                                if (img != null)
                                {
                                    JobList[stationIndex].ImageResult = SharedFunctions.ConvertToBitmapImage(img);
                                }
                                if (currentCode != null)
                                {
                                    JobList[stationIndex].CurrentCodeData = currentCode;
                                }
                                if (compareStatus != null)
                                {
                                    JobList[stationIndex].CompareResult = (ComparisonResult)compareStatus;
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
#if DEBUG
                        Debug.WriteLine("Get Image Failed !");
#endif
                    }
                    await Task.Delay(5);
                }
            });
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
                        await Task.Delay(100);
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
                    if (totalChecked >= 0)
                    {
                        double percent = (double)totalChecked * 100 / JobList[stationIndex].TotalRecDb;
                        JobList[stationIndex].CircleChart.Value = Math.Round(percent, 2);
                        if (JobList[stationIndex].CircleChart.Value > 100) JobList[stationIndex].CircleChart.Value = 100;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine("UpdatePercentForCircleChart Error" + ex.Message);
#endif
                }
            }
        }

        #endregion END GET CHECKED DATA
    }
}
