﻿using IPCSharedMemory;
using SharedProgram.Controller;
using SharedProgram.DeviceTransfer;
using SharedProgram.Models;
using SharedProgram.Shared;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Shapes;
using static SharedProgram.DataTypes.CommonDataType;
using PrinterStatus = SharedProgram.DataTypes.CommonDataType.PrinterStatus;
namespace DipesLinkDeviceTransfer
{

    public partial class Program
    {
        private CancellationTokenSource _CTS_SendStsPrint = new();
        private CancellationTokenSource _CTS_SendStsCheck = new();
        private CancellationTokenSource _CTS_SendData = new();

        public void PrinterEventInit()
        {
            OnReceiveVerifyDataEvent -= SendVerifiedDataToPrinter;
            OnReceiveVerifyDataEvent += SendVerifiedDataToPrinter;
            SharedEvents.OnPrinterDataChange += SharedEvents_OnPrinterDataChange; // Printer Data change event
            //SharedEvents.OnVerifyAndPrindSendDataMethod -= SharedEvents_OnVerifyAndPrindSendDataMethod;
            //SharedEvents.OnVerifyAndPrindSendDataMethod += SharedEvents_OnVerifyAndPrindSendDataMethod;
            ReceiveDataFromPrinterHandlerAsync();
        }

       

        private void SharedEvents_OnPrinterDataChange(object? sender, EventArgs e)
        {
            if (sender is PODDataModel)
            {
                _QueueBufferPrinterReceivedData.Enqueue(sender);
            }
        }

        private NotifyType CheckInitDataErrorAndGenerateMessage()
        {
            if (_InitDataErrorList.Count > 0)
            {
                foreach (var value in _InitDataErrorList)
                {
                    NotifyType tmp = value switch
                    {
                        NotifyType.DatabaseUnknownError => value,
                        NotifyType.PrintedStatusUnknownError => value,
                        NotifyType.CheckedResultUnknownError => value,
                        NotifyType.CannotAccessDatabase => value,
                        NotifyType.CannotAccessCheckedResult => value,
                        NotifyType.CannotAccessPrintedResponse => value,
                        NotifyType.DatabaseDoNotExist => value,
                        NotifyType.CheckedResultDoNotExist => value,
                        NotifyType.PrintedResponseDoNotExist => value,
                        NotifyType.CannotCreatePodDataList => value,
                        NotifyType.Unknown => value,
                        _ => NotifyType.Unk,
                    };

                    return tmp;
                }
            }
            return NotifyType.Unk;
        }

        private CheckCondition CheckAllTheConditions()
        {
            // Check Camera Connection
//#if !DEBUG
//            if (DatamanCameraDeviceHandler != null && !DatamanCameraDeviceHandler.IsConnected)
//            {
//                return CheckCondition.NotConnectCamera;
//            }
//#endif

            if (_SelectedJob != null && _SelectedJob.CompareType == CompareType.Database && _SelectedJob.PrinterSeries == PrinterSeries.RynanSeries)
            {
                //Check Printer Connection
                if (RynanRPrinterDeviceHandler != null && !RynanRPrinterDeviceHandler.IsConnected())
                {
                    return CheckCondition.NotConnectPrinter;
                }

                // Check Printer Template
                if (_SelectedJob.CompareType == CompareType.Database && (_SelectedJob == null || _SelectedJob.PrinterTemplate == ""))
                {
                    return CheckCondition.MissingParameterActivation;
                }
            }

            // Check list POD code for Print and Check
            if (_CodeListPODFormat == null || _CodeListPODFormat.IsEmpty)
            {
                return CheckCondition.MissingParameterActivation;
            }

            return CheckCondition.Success;
        }

        public void StartProcess()
        {
            if (_SelectedJob == null) return;

            // Block Run More Times
            if (SharedValues.OperStatus == OperationStatus.Running || SharedValues.OperStatus == OperationStatus.Processing) // Only Start 1 times
            {
#if DEBUG
                Console.WriteLine("Notify Error: The system has started !");
#endif
                NotificationProcess(NotifyType.StartSync);
                return;
            }

            // Check Init Data
            var checkInitDataNotifyType = CheckInitDataErrorAndGenerateMessage();
            if (checkInitDataNotifyType != NotifyType.Unk)
            {
                NotificationProcess(checkInitDataNotifyType);
#if DEBUG
                Console.WriteLine("Check Init Data Error: " + checkInitDataNotifyType.ToString());
#endif
                return;
            }

            // Check Database Exist
            bool isDatabaseDeny = _SelectedJob.CompareType == CompareType.Database && _TotalCode == 0;
            if (isDatabaseDeny)
            {
                // Show Dialog Error !
#if DEBUG
                Console.WriteLine("Database doesn't exist !");
#endif
                NotificationProcess(NotifyType.DatabaseDoNotExist);
                return;
            }

            //Check All Condition
            CheckCondition checkCondition = CheckAllTheConditions();
            if (checkCondition != CheckCondition.Success)
            {
                switch (checkCondition)
                {
                    case CheckCondition.NotLoadDatabase:
                        Console.WriteLine("Notify Error: Please check database connection");
                        NotificationProcess(NotifyType.NotLoadDatabase);
                        break;
                    case CheckCondition.NotConnectCamera:
                        Console.WriteLine("Notify Error: Please check camera connection");
                        NotificationProcess(NotifyType.NotConnectCamera);
                        break;
                    case CheckCondition.NotLoadTemplate:
                        Console.WriteLine("Notify Error: Please check the printer template");
                        NotificationProcess(NotifyType.NotLoadTemplate);
                        break;
                    case CheckCondition.MissingParameter:
                        Console.WriteLine("Notify Error: Some parameter is missing ! Please check again");
                        NotificationProcess(NotifyType.MissingParameter);
                        break;
                    case CheckCondition.NotConnectPrinter:
                        Console.WriteLine("Notify Error: Please check printer connection");
                        NotificationProcess(NotifyType.NotConnectPrinter);
                        break;
                    case CheckCondition.NotConnectServer:
                        NotificationProcess(NotifyType.NotConnectServer);
                        break;
                    case CheckCondition.LeastOneAction:
                        NotificationProcess(NotifyType.LeastOneAction);
                        break;
                    case CheckCondition.MissingParameterActivation:
                        Console.WriteLine("Notify Error: Some activation params are missing! Please check again");
                        NotificationProcess(NotifyType.MissingParameterActivation);
                        break;
                    case CheckCondition.MissingParameterPrinting:
                        NotificationProcess(NotifyType.MissingParameterPrinting);
                        break;
                    case CheckCondition.MissingParameterWarehouseInput:
                        NotificationProcess(NotifyType.MissingParameterWarehouseInput);
                        break;
                    case CheckCondition.MissingParameterWarehouseOutput:
                        NotificationProcess(NotifyType.MissingParameterWarehouseOutput);
                        break;
                    case CheckCondition.CreatingWarehouseInputReceipt:
                        NotificationProcess(NotifyType.CreatingWarehouseInputReceipt);
                        break;
                    case CheckCondition.NoJobsSelected:
                        Console.WriteLine("Notify Error: Please select a jon for system");
                        NotificationProcess(NotifyType.NoJobsSelected);
                        break;
                    default:
                        break;
                }
                return;
            }


            // Check Printer via Web API  : todo
            _SelectedJob.ExportNamePrefix = DateTime.Now.ToString(_SelectedJob.ExportNamePrefixFormat);
            // Logging Start : todo

            _QueueBufferPrinterReceivedData.Clear();

            if (_SelectedJob.CompareType == CompareType.Database &&
                _SelectedJob.PrinterSeries == PrinterSeries.RynanSeries)
            {
                SharedValues.OperStatus = OperationStatus.Processing;
                if (RynanRPrinterDeviceHandler != null &&
                _SelectedJob.JobStatus != JobStatus.Accomplished)
                {
                    RynanRPrinterDeviceHandler.SendData("STOP"); //send stop command to printer
                    Thread.Sleep(5);
                    RynanRPrinterDeviceHandler.SendData("CLPB"); //send clear buffer command to printer
                    string templateNameWithoutExt = _SelectedJob.PrinterTemplate.Replace(".dsj", "");
                    string startPrintCommand = string.Format("STAR;{0};1;1;true", templateNameWithoutExt);
                    Thread.Sleep(50);
                    RynanRPrinterDeviceHandler.SendData(startPrintCommand); // Send Start command to printer
                }
            }
            else
            {
                SharedValues.OperStatus = OperationStatus.Running;
            }

            _SelectedJob.JobStatus = JobStatus.Unfinished;
            SaveJobChangedSettings(_SelectedJob);

            SendCompleteDataToUIAsync();

            CompareAsync(); // Begin Compare data thread

            ExportImagesToFileAsync();

            UpdateUICheckedResultAsync(); // Begin Update Checked Result Thread + Queue for backup

            ExportCheckedResultToFileAsync(); // Begin export checked result to file  Thread

            if (_SelectedJob.CompareType == CompareType.Database) // If use mode database : start thread for backup printed code to file and update printed respone
            {
                UpdateUIPrintedResponseAsync(); // Begin Update Printed response Thread + Queue for backup
                ExportPrintedResponseToFileAsync();
            }
            //Shared.RaiseOnOperationStatusChangeEvent(Shared.OperStatus);
        }

        /// <summary>
        /// Desciption mode: 
        /// 1. Printed and Failed code -> reprint status
        /// 2. Printed and not yet checked -> reprint status
        /// 3. Waiting -> keep the waiting state
        /// </summary>
        private async void RePrintAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!_IsAfterProductionMode) return; // Only use for After Production

                    lock (_SyncObjCodeList)
                    {
                        if (_ListPrintedCodeObtainFromFile.Count > 0 && _CodeListPODFormat.Count > 0)
                        {
                            _TotalMissed = 0;
                            int statusColIndex = _ListPrintedCodeObtainFromFile[0].Length - 1;
                            foreach (var item in _CodeListPODFormat)
                            {
                                if (!item.Value.Status) // Not yet compare or failed compare
                                {
                                    if (_ListPrintedCodeObtainFromFile[item.Value.Index][statusColIndex] == "Printed") // Is Printed
                                        _ListPrintedCodeObtainFromFile[item.Value.Index][statusColIndex] = "Reprint"; // change to Re-Print state
#if DEBUG
                                    Console.WriteLine("Status Reprint: " + _ListPrintedCodeObtainFromFile[item.Value.Index][statusColIndex]);
#endif
                                }
                                else // Valid compare
                                {
                                    if (_ListPrintedCodeObtainFromFile[item.Value.Index][statusColIndex] != "Printed") // Not Printed
                                        _ListPrintedCodeObtainFromFile[item.Value.Index][statusColIndex] = "Printed";
#if DEBUG
                                    Console.WriteLine("Status Printed: " + _ListPrintedCodeObtainFromFile[item.Value.Index][statusColIndex]);
#endif
                                }
                            }
                            _TotalMissed = _TotalCode - NumberOfCheckPassed;
                        }
                    }
                    if (NumberOfCheckPassed < _TotalCode)
                    {
                        StartProcess();
                    }
                }
                catch (Exception)
                {
#if DEBUG
                    Console.WriteLine("Reprint failed !");
#endif 
                }
            });
        }

        private static void SaveJobChangedSettings(JobModel selectedJob)
        {
            string jobFilePath = SharedPaths.PathSubJobsApp + $"{JobIndex + 1}\\" + selectedJob.Name + SharedValues.Settings.JobFileExtension;
            string jobFileSelectedPath = SharedPaths.PathSelectedJobApp + $"Job{JobIndex + 1}\\" + selectedJob.Name + SharedValues.Settings.JobFileExtension;

            selectedJob.SaveJobFile(jobFilePath);
            if (File.Exists(jobFileSelectedPath))
            {
                selectedJob.SaveJobFile(jobFileSelectedPath);
            }
        }

        private async Task StopProcessAsync()
        {
            await Task.Run(() =>
            {
                KillTThreadSendWorkingDataToPrinter();
                _QueueBufferPrinterReceivedData.Clear();
                ClearBufferUpdateUI();

                if (_SelectedJob != null && _SelectedJob.PrinterSeries == PrinterSeries.RynanSeries)
                {
                    if (RynanRPrinterDeviceHandler != null)
                    {
                        RynanRPrinterDeviceHandler.SendData("STOP");
                        lock (_StopLocker)
                        {
                            _IsStopOK = false;
                            int countTimeout = 0;
                            while (!_IsStopOK && countTimeout < 1)
                            {
                                Monitor.Wait(_StopLocker, 3000); //Wait until there is a stop notify from the printer
                                countTimeout++;
                            }
                        }
                    }
                }

                _TotalMissed = 0;
                _CTS_UIUpdatePrintedResponse.Cancel();
                _CTS_CompareAction.Cancel();
                _QueueBufferCameraReceivedData.Enqueue(null);
                _QueueBufferPODDataCompared.Enqueue(null);

                SharedValues.OperStatus = OperationStatus.Stopped;

                if (_SelectedJob != null && _SelectedJob.CompareType == CompareType.Database)
                {
                    int currentCheckNumber = _SelectedJob.CompleteCondition == CompleteCondition.TotalChecked ? TotalChecked : NumberOfCheckPassed;

                    if (currentCheckNumber >= _TotalCode - _NumberOfDuplicate)
                    {
                        _SelectedJob.JobStatus = JobStatus.Accomplished;
                        SaveJobChangedSettings(_SelectedJob);
                    }
                }
            });
        }

        private void ClearBufferUpdateUI()
        {
            _QueueSentCodeNumber.Clear();
            _QueueReceivedCodeNumber.Clear();
        }

        private async void SendWorkingDataToPrinterHandlerAsync()
        {
            _CTS_SendWorkingDataToPrinter = new();
            var token = _CTS_SendWorkingDataToPrinter.Token;

            await Task.Run(async () =>
            {
                await Task.Delay(500, token); // Wait printer ready
                int counterCodeSent = 0;
                List<string[]> codeList;
                lock (_SyncObjCodeList)
                {
                    codeList = new List<string[]>(_ListPrintedCodeObtainFromFile); //Clone list code
                }
                lock (_PrintLocker)
                {
                    _IsPrintedWait = false; // Reset waiting mode
                    _PrintedResult = ComparisonResult.Valid;
                }
                try
                {
                    int startIndex = codeList.FindIndex(x => x.Last() != "Printed"); // Get the first not-printed code in database (first waiting)
                    if (startIndex == -1) return;
                    _IsPrintedWait = true;  // Init waiting send data

                    // Send data loop
                    for (int codeIndex = startIndex; codeIndex < codeList.Count; codeIndex++)
                    {
                        token.ThrowIfCancellationRequested();
                        string[] codeModel = codeList[codeIndex]; // Get data at waiting print index

                        //Ensure not printed or duplicate
                        if (codeModel[^1] != "Printed" && codeModel[^1] != "Duplicate")
                        {
                            string data = "";
                            token.ThrowIfCancellationRequested();
                            data = string.Join(";", codeModel.Take(codeModel.Length - 1).Skip(1));// Trim data (exclude Index, Status column)
                            string command = string.Format("DATA;{0}", data); // Init send command

                            //Send data command to Printer
                            if (RynanRPrinterDeviceHandler != null)
                            {
                                RynanRPrinterDeviceHandler.SendData(command);
                                NumberOfSentPrinter++;
                                Interlocked.Exchange(ref _countSentCode, NumberOfSentPrinter);
                                _QueueSentCodeNumber.Enqueue(_countSentCode);
#if DEBUG
                                //  Console.WriteLine($"Data send to printer: {command}");
#endif
                            }

                            counterCodeSent++;

                            if (SharedValues.OperStatus == OperationStatus.Processing)
                            {
                                if (counterCodeSent >= 100 && _IsAfterProductionMode) // Sau 100 lần gửi đầu tiên thành công sẽ chuyển sang trạng thái Running
                                {
                                    SharedValues.OperStatus = OperationStatus.Running;
                                }
                            }

                            // After production mode
                            if (_IsAfterProductionMode)
                            {
                                if (counterCodeSent < 100)
                                {
                                    await Task.Delay(50, token); // Gửi 100 code đầu tiên theo thời gian delay
                                }
                                else
                                {
                                    await Task.Delay(50, token);
                                    bool detectStopSend = true;
                                    while (detectStopSend)
                                    {
                                        if (_QueueCountFeedback.TryDequeue(out int res)) // Chờ feedback đã in từ máy in mới được phép gửi tiếp
                                        {
                                            detectStopSend = false;
                                        }
                                        await Task.Delay(1, token);
                                    }
                                }
                            }
                        }
                    }

                    if (SharedValues.OperStatus == OperationStatus.Processing)
                    {
                        SharedValues.OperStatus = OperationStatus.Running;
                    }

                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Thread send data to printer was canceled !");
                }
                catch (Exception)
                {
                    Debug.WriteLine("Thread send data to printer was error!");
                    _ = StopProcessAsync();
                }
            }, _CTS_SendWorkingDataToPrinter.Token);
        }

        private async void ReceiveDataFromPrinterHandlerAsync()
        {
            _CTS_ReceiveDataFromPrinter = new();
            var token = _CTS_ReceiveDataFromPrinter.Token;
            await Task.Run(async () =>
            {
                try
                {
                    int countTemp = 0;
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        var isDoneDequeue = _QueueBufferPrinterReceivedData.TryDequeue(out object? result);
                        if (!isDoneDequeue || result == null)
                        {
                            await Task.Delay(1, token);
                            continue;
                        }
                        try
                        {
                            if (result is PODDataModel podDataModel)
                            {
                                string[] pODcommand = podDataModel.Text.Split(';'); // Array of Command use only ; symbol

                                var PODResponseModel = new PODResponseModel { Command = pODcommand[0] }; // Get header command (DATA, RSFP,...)
                                if (PODResponseModel.Command != null)
                                {
                                    switch (PODResponseModel.Command)
                                    {
                                        case "RSLI": // Feedback Template Printer 
                                            pODcommand = pODcommand.Skip(1).ToArray(); // reject header RSLI
                                            pODcommand = pODcommand.Take(pODcommand.Length - 1).ToArray(); // Take n element remaining
                                            PODResponseModel.Template = pODcommand;
                                            _PrintProductTemplateList = PODResponseModel.Template; // add to list
                                            break;

                                        case "DATA": //Feedback data sent
                                            if (SharedValues.OperStatus != OperationStatus.Stopped)
                                            {
                                                ReceivedCode++;
                                                Interlocked.Exchange(ref _countReceivedCode, ReceivedCode);
                                                _QueueReceivedCodeNumber.Enqueue(_countReceivedCode);
                                            }
                                            break;

                                        case "RSFP": //Feedback data printed

                                            // Todo: On production 

                                            if (_IsAfterProductionMode)
                                            {
                                                // Condition for wait send
                                                CountFeedback++;

                                                Interlocked.Exchange(ref _countFb, CountFeedback);
                                                _QueueCountFeedback.Enqueue(_countFb);
                                            }
                                            //Example Receive data: RSFP;1/101;DATA; data1;data2;data3
                                            pODcommand = pODcommand.Skip(3).ToArray();  // get  [data1;data2;data3]
                                            ResultFinishPrint(pODcommand);
                                            await Task.Delay(1, token);
                                            break;

                                        case "STAR": //Feedback start print
                                            StartCommandHandler(pODcommand, PODResponseModel, podDataModel);
                                            break;

                                        case "STOP": //Feedback stop print
                                            StopCommandHandler(pODcommand, PODResponseModel);
                                            break;

                                        case "MON":  // Monitor 
                                            MonitorCommandHandler(pODcommand, PODResponseModel);
                                            break;

                                        default:
                                            break;
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
#if DEBUG
                            Console.WriteLine("ReceiveDataFromPrinterHandlerAsync logic Fail");
#endif
                        }
                    }
                }
                catch (OperationCanceledException)
                {
#if DEBUG
                    Console.WriteLine("Receive Data From Printer Thread was Canceled !");
#endif
                }
                catch (Exception)
                {
#if DEBUG
                    Console.WriteLine("Receive Data From Printer Thread was failed !");
#endif
                    KillAllProccessThread();
                    _ = StopProcessAsync();
                }
            });
        }
        private ConcurrentDictionary<string, int> _Emergency = new ConcurrentDictionary<string, int>();
        private void ResultFinishPrint(string[] podCommand)
        {
            if (_SelectedJob == null) return;

            string printedResult = "";

            if (_SelectedJob.JobType == JobType.VerifyAndPrint) // Verify and Print
            {
                if (DeviceSharedValues.VPObject.VerifyAndPrintBasicSentMethod)
                {
                    printedResult = podCommand[0];
                }
                else // Compares mode
                {
                    if (DeviceSharedValues.VPObject.PrintFieldForVerifyAndPrint.Count > 0)
                    {
                        if (_Emergency.TryGetValue(string.Join("", podCommand), out int codeIndex))
                        {
                            lock (_SyncObjCodeList)
                            {
                                printedResult = GetCompareDataByPODFormat(_ListPrintedCodeObtainFromFile[codeIndex], _SelectedJob.PODFormat);
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in _SelectedJob.PODFormat)
                        {
                            if (item.Type == PODModel.TypePOD.FIELD)
                            {
                                int indexItem = item.Index - 1;
                                if (indexItem < podCommand.Length)
                                    printedResult += podCommand[item.Index - 1];
                            }
                            else if (item.Type == PODModel.TypePOD.TEXT)
                            {
                                printedResult += item.Value;
                            }
                        }
                    }
                }
            }
            else // After production
            {
                foreach (PODModel podData in _SelectedJob.PODFormat)
                {
                    if (podData.Type == PODModel.TypePOD.FIELD)
                    {
                        int indexItem = podData.Index - 1;
                        if (indexItem < podCommand.Length)
                            printedResult += podCommand[indexItem]; // Get printed Result by index POD format
                    }
                    else if (podData.Type == PODModel.TypePOD.TEXT)
                    {
                        printedResult += podData.Value; //Custom Text String concatenation
                    }
                }
            }
        
            _QueueBufferPODDataCompared.Enqueue(printedResult); // Add to UI update queue 
#if DEBUG
            // Debug.WriteLine($"UI Update Queue: {printedResult}");
#endif
        }

        private void StartCommandHandler(string[] podCommand, PODResponseModel podResponse, PODDataModel podData)
        {
            podResponse.Command = podCommand[0];
            podResponse.Status = podCommand[1]; // OK . Ready
                                                // podResponse.Error = podCommand[2]; // Error code

            // Correct case
            if (podResponse.Status != null && (podResponse.Status == "OK" || podResponse.Status == "READY"))
            {
                // If not Verify and Print : Send POD First
                if (podData.RoleOfPrinter == RoleOfStation.ForProduct && !_IsVerifyAndPrintMode)
                {
                    SendWorkingDataToPrinterHandlerAsync(); // Send POD data to printer when printer ready receive data
                }
            }
            else // Error case
            {
                string message = "Unknown";
                switch (podResponse.Error)
                {
                    case "001": message = "Open templates failed (dose not exist, others templates being opening,...)"; break;
                    case "002": message = "Start pages, End pages is invalid"; break;
                    case "003": message = "No printhead is selected"; break;
                    case "004": message = "Speed limit"; break;
                    case "005": message = "Printhead disconnected"; break;
                    case "006": message = "Unknown printhead"; break;
                    case "007": message = "No cartridges"; break;
                    case "008": message = "Invalid cartridges"; break;
                    case "009": message = "Out of ink"; break;
                    case "010": message = "Cartridges is locked"; break;
                    case "011": message = "Invalid version"; break;
                    case "012": message = "Incorrect printhead"; break;
                    default:
                        break;
                }
            }
        }

        private void StopCommandHandler(string[] podCommand, PODResponseModel podResponse)
        {
            podResponse.Status = podCommand[1];

            // Is STOP
            if (podResponse.Status != null && podResponse.Status == "OK")
            {
                lock (_StopLocker)
                {
                    _IsStopOK = true;
                    Monitor.PulseAll(_StopLocker); //Notification has stopped completed
                }
            }
        }

        private void MonitorCommandHandler(string[] podCommand, PODResponseModel podResponse)
        {
            if (_SelectedJob == null) return;
            try
            {
                podResponse.Status = podCommand[3];

                // Detect printer suddenly stop 
              //  Console.WriteLine($"operation: {SharedValues.OperStatus} !");
                if (podResponse.Status == "Stop" && (SharedValues.OperStatus == OperationStatus.Running || SharedValues.OperStatus == OperationStatus.WaitingData) &&
                    _SelectedJob.CompareType == CompareType.Database && _SelectedJob.JobType != JobType.StandAlone && !_isStopOrPauseAction)
                {
                    _ = StopProcessAsync();
                    NotificationProcess(NotifyType.PrinterSuddenlyStop);
                }

                switch (podResponse.Status)
                {
                    case "Stop":
                        _PrinterStatus = PrinterStatus.Stop;
                        SharedValues.OperStatus = OperationStatus.Stopped;
                       
                        break;
                    case "Processing":
                        _PrinterStatus = PrinterStatus.Processing;
                        SharedValues.OperStatus = OperationStatus.Processing;
                        break;
                    case "Ready":
                    case "Start":
                        _PrinterStatus = PrinterStatus.Ready;
                        _PrinterStatus = PrinterStatus.Start;
                        SharedValues.OperStatus = OperationStatus.Running;
                        break;
                    case "WaitingData":
                        _PrinterStatus = PrinterStatus.WaitingData;
                        SharedValues.OperStatus = OperationStatus.WaitingData;
                        break;
                    case "Printing": _PrinterStatus = PrinterStatus.Printing; break;
                    case "Connected": _PrinterStatus = PrinterStatus.Connected; ; break;
                    case "Disconnected": _PrinterStatus = PrinterStatus.Disconnected; break;
                    case "Error": _PrinterStatus = PrinterStatus.Error; ; break;
                    case "Disable": _PrinterStatus = PrinterStatus.Disable; break;
                    case "": _PrinterStatus = PrinterStatus.Null; break;
                    default:
                        break;
                }
                //Console.WriteLine("Printer Status: "+ _PrinterStatus);
            }
            catch (Exception)
            {
                Debug.WriteLine("Monitor response handle fail !");
            }
        }


        #region COMPARE 
        private async void CompareAsync()
        {
            // await Console.Out.WriteLineAsync("Compare Start");
            if (_SelectedJob == null) return;
            _CTS_CompareAction = new();
            var token = _CTS_CompareAction.Token;

            await Task.Run(async () =>
            {
                int currentCheckedIndex = -1;
                //  string staticText = "";
                bool isAutoComplete = _SelectedJob.CompareType == CompareType.Database;

                bool isReprint =
                    _SelectedJob.CompareType == CompareType.Database &&
                    _SelectedJob.JobType == JobType.AfterProduction &&
                    _TotalMissed > 0 &&
                    _SelectedJob.CompleteCondition == CompleteCondition.TotalChecked; // Condition for RePrint

                bool isDBStandalone = _SelectedJob.CompareType == CompareType.Database && _SelectedJob.JobType == JobType.StandAlone;
                int reprintStopCond = TotalChecked + _TotalMissed - _NumberOfDuplicate;
                int stopCond = _TotalCode - _NumberOfDuplicate;
                bool isOneMore = false;
                bool isComplete = false;
                try
                {
                    while (true)
                    {
                        if (token.IsCancellationRequested)
                        {
                            if (_QueueBufferCameraReceivedData.IsEmpty)
                                token.ThrowIfCancellationRequested();
                        }
                        else
                        {
                            if (isComplete)
                            {
                                continue;  // Complete Job
                            }
                            if (isAutoComplete)
                            {
                                bool completeCondition = false;
                                // Total check : ((pass + fail) >= total) code will auto stop
                                // Pass check: (pass >= total) check will auto stop
                                int stopNumber = _SelectedJob.CompleteCondition == CompleteCondition.TotalChecked ? TotalChecked : NumberOfCheckPassed;

                                // get stop number for verify and print mode
                                if (_SelectedJob.JobType == JobType.VerifyAndPrint)
                                {
                                    // is total pass check
                                    if (!(_SelectedJob.CompleteCondition == CompleteCondition.TotalChecked))
                                    {
                                        if (isOneMore)
                                        {
                                            stopNumber++;
                                        }

                                        if (NumberOfCheckPassed >= _TotalCode - _NumberOfDuplicate)
                                        {
                                            isOneMore = true;
                                        }
                                    }
                                    stopNumber--;
                                }
                                //await Console.Out.WriteLineAsync("Status: "+ SharedValues.OperStatus + stopNumber);
                                // get Stop condtion
                                if (!isReprint)
                                {
                                    completeCondition = SharedValues.OperStatus != OperationStatus.Stopped && stopNumber >= stopCond;
                                    //Debug.WriteLine("StopCounter : " + stopNumber);
                                   // Console.WriteLine($"Stop Number/Stop Cond: {stopNumber}/{stopCond}");
                                }
                                else
                                {
                                    completeCondition = SharedValues.OperStatus != OperationStatus.Stopped && stopNumber >= reprintStopCond;
                                }

                                if (completeCondition)
                                {
#if DEBUG
                                    Console.WriteLine("Complete the barcode verification process !");
#endif
                                    await StopProcessAsync();
                                    NotificationProcess(NotifyType.ProcessCompleted);
                                    isComplete = true;
                                    continue;
                                }
                            }
                        }


                        _ = _QueueBufferCameraReceivedData.TryDequeue(out DetectModel? detectModel); // Dequeue camera data

                        int compareIndex = _StartIndex + TotalChecked; // remember index

                        if (detectModel != null)
                        {
                            Stopwatch measureTime = Stopwatch.StartNew();

                            // CAN READ COMPARE
                            if (_SelectedJob.CompareType == CompareType.CanRead)
                            {

                            }

                            //STATIC TEXT COMPARE
                            else if (_SelectedJob.CompareType == CompareType.StaticText)
                            {

                            }
                            // DATABASE COMPARE
                            else if (_SelectedJob.CompareType == CompareType.Database)
                            {
                                bool isNeedToCheckPrintedResponse = true;
                               
                                if (_IsOnProductionMode)  // On Production
                                {
                                    // Todo
                                }

                                if (!isNeedToCheckPrintedResponse || _CodeListPODFormat == null) // Invalid check first
                                {
                                    detectModel.CompareResult = ComparisonResult.Invalided;
                                }
                                else
                                {
                                    detectModel.CompareResult = DatabaseCompare(detectModel.Text, ref currentCheckedIndex); // Conclude Compare Result

                                    if (_IsOnProductionMode)
                                    {
                                        // Todo
                                    }
                                }
                               
                                if (_IsVerifyAndPrintMode)
                                {
                                    VerifyAndPrintProcessing(detectModel,ref currentCheckedIndex);
                                }


                            }
                            // Output trigger for Camera
                            if (_SelectedJob.OutputCamera)
                            {
                                bool outputCondition = detectModel.CompareResult != ComparisonResult.Valid;
                                if (outputCondition)
                                {
                                    SharedEvents.RaiseOnCameraOutputSignalChangeEvent();
                                }
                            }


                            measureTime.Stop();

                            TotalChecked++;
                            Interlocked.Exchange(ref _countTotalCheked, TotalChecked);
                            _QueueTotalChekedNumber.Enqueue(_countTotalCheked);

                            if (detectModel.CompareResult == ComparisonResult.Valid)
                            {
                                NumberOfCheckPassed++;
                                Interlocked.Exchange(ref _countTotalPassed, NumberOfCheckPassed);
                                _QueueTotalPassedNumber.Enqueue(_countTotalPassed);
                            }
                            else
                            {
                                NumberOfCheckFailed++;
                                Interlocked.Exchange(ref _countTotalFailed, NumberOfCheckFailed);
                                _QueueTotalFailedNumber.Enqueue(_countTotalFailed);
                            }

                            // Modify Detect Model
                            detectModel.Index = compareIndex;
                            detectModel.CompareTime = measureTime.ElapsedMilliseconds;
                            detectModel.ProcessingDateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff");

                            // For Standalone Mode
                            // Todo
                            // Add result compare to buffer
                            _QueueBufferCameraDataCompared.Enqueue(detectModel);
                           
                        }
                        await Task.Delay(1, token);
                    }
                }
                catch (OperationCanceledException)
                {
#if DEBUG
                    Console.WriteLine("Thread Compare was Canceled !");
#endif
                    _CTS_UIUpdateCheckedResult?.Cancel();
                    _QueueBufferCameraDataCompared.Enqueue(null);

                    // Reset Number sent/received data and 
                    NumberOfSentPrinter = 0;
                    ReceivedCode = 0;

                }
                catch (Exception)
                {
#if DEBUG
                    Console.WriteLine("Thread Compare Fail");
#endif
                    _ = StopProcessAsync();
                }
            });
        }

        #region VerifyAndPrint
        private async Task InitVerifyAndPrindSendDataMethod()
        {

            if (DeviceSharedValues.VPObject.VerifyAndPrintBasicSentMethod) return;
            // EnableUIComponentWhenLoadData(false);
            _Emergency.Clear();
            _Emergency = await InitVNPUpdatePrintedStatusConditionBuffer();
            // EnableUIComponentWhenLoadData(true);
        }
        private async Task<ConcurrentDictionary<string, int>> InitVNPUpdatePrintedStatusConditionBuffer() 
        {
            // Thực hiện khi load hết database lên
            var result = new ConcurrentDictionary<string, int>();
            var _CheckedResultCodeSet = new HashSet<string>();
            string validCond = ComparisonResult.Valid.ToString();
            int columnCount = _ColumnNames.Length;
            foreach (string[] array in _ListCheckedResultCode)
            {
                if (columnCount == array.Length && array[2] == validCond)
                {
                    _CheckedResultCodeSet.Add(array[1]);
                }
            }

            if (_ListPrintedCodeObtainFromFile.Count > 0)
            {
                int codeLenght = _ListPrintedCodeObtainFromFile[0].Count() - 1;
                for (int index = 0; index < _ListPrintedCodeObtainFromFile.Count; index++)
                {
                    string[] row = _ListPrintedCodeObtainFromFile[index].ToArray();  // Get row data
                    string data = "";
                    foreach (PODModel item in _SelectedJob.PODFormat) // Get data by POD compare
                    {
                        if (item.Type == PODModel.TypePOD.DATETIME)
                        {
                            data += DateTime.Now;
                        }
                        else if (item.Type == PODModel.TypePOD.FIELD)
                        {
                            data += row[item.Index];
                        }
                        else
                        {
                            data += item.Value;
                        }
                    }

                    if (!_CheckedResultCodeSet.Contains(data)) // Data unchecked
                    {
                        if (_IsVerifyAndPrintMode)
                        {
                            string tmp = "";
                            for (int i = 1; i < row.Length - 1; i++)
                            {
                                PODModel tmpPOD = DeviceSharedValues.VPObject.PrintFieldForVerifyAndPrint.Find(x => x.Index == i);  // Find data by POD Send 
                                if (tmpPOD != null)
                                {
                                    tmp += row[tmpPOD.Index];
                                }
                            }

                            result.TryAdd(tmp, index);  // Add to List : Tạo sẵn một danh sách theo cấu trúc POD send để sẵn sàng gửi xuống máy in
                           // Console.WriteLine("VNP List "+ tmp +" - "+ index);
                        }
                    }
                }
            }
            await Task.Delay(10);
            _CheckedResultCodeSet.Clear();
            return result;
        }

        private void VerifyAndPrintProcessing(DetectModel detectModel,ref int currentCheckedIndex)
        {
            if (GetPrinterStatus())
            {
                string[] arr = Array.Empty<string>();

                //Valid
                if (detectModel.CompareResult == ComparisonResult.Valid)
                {
                    if (DeviceSharedValues.VPObject.VerifyAndPrintBasicSentMethod) // Verify and Print Basic Sent : Send exactly camera data to printer
                    {
                        arr = new string[3];
                        arr[1] = detectModel.Text;
                    }
                    else
                    {
                        lock (_SyncObjCodeList)
                        {
                            arr = _ListPrintedCodeObtainFromFile[currentCheckedIndex]; //Verify and Print Compare send method
                        }
                    }
                }
                // Invalid
                else
                {
                    if (DeviceSharedValues.VPObject.VerifyAndPrintBasicSentMethod)
                    {
                        arr = new string[3];
                    }
                    else
                    {
                        lock (_SyncObjCodeList)
                        {
                            arr = new string[_ListPrintedCodeObtainFromFile[0].Length];
                        }
                    }
                }
                RaiseOnReceiveVerifyDataEvent(arr);
                currentCheckedIndex = -1;
            }
        }
        public event EventHandler OnReceiveVerifyDataEvent;
        public void RaiseOnReceiveVerifyDataEvent(object sender)
        {
            OnReceiveVerifyDataEvent?.Invoke(sender, EventArgs.Empty);
        }
        public static bool GetPrinterStatus()
        {
            if (RynanRPrinterDeviceHandler != null && !RynanRPrinterDeviceHandler.IsConnected())
            {
                return false;
            }
            return true;
        }
        private void SendVerifiedDataToPrinter(object? sender, EventArgs e)
        {
            string command = "DATA;";
            string[] arr = sender as string[];

            if (DeviceSharedValues.VPObject.VerifyAndPrintBasicSentMethod) // is basic
            {
                command += arr[1] == null ? DeviceSharedValues.VPObject.FailedDataSentToPrinter : arr[1]; // If null => send custom string, else send detectmodel.text (read = send)
            }
            else // is compare
            {
                if (DeviceSharedValues.VPObject.PrintFieldForVerifyAndPrint.Count == 0) // If no select any field => send all field or send n failure field
                {
                    command += string.Join(";", arr.Take(arr.Length - 1).Skip(1)
                        .Select(x => x ?? DeviceSharedValues.VPObject.FailedDataSentToPrinter));
                }
                else // if select field => send selected field or send n value failure
                {
                    command += string.Join(";", DeviceSharedValues.VPObject.PrintFieldForVerifyAndPrint
                       .Where(x => x.Index < arr.Length - 1)
                       .Select(x => arr[x.Index] == null ? DeviceSharedValues.VPObject.FailedDataSentToPrinter : arr[x.Index]));
                }
            }
            if(RynanRPrinterDeviceHandler != null)
            {
                RynanRPrinterDeviceHandler.SendData(command);

                // Count Sent and push to Queue
                NumberOfSentPrinter++;
                Interlocked.Exchange(ref _countSentCode, NumberOfSentPrinter);
                _QueueSentCodeNumber.Enqueue(_countSentCode);

            }
            else
            {
                // Todo: Device Transfer Failed
            }
        }

        #endregion End VerifyAndPrint

        private ComparisonResult CanreadCompare(string txt)
        {
            return ComparisonResult.Valid;
        }

        private ComparisonResult StaticTextCompare(string txt, string staticText)
        {
            // Verify data static text
            if (staticText != "" && txt == staticText)
            {
                return ComparisonResult.Valid;
            }
            else
            {
                return ComparisonResult.Invalided;
            }
        }

        private ComparisonResult DatabaseCompare(string data, ref int currentValidIndex)
        {
            if (_CodeListPODFormat == null)
            {
                return ComparisonResult.Invalided;
            }
            else
            {
                bool checkNull = (data == "");
                if (!checkNull)
                {
                    if (_CodeListPODFormat.TryGetValue(data, out CompareStatus? compareStatus))
                    {
                        if (compareStatus.Index == -1)
                        {
                            compareStatus.Index = _ListPrintedCodeObtainFromFile.FindIndex(x => GetCompareDataByPODFormat(x, _SelectedJob.PODFormat) == data);
                        }
                        //If not yet check => change status to checked and => return valid Result
                        if (!compareStatus.Status) // Check duplicate
                        {
                            _CodeListPODFormat[data].Status = true;
                            currentValidIndex = compareStatus.Index;
                            return ComparisonResult.Valid;
                        }
                        // If there has been a previous check => Duplicate
                        else
                        {
                            return ComparisonResult.Duplicated;
                        }
                    }
                    //Not exist in Dictionary
                    else
                    {
                        return ComparisonResult.Invalided;
                    }
                }
                // Null code
                else
                {
                    return ComparisonResult.Null;
                }
            }
        }
        #endregion

        // UPDATE UI 
        private async void UpdateUIPrintedResponseAsync()
        {
            if (_SelectedJob == null) return;
            _CTS_UIUpdatePrintedResponse = new();
            var token = _CTS_UIUpdatePrintedResponse.Token;

            await Task.Run(async () =>
            {
                List<string[]> strPrintedResponseList = new();
                var isAutoComplete = _SelectedJob.CompareType == CompareType.Database; // Check if need to auto stop procces when compare type is verify and print
                int numOfResponse = NumberPrinted;
                int currentIndex = 0;
                int currentPage = 0;
                try
                {
                    while (true)
                    {
                        // Only stop if handled all data
                        if (token.IsCancellationRequested)
                            if (_QueueBufferPODDataCompared.IsEmpty)
                                token.ThrowIfCancellationRequested(); // Stop thread

                        // Waiting until have data
                        _QueueBufferPODDataCompared.TryDequeue(out string? podCommand);
                        if (podCommand != null)
                        {
                            // Update printed status
                            if (_CodeListPODFormat.TryGetValue(podCommand, out CompareStatus? compareStatus)) // Get status from done compare list
                            {
                                currentIndex = compareStatus.Index % _MaxDatabaseLine;
                                currentPage = compareStatus.Index / _MaxDatabaseLine;

                                var arrCurId = SharedFunctions.StringToFixedLengthByteArray(currentIndex.ToString(), 7);
                                var arrCurPage = SharedFunctions.StringToFixedLengthByteArray(currentPage.ToString(), 7);
                                var bytesPos = SharedFunctions.CombineArrays(arrCurId, arrCurPage);
                                _QueueCurrentPositionInDatabase.Enqueue(bytesPos);

                                // Printed response backup data
                                if (_ListPrintedCodeObtainFromFile[compareStatus.Index].Last() == "Waiting")
                                {
                                    lock (_SyncObjCodeList)// Update status column by index compare
                                    {
                                        (_ListPrintedCodeObtainFromFile[compareStatus.Index])[^1] = "Printed";
                                        strPrintedResponseList.Add(_ListPrintedCodeObtainFromFile[compareStatus.Index]); // Add new printed data to list
                                        _QueuePrintedCode.Enqueue(_ListPrintedCodeObtainFromFile[compareStatus.Index]); // Queue raw code for UI
                                    }
                                }

                                // Count Printed
                                NumberPrinted++;
                                //Debug.WriteLine("Printed Number: " + NumberPrinted);
                                Interlocked.Exchange(ref _countPrintedCode, NumberPrinted);
                                _QueuePrintedCodeNumber.Enqueue(_countPrintedCode);

                                // Add data to queue of Backup printed 
                                var printedCode = new List<string[]>(strPrintedResponseList);
                                _QueueBufferBackupPrintedCode.Enqueue(printedCode);


                                // Clear list
                                strPrintedResponseList.Clear();
                            }
                        }

                        //Time delay avoid frezee user interface
                        await Task.Delay(1, token);
                    }
                }
                catch (OperationCanceledException)
                {
#if DEBUG
                    Console.WriteLine("Thread update printed status was stoppped!");
#endif
                    _CTS_BackupPrintedResponse?.Cancel(); // Stop thread export respone data to file
                    _QueueBufferBackupPrintedCode.Enqueue(null); // Queue for backup printed code and status
                }
                catch (Exception)
                {
#if DEBUG
                    Console.WriteLine("Thread update printed status was error!");
#endif
                    KillAllProccessThread();
                    _ = StopProcessAsync();
                }
            });
        }

        private async void UpdateUICheckedResultAsync()
        {
            _CTS_UIUpdateCheckedResult = new();
            var token = _CTS_UIUpdateCheckedResult.Token;

            await Task.Run(async () =>
            {
                List<string[]> strResultCheckList = new();
                string[] strResult = Array.Empty<string>();
                try
                {
                    while (true)
                    {
                        if (token.IsCancellationRequested)
                            if (_QueueBufferCameraDataCompared.IsEmpty)
                                token.ThrowIfCancellationRequested();

                        _ = _QueueBufferCameraDataCompared.TryDequeue(out DetectModel? detectModel);
                        if (detectModel == null) { await Task.Delay(1); continue; };

                        // Backup Failed Image
                        var image = detectModel.Image ?? new Bitmap(100, 100);
                        if (_SelectedJob != null && _SelectedJob.IsImageExport)
                        {
                            if (detectModel.CompareResult != ComparisonResult.Valid)
                            {
                                var bitmapImage = new ExportImagesModel(image, detectModel.Index);
                                if (bitmapImage != null)
                                    _QueueBackupFailedImage.Enqueue(bitmapImage);
                            }
                        }
                        // Get result 
                        strResult = new string[] { detectModel.Index + "", detectModel.Text, detectModel.CompareResult.ToString(), (detectModel.CompareTime) + " ms", detectModel.ProcessingDateTime };
                        strResultCheckList.Add(strResult);

                        lock (_SyncObjCheckedResultList)
                        {
                            _ListCheckedResultCode.Add(strResult);
                            _QueueCheckedResult.Enqueue(strResult);
                            Console.WriteLine("Enque checked code: " + Thread.CurrentThread.ManagedThreadId);
                        }

                        _QueueBufferBackupCheckedResult.Enqueue(new List<string[]>(strResultCheckList)); // Add to queue for backup file checked result
                        _QueueCheckedResultForUpdateUI.Enqueue(detectModel); // Queue Detectmodel for UI Update (Image and Decode Time)

                        _ = strResult.DefaultIfEmpty();
                        strResultCheckList.Clear();

                        //Time delay avoid frezee user interface
                        await Task.Delay(1, token);
                    }
                }
                catch (OperationCanceledException)
                {
#if DEBUG
                    Console.WriteLine("Thread update checked result was stopped!");
#endif
                    _CTS_BackupFailedImage?.Cancel(); //Cancel backup failed image task
                    _CTS_BackupCheckedResult?.Cancel(); //Cancel backup checked result task

                    ///   _CTS_BackupPrintedResponse?.Cancel();


                }
                catch (Exception)
                {
#if DEBUG
                    Console.WriteLine("Thread update checked result was error!");
#endif
                    KillAllProccessThread();
                    _ = StopProcessAsync();
                }

            });
        }

        private void KillTThreadSendWorkingDataToPrinter()
        {
            _CTS_SendWorkingDataToPrinter?.Cancel();
        }

        private void KillAllProccessThread()
        {
            _CTS_UIUpdateCheckedResult?.Cancel();
            _CTS_UIUpdatePrintedResponse?.Cancel();
            _CTS_BackupCheckedResult?.Cancel();
            _CTS_BackupPrintedResponse?.Cancel();
            _CTS_BackupFailedImage?.Cancel();

            _CTS_SendStsPrint.Cancel();
            _CTS_SendStsPrint.Cancel();
            _CTS_SendData.Cancel();

            //_QueueBufferCameraDataCompared.Enqueue(null);
            //_QueueBufferPODDataCompared.Enqueue(null);
            //_QueueBufferBackupPrintedCode.Enqueue(null);
            //_QueueBufferBackupCheckedResult.Enqueue(null);
        }

        private void ReleaseResource()
        {
            try
            {
                // Release Cancel Token Source
                _CTS_SendWorkingDataToPrinter?.Cancel();
                _CTS_ReceiveDataFromPrinter?.Cancel();
                _CTS_CompareAction?.Cancel();
                _CTS_UIUpdatePrintedResponse?.Cancel();
                _CTS_UIUpdateCheckedResult?.Cancel();
                _CTS_BackupPrintedResponse?.Cancel();
                _CTS_BackupCheckedResult?.Cancel();
                _CTS_BackupFailedImage?.Cancel();

                _CTS_SendWorkingDataToPrinter?.Dispose();
                _CTS_ReceiveDataFromPrinter?.Dispose();
                _CTS_CompareAction?.Dispose();
                _CTS_UIUpdatePrintedResponse?.Dispose();
                _CTS_UIUpdateCheckedResult?.Dispose();
                _CTS_BackupPrintedResponse?.Dispose();
                _CTS_BackupCheckedResult?.Dispose();
                _CTS_BackupFailedImage?.Dispose();

                _ListCheckedResultCode.Clear();
                _CodeListPODFormat.Clear();
                _ListPrintedCodeObtainFromFile.Clear();

                _QueueBufferCameraReceivedData.Clear();
                _QueueBufferPrinterReceivedData.Clear();
                _QueueBufferBackupPrintedCode.Clear();
                _QueueBufferBackupCheckedResult.Clear();
                _QueueBufferPODDataCompared.Clear();
                _QueueCheckedResult.Clear();
                _QueuePrintedCode.Clear();
                _QueueTotalChekedNumber.Clear();
                _QueueTotalPassedNumber.Clear();
                _QueueTotalFailedNumber.Clear();
                _QueueCountFeedback.Clear();
                _QueueSentCodeNumber.Clear();
                _QueueReceivedCodeNumber.Clear();
                _QueuePrintedCodeNumber.Clear();
                _QueueBackupFailedImage.Clear();

                OnReceiveVerifyDataEvent -= SendVerifiedDataToPrinter;

            }
            catch (Exception) { }
        }


        /// <summary>
        /// Get Printer Template by Command 
        /// </summary>
        /// 
        public static void ObtainPrintProductTemplateList()
        {
            Task requestTemplateTask = Task.Run(async () =>
            {
                if (RynanRPrinterDeviceHandler != null && RynanRPrinterDeviceHandler.IsConnected())
                {
                    RynanRPrinterDeviceHandler.SendData("RQLI"); //send request template list command to printer
                    await Task.Delay(100);
                    SendTemplateListToUI(DeviceSharedValues.Index, _PrintProductTemplateList);
                }
            });
        }

        private static void SendTemplateListToUI(int index, string[] printerTemplate)
        {
            byte[] byteRes = DataConverter.ToByteArray(printerTemplate);
            MemoryTransfer.SendPrinterTemplateListToUI(index, byteRes);
        }

        public void SendCompleteDataToUIAsync()
        {
            // Sent Number, Received Number, Printed Number
            _CTS_SendStsPrint = new();
            var tokenSts = _CTS_SendStsPrint.Token;
            Task.Run(async () =>
            {
                byte[]? sentDataNumberBytes = new byte[7];
                byte[]? receivedDataNumberBytes = new byte[7];
                byte[]? printedDataNumberBytes = new byte[7];

                try
                {
                    while (true)
                    {
                        if (tokenSts.IsCancellationRequested)
                        {
                            if (_QueueSentCodeNumber.IsEmpty &&
                            _QueueReceivedCodeNumber.IsEmpty &&
                            _QueuePrintedCodeNumber.IsEmpty &&
                            _QueueCurrentPositionInDatabase.IsEmpty)
                            {
                                tokenSts.ThrowIfCancellationRequested();
                            }
                        }

                        // Sent POD Number
                        if (_QueueSentCodeNumber.TryDequeue(out int sentNumber))
                        {
                            //   Console.WriteLine($"Sent:  {sentNumber}");
                            sentDataNumberBytes = SharedFunctions.StringToFixedLengthByteArray(_countSentCode.ToString(), 7); // max 2000000
                            MemoryTransfer.SendCounterSentToUI(JobIndex, sentDataNumberBytes);
                        }

                        // Received POD Number
                        if (_QueueReceivedCodeNumber.TryDequeue(out int receivedNumber))
                        {
                            // Console.WriteLine($"Received: {receivedNumber}");
                            receivedDataNumberBytes = SharedFunctions.StringToFixedLengthByteArray(_countReceivedCode.ToString(), 7); // max 2000000
                            MemoryTransfer.SendCounterReceivedToUI(JobIndex, receivedDataNumberBytes);
                        }

                        // Printed Number
                        if (_QueuePrintedCodeNumber.TryDequeue(out int printedNumber))
                        {
                            //  Console.WriteLine($"Printed:  {printedNumber}");

                            printedDataNumberBytes = SharedFunctions.StringToFixedLengthByteArray(printedNumber.ToString(), 7);
                            MemoryTransfer.SendCounterPrintedToUI(JobIndex, printedDataNumberBytes);



                        }

                        //Current position (index and page)
                        if (_QueueCurrentPositionInDatabase.TryDequeue(out byte[]? currentPos)) // Current Pos in Databse (index and page)
                        {
                            //await Console.Out.WriteLineAsync("Current Pos: " + currentPos.ToString());
                            MemoryTransfer.SendCurrentPosDatabaseToUI(JobIndex, currentPos);
                        }
                        await Task.Delay(1);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Thread sent sts print canceled !");
                }
                catch (Exception)
                {
                    Console.WriteLine("Thread sent sts print fail !");
                }
            });

            // Total Checked, Total Passed, Total Failed
            _CTS_SendStsCheck = new();
            var tokenResult = _CTS_SendStsCheck.Token;
            Task.Run(async () =>
            {
                byte[]? totalCheckedNumberBytes = new byte[7];
                byte[]? totalPassedNumberBytes = new byte[7];
                byte[]? totalFailedNumberBytes = new byte[7];

                try
                {
                    while (true)
                    {
                        if (tokenResult.IsCancellationRequested)
                        {
                            if (_QueueTotalChekedNumber.IsEmpty && _QueueTotalPassedNumber.IsEmpty && _QueueTotalFailedNumber.IsEmpty)
                            {
                                tokenResult.ThrowIfCancellationRequested();
                            }
                        }

                        bool triggerData = false;

                        //Total Checked
                        if (_QueueTotalChekedNumber.TryDequeue(out int totalCheckedNum))
                        {
                            // Console.WriteLine("Total checked number " + totalCheckedNum);
                            totalCheckedNumberBytes = SharedFunctions.StringToFixedLengthByteArray(totalCheckedNum.ToString(), 7);
                            triggerData = true;
                        }

                        //Total Passed
                        if (_QueueTotalPassedNumber.TryDequeue(out int totalPassedNum))
                        {
                            //Console.WriteLine("Total passed number " + totalPassedNum);
                            totalPassedNumberBytes = SharedFunctions.StringToFixedLengthByteArray(totalPassedNum.ToString(), 7);
                            triggerData = true;
                        }

                        //Total Failed
                        if (_QueueTotalFailedNumber.TryDequeue(out int totalFailedNum))
                        {
                            // Console.WriteLine("Total failed number " + totalFailedNum);
                            totalFailedNumberBytes = SharedFunctions.StringToFixedLengthByteArray(totalFailedNum.ToString(), 7);
                            triggerData = true;
                        }

                        if (triggerData)
                        {
                            byte[] combineBytes = SharedFunctions.CombineArrays(totalCheckedNumberBytes, totalPassedNumberBytes, totalFailedNumberBytes);
                            MemoryTransfer.SendCheckedStatisticsToUI(JobIndex, combineBytes);
                        }

                        await Task.Delay(10);
                    }

                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Thread sent sts checked canceled !");
                }
                catch (Exception)
                {
                    Console.WriteLine("Thread sent sts checked fail !");
                }
            });

            //Printed code, Camera Data (Image, Point Focus), Checked Result
            _CTS_SendData = new();
            var tokenData = _CTS_SendData.Token;
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        try
                        {
                            if (tokenData.IsCancellationRequested)
                            {
                                if (_QueuePrintedCode.IsEmpty && _QueueCheckedResultForUpdateUI.IsEmpty && _QueueCheckedResult.IsEmpty)
                                {
                                    tokenData.ThrowIfCancellationRequested();
                                }
                            }

                            //Printed Code
                            if (_QueuePrintedCode.TryDequeue(out string[]? data))
                            {
                                MemoryTransfer.SendPrintedCodeRawToUI(JobIndex, DataConverter.ToByteArray(data));
                                //foreach (var item in data)
                                //{
                                //    Console.WriteLine(item);
                                //}
                            }

                            //Detect Model Data
                            if (_QueueCheckedResultForUpdateUI.TryDequeue(out DetectModel? detectModel))
                            {
                                var detectModelBytes = DataConverter.ToByteArray(detectModel);
                                MemoryTransfer.SendDetectModelToUI(JobIndex, detectModelBytes);
                            }

                            // Current Checked Result
                            if (_QueueCheckedResult.TryDequeue(out string[]? checkedResult))
                            {
                                var checkedResultBytes = DataConverter.ToByteArray(checkedResult);
                                MemoryTransfer.SendCheckedResultRawToUI(JobIndex, checkedResultBytes);
                             //   await Console.Out.WriteLineAsync("Checked code: "+ checkedResultBytes.Count());
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Loi gửi qua UI !");
                        }
                        await Task.Delay(10);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Thread sent realtime data canceled !");
                }
                catch (Exception)
                {
                    Console.WriteLine("Thread sent realtime data fail !");
                }
            });
        }

    }
}