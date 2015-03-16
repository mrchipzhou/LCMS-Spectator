﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using InformedProteomics.Backend.Data.Composition;
using LcmsSpectator.Config;
using LcmsSpectator.DialogServices;
using LcmsSpectator.Models;
using LcmsSpectator.Readers;
using LcmsSpectator.TaskServices;
using LcmsSpectator.Utils;
using ReactiveUI;

namespace LcmsSpectator.ViewModels
{
    public class MainWindowViewModel: ReactiveObject
    {
        // Commands
        public IReactiveCommand OpenDataSetCommand { get; private set; }
        public IReactiveCommand OpenRawFileCommand { get; private set; }
        public IReactiveCommand OpenTsvFileCommand { get; private set; }
        public IReactiveCommand OpenFeatureFileCommand { get; private set; }
        public IReactiveCommand OpenFromDmsCommand { get; private set; }
        public IReactiveCommand OpenSettingsCommand { get; private set; }
        public IReactiveCommand OpenAboutBoxCommand { get; private set; }
        public IReactiveCommand OpenRegisterModificationCommand { get; private set; }

        // Child view models
        public ScanViewModel ScanViewModel { get; private set; }
        public CreateSequenceViewModel CreateSequenceViewModel { get; private set; }
        public ReactiveList<DataSetViewModel> DataSets { get; private set; }
        public LoadingScreenViewModel LoadingScreenViewModel { get; private set; }

        /// <summary>
        /// Constructor for creating a new, empty MainWindowViewModel
        /// </summary>
        /// <param name="dialogService">Service for MVVM-friendly dialogs</param>
        /// <param name="taskService">Service for task queueing</param>
        public MainWindowViewModel(IMainDialogService dialogService, ITaskService taskService)
        {
            _dialogService = dialogService;
            _taskService = taskService;
            ScanViewModel = new ScanViewModel(_dialogService, TaskServiceFactory.GetTaskServiceLike(_taskService), new List<PrSm>());
            DataSets = new ReactiveList<DataSetViewModel> {ChangeTrackingEnabled = true};
            CreateSequenceViewModel = new CreateSequenceViewModel(DataSets, _dialogService);
            LoadingScreenViewModel = new LoadingScreenViewModel();

            // When a data set sets its ReadyToClose property to true, remove it from dataset list
            DataSets.ItemChanged.Where(x => x.PropertyName == "ReadyToClose")
                .Select(x => x.Sender)
                .Where(x => x.ReadyToClose)
                .Subscribe(dataSet =>
                {
                    var rawFileName = dataSet.RawFileName;
                    ScanViewModel.RemovePrSmsFromRawFile(rawFileName);
                    DataSets.Remove(dataSet);
                    if (DataSets.Count == 0) ShowSplash = true;
                });

            // When a PrSm is selected in the Protein Tree, make all data sets show the PrSm
            ScanViewModel.WhenAnyValue(x => x.SelectedPrSm)
                .Where(selectedPrSm => selectedPrSm != null)
                .Subscribe(selectedPrSm =>
                {
                    foreach (var dataSet in DataSets) dataSet.SelectedPrSm = selectedPrSm;
                });

            var openDataSetCommand = ReactiveCommand.Create();
            openDataSetCommand.Subscribe(_ => OpenDataSet());
            OpenDataSetCommand = openDataSetCommand;

            var openRawFileCommand = ReactiveCommand.Create();
            openRawFileCommand.Subscribe(_ => OpenRawFile());
            OpenRawFileCommand = openRawFileCommand;

            var openTsvFileCommand = ReactiveCommand.Create();
            openTsvFileCommand.Subscribe(_ => OpenIdFile());
            OpenTsvFileCommand = openTsvFileCommand;

            var openFeatureFileCommand = ReactiveCommand.Create();
            openFeatureFileCommand.Subscribe(_ => OpenFeatureFile());
            OpenFeatureFileCommand = openFeatureFileCommand;

            var openFromDmsCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.ShowOpenFromDms));
            openFromDmsCommand.Subscribe(_ => OpenFromDms());
            OpenFromDmsCommand = openFromDmsCommand;

            var openSettingsCommand = ReactiveCommand.Create();
            openSettingsCommand.Subscribe(_ => OpenSettings());
            OpenSettingsCommand = openSettingsCommand;

            var openAboutBoxCommand = ReactiveCommand.Create();
            openAboutBoxCommand.Subscribe(_ => OpenAboutBox());
            OpenAboutBoxCommand = openAboutBoxCommand;

            var openRegisterModificationCommand = ReactiveCommand.Create();
            openRegisterModificationCommand.Subscribe(_ =>
            {
                var custModVm = new CustomModificationViewModel("", false, _dialogService);
                if (_dialogService.OpenCustomModification(custModVm))
                {
                        if (custModVm.FromFormulaChecked)
                            IcParameters.Instance.RegisterModification(custModVm.ModificationName,
                                custModVm.Composition);
                        else if (custModVm.FromMassChecked)
                            IcParameters.Instance.RegisterModification(custModVm.ModificationName, custModVm.Mass);
                }
            });
            OpenRegisterModificationCommand = openRegisterModificationCommand;

            ShowSplash = true;
            FileOpen = false;

            // Warm up Informed Proteomics
            Task.Run(() => Averagine.GetIsotopomerEnvelopeFromNominalMass(50000));
        }

        /// <summary>
        /// Constructor for creating MainWindowViewModel with existing IDs
        /// </summary>
        /// <param name="dialogService">Service for MVVM-friendly dialogs</param>
        /// <param name="taskService">Service for task queueing</param>
        /// <param name="idTree">Existing IDs</param>
        public MainWindowViewModel(IMainDialogService dialogService, ITaskService taskService, IdentificationTree idTree) : this(dialogService, taskService)
        {
            ScanViewModel.AddIds(idTree.AllPrSms);
        }

        /// <summary>
        /// Determine whether or not "Open From DMS" should be shown on the menu based on whether
        /// or not the user is on the PNNL network or not.
        /// </summary>
        public bool ShowOpenFromDms
        {
            get { return System.Net.Dns.GetHostEntry("").HostName.Contains("pnl.gov"); }
        }

        private bool _showSplash;
        /// <summary>
        /// Toggles whether or not splash screen is shown.
        /// </summary>
        public bool ShowSplash
        {
            get { return _showSplash; }
            set { this.RaiseAndSetIfChanged(ref _showSplash, value); }
        }

        private bool _fileOpen;
        /// <summary>
        /// Tracks whether or not a file is currently open
        /// </summary>
        public bool FileOpen
        {
            get { return _fileOpen; }
            set { this.RaiseAndSetIfChanged(ref _fileOpen, value); }
        }

        /// <summary>
        /// Open raw file and/or id file, feature file
        /// </summary>
        public async Task OpenDataSet()
        {
            var openDataVm = new OpenDataWindowViewModel(_dialogService);
            if (_dialogService.OpenDataWindow(openDataVm))
            {
                ShowSplash = false;
                await Task.Run(async () =>
                {
                    DataSetViewModel dsVm;
                    //try
                    //{
                        dsVm = await ReadRawFile(openDataVm.RawFilePath);
                    //}
                    //catch (Exception)
                    //{
                    //    _dialogService.ExceptionAlert(new Exception("Cannot read raw file."));
                    //    if (DataSets.Count > 0) GuiInvoker.Invoke(() => DataSets.RemoveAt(DataSets.Count-1));
                    //    return;
                    //}
                    if (dsVm != null)
                    {
                        if (!String.IsNullOrEmpty(openDataVm.IdFilePath))
                            try
                            {
                                ReadIdFile(openDataVm.IdFilePath, dsVm.RawFilePath, dsVm);
                            }
                            catch (KeyNotFoundException e)
                            {
                                _dialogService.ExceptionAlert(e);
                            }
                            catch (Exception)
                            {
                                _dialogService.ExceptionAlert(new Exception("Cannot read ID file."));
                            }
                        if (!String.IsNullOrEmpty(openDataVm.FeatureFilePath))
                            try
                            {
                                dsVm.OpenFeatureFile(openDataVm.FeatureFilePath);
                            }
                            catch (KeyNotFoundException e)
                            {
                                _dialogService.ExceptionAlert(e);
                            }
                            catch (Exception)
                            {
                                _dialogService.ExceptionAlert(new Exception("Cannot read feature file."));
                            }
                    }
                });
            }
        }

        /// <summary>
        /// Prompt user for raw files and call ReadRawFile() to open file.
        /// </summary>
        public async Task OpenRawFile()
        {
            var rawFileNames = _dialogService.MultiSelectOpenFile(".raw", @"Supported Files|*.raw;*.mzML;*.mzML.gz|Raw Files (*.raw)|*.raw|MzMl Files (*.mzMl[.gz])|*.mzMl;*.mzML.gz");
            if (rawFileNames == null) return;
            ShowSplash = false;
            foreach (var rawFileName in rawFileNames)
            {
                var name = rawFileName;
                string fileName = rawFileName;
                await Task.Run(async () =>
                {
                    try
                    {
                        await ReadRawFile(name);
                    }
                    catch (Exception)
                    {
                        _dialogService.ExceptionAlert(new Exception(String.Format("Cannot read {0}.", fileName)));
                        if (DataSets.Count > 0) GuiInvoker.Invoke(() => DataSets.RemoveAt(DataSets.Count - 1));
                    }
                    if (DataSets.Count > 0) ScanViewModel.HideUnidentifiedScans = false;
                });
            }
        }

        /// <summary>
        /// Open identification file. Checks to ensure that there is a raw file open
        /// corresponding to this ID file.
        /// </summary>
        public async Task OpenIdFile()
        {
            const string formatStr = @"Supported Files|*.txt;*.tsv;*.mzId;*.mzId.gz;*.mtdb|TSV Files (*.txt; *.tsv)|*.txt;*.tsv|MzId Files (*.mzId[.gz])|*.mzId;*.mzId.gz|MTDB Files (*.mtdb)|*.mtdb";
            var tsvFileName = _dialogService.OpenFile(".txt", formatStr);
            if (tsvFileName == "") return;
            var fileName = Path.GetFileNameWithoutExtension(tsvFileName);
            var ext = Path.GetExtension(tsvFileName);
            string path = ext != null ? tsvFileName.Remove(tsvFileName.IndexOf(ext, StringComparison.Ordinal)) : tsvFileName;
            string rawFileName = "";
            DataSetViewModel dsVm = null;
            foreach (var ds in DataSets)      // Raw file already open?
            {
                if (ds.RawFileName == fileName)
                {   // xicVm with correct raw file name was found. Raw file is already open
                    dsVm = ds;
                    rawFileName = dsVm.RawFileName;
                }
            }
            if (dsVm == null)  // Raw file not already open
            {
                var directoryName = Path.GetDirectoryName(tsvFileName);
                if (directoryName != null)
                {
                    var directory = Directory.GetFiles(directoryName);
                    foreach (var file in directory) // Raw file in same directory as tsv file?
                        if (file == path + ".raw") rawFileName = path + ".raw";
                    if (rawFileName == "")  // Raw file was not in the same directory.
                    {   // prompt user for raw file path
                        /*_dialogService.MessageBox("Please select raw file.");
                        rawFileName = _dialogService.OpenFile(".raw", @"Raw Files (*.raw)|*.raw");*/
                        var selectDataVm = new SelectDataSetViewModel(_dialogService, DataSets);
                        if (_dialogService.OpenSelectDataWindow(selectDataVm))
                        {
                            // manually find raw file
                            if (!String.IsNullOrEmpty(selectDataVm.RawFilePath))
                            {
                                rawFileName = selectDataVm.RawFilePath;
                            }
                            else
                            {
                                rawFileName = selectDataVm.SelectedDataSet.RawFileName;
                                dsVm = selectDataVm.SelectedDataSet;
                            }
                        }
                    }
                }
            }
            if (!String.IsNullOrEmpty(rawFileName))
            {
                ShowSplash = false;
                await Task.Run(async () =>
                {
                    // Name of raw file was found
                    if (dsVm == null) // raw file isn't open yet
                        //try
                        //{
                            dsVm = await ReadRawFile(rawFileName);
                        //}
                        //catch (Exception)
                        //{
                        //    _dialogService.ExceptionAlert(new Exception("Cannot read raw file."));
                        //    if (DataSets.Count > 0) GuiInvoker.Invoke(() => DataSets.RemoveAt(DataSets.Count - 1));
                        //}
                    ReadIdFile(tsvFileName, rawFileName, dsVm); // finally read the TSV file
                });
            }
            else _dialogService.MessageBox("Cannot open ID file.");
        }

        /// <summary>
        /// Open feature file. Checks to ensure that there is a raw file open
        /// corresponding to this ID file.
        /// </summary>
        public async Task OpenFeatureFile()
        {
            const string formatStr = @"Ms1FT Files (*.ms1ft)|*.ms1ft";
            var tsvFileName = _dialogService.OpenFile(".ms1ft", formatStr);
            if (tsvFileName == "") return;
            var fileName = Path.GetFileNameWithoutExtension(tsvFileName);
            var ext = Path.GetExtension(tsvFileName);
            string path = ext != null ? tsvFileName.Remove(tsvFileName.IndexOf(ext, StringComparison.Ordinal)) : tsvFileName;
            string rawFileName = "";
            DataSetViewModel dsVm = null;
            foreach (var ds in DataSets)      // Raw file already open?
            {
                if (ds.RawFileName == fileName)
                {   // xicVm with correct raw file name was found. Raw file is already open
                    dsVm = ds;
                    rawFileName = dsVm.RawFileName;
                }
            }
            if (dsVm == null)  // Raw file not already open
            {
                var directoryName = Path.GetDirectoryName(tsvFileName);
                if (directoryName != null)
                {
                    var directory = Directory.GetFiles(directoryName);
                    foreach (var file in directory) // Raw file in same directory as tsv file?
                    {
                        var lPath = path.ToLower();
                        var lFile = file.ToLower();
                        if (lFile == lPath + ".raw") rawFileName = lPath + ".raw";
                        else if (lFile == lPath + ".mzml") rawFileName = lPath + ".mzml";
                        else if (lFile == lPath + ".mzml.gz") rawFileName = lPath + ".mzml.gz";
                    }
                    if (rawFileName == "")  // Raw file was not in the same directory.
                    {   // prompt user for raw file path
                        /*_dialogService.MessageBox("Please select raw file.");
                        rawFileName = _dialogService.OpenFile(".raw", @"Raw Files (*.raw)|*.raw");*/
                        var selectDataVm = new SelectDataSetViewModel(_dialogService, DataSets);
                        if (_dialogService.OpenSelectDataWindow(selectDataVm))
                        {
                            // manually find raw file
                            if (!String.IsNullOrEmpty(selectDataVm.RawFilePath))
                            {
                                rawFileName = selectDataVm.RawFilePath;
                            }
                            else
                            {
                                rawFileName = selectDataVm.SelectedDataSet.RawFileName;
                                dsVm = selectDataVm.SelectedDataSet;
                            }
                        }
                    }
                }
            }
            if (!String.IsNullOrEmpty(rawFileName))
            {
                ShowSplash = false;
                await Task.Run(async () =>
                {
                    // Name of raw file was found
                    if (dsVm == null) // raw file isn't open yet
                        //try
                        //{
                            dsVm = await ReadRawFile(rawFileName);
                        //}
                        //catch (Exception)
                        //{
                        //    _dialogService.ExceptionAlert(new Exception("Cannot read raw file."));
                        //    if (DataSets.Count > 0) GuiInvoker.Invoke(() => DataSets.RemoveAt(DataSets.Count - 1));
                        //}
                    if (dsVm != null)
                        try
                        {
                            dsVm.OpenFeatureFile(tsvFileName);
                        }
                        catch (KeyNotFoundException e)
                        {
                            _dialogService.ExceptionAlert(e);
                        }
                        catch (Exception)
                        {
                            _dialogService.ExceptionAlert(new Exception("Cannot read feature file."));
                        }
                });
            }
            else _dialogService.MessageBox("Cannot open feature file.");
        }

        /// <summary>
        /// Attempt to open Ids from identification file and associate raw file with them.
        /// </summary>
        /// <param name="idFileName">Name of id file.</param>
        /// <param name="rawFileName">Name of raw file to associate with id file.</param>
        /// <param name="dsVm">Data Set View model to associate with id file.</param>
        public void ReadIdFile(string idFileName, string rawFileName, DataSetViewModel dsVm)
        {
            LoadingScreenViewModel.IsLoading = true;
            var ids = new IdentificationTree();
            bool attemptToReadFile = true;
            var modIgnoreList = new List<string>();
            try
            {
                dsVm.MsPfParameters = MsPfParameters.ReadFromIdFilePath(idFileName);
            }
            catch (FormatException e)
            {
                _dialogService.ExceptionAlert(!String.IsNullOrEmpty(e.Message) ? e
                    : new Exception("MsPathFinder param file is incorrectly formatted."));
            }
            do
            {
                try
                {
                    var reader = IdFileReaderFactory.CreateReader(idFileName);
                    ids = reader.Read(modIgnoreList);
                    ids.SetLcmsRun(dsVm.Lcms, dsVm.RawFileName);
                    attemptToReadFile = false;
                }
                catch (KeyNotFoundException e)
                {
                    _dialogService.ExceptionAlert(e);
                    FileOpen = false;
                    LoadingScreenViewModel.IsLoading = false;
                    return;
                }
                catch (IOException e)
                {
                    _dialogService.ExceptionAlert(e);
                    FileOpen = false;
                    LoadingScreenViewModel.IsLoading = false;
                    return;
                }
                catch (InvalidModificationNameException e)
                {
                    var result =
                        _dialogService.ConfirmationBox(
                            String.Format(
                                "{0}\nWould you like to add this modification?\nIf not, all sequences containing this modification will be ignored.",
                                e.Message),
                            "Unknown Modification");
                    if (result)
                    {
                        var customModVm = new CustomModificationViewModel(e.ModificationName, true, _dialogService);
                        GuiInvoker.Invoke(() => _dialogService.OpenCustomModification(customModVm));
                        if (customModVm.Status)
                        {
                            if (customModVm.FromFormulaChecked)
                                IcParameters.Instance.RegisterModification(customModVm.ModificationName,
                                    customModVm.Composition);
                            else if (customModVm.FromMassChecked)
                                IcParameters.Instance.RegisterModification(customModVm.ModificationName, customModVm.Mass);
                        }
                        else
                        {
                            modIgnoreList.Add(e.ModificationName);
                        }
                    }
                    else
                    {
                        modIgnoreList.Add(e.ModificationName);
                    }
                }
                //catch (Exception)
                //{
                //    _dialogService.ExceptionAlert(new Exception("Cannot read ID file."));
                //    FileOpen = false;
                //    LoadingScreenViewModel.IsLoading = false;
                //    return;
                //}
            } while (attemptToReadFile);
            var data = ScanViewModel.Data;
            dsVm.AddIds(ids);
            data.AddRange(ids.AllPrSms);
            ScanViewModel.AddIds(data);
            ScanViewModel.HideUnidentifiedScans = true;
            FileOpen = true;
            LoadingScreenViewModel.IsLoading = false;
        }

        /// <summary>
        /// Open raw file
        /// </summary>
        /// <param name="rawFilePath">Path to raw file to open</param>
        public async Task<DataSetViewModel> ReadRawFile(string rawFilePath)
        {
            var dsVm = new DataSetViewModel(_dialogService, TaskServiceFactory.GetTaskServiceLike(_taskService)); // create data set view model
            GuiInvoker.Invoke(() => DataSets.Add(dsVm)); // add data set view model. Can only add to ObservableCollection in thread that created it (gui thread)
            GuiInvoker.Invoke(() => { CreateSequenceViewModel.SelectedDataSetViewModel = DataSets[0]; });
            dsVm.RawFilePath = rawFilePath;
            await dsVm.Initialize();
            FileOpen = true;
            return dsVm;
        }

        /// <summary>
        /// Open data set (raw file and ID files) from PNNL DMS system
        /// </summary>
        public async Task OpenFromDms()
        {
            Task task = null;
            var dmsLookUp = new DmsLookupViewModel(_dialogService);
            var data = _dialogService.OpenDmsLookup(dmsLookUp);
            if (data == null) return;
            var dataSetDirName = data.Item1;
            var jobDirName = data.Item2;
            var idFilePaths = new List<string>();
            string featureFilePath = "";
            string pbfFilePath = "";
            List<string> rawFileNames = null;
            string selectedTool ="";
            if (!String.IsNullOrEmpty(dataSetDirName))      // did the user actually choose a dataset?
            {
                selectedTool = dmsLookUp.SelectedJob.Tool;
                var dataSetDirFiles = Directory.GetFiles(dataSetDirName);
                var dataSetDirDirectories = Directory.GetDirectories(dataSetDirName);
                rawFileNames = (from filePath in dataSetDirFiles
                                let ext = Path.GetExtension(filePath)
                                where (ext == ".raw" || ext == ".mzml" || ext == ".gz")
                                select filePath).ToList();
                var pbfFolderPath = (from folderPath in dataSetDirDirectories
                                     let folderName = Path.GetFileNameWithoutExtension(folderPath)
                                     where (folderName.StartsWith("PBF_Gen"))
                                     select folderPath).FirstOrDefault();
                if (!String.IsNullOrEmpty(pbfFolderPath))
                {
                    var pbfIndirectionPath =
                        rawFileNames.Select(
                            fn => String.Format(@"{0}\{1}.pbf_CacheInfo.txt", pbfFolderPath, Path.GetFileNameWithoutExtension(fn))).FirstOrDefault();
                    if (!String.IsNullOrEmpty(pbfIndirectionPath) && File.Exists(pbfIndirectionPath))
                    {
                        var lines = File.ReadAllLines(pbfIndirectionPath);
                        if (lines.Length > 0) pbfFilePath = lines[0];
                    }
                }  
            }
            if (!String.IsNullOrEmpty(jobDirName))      // did the user actually choose a job?
            {
                var jobDir = Directory.GetFiles(jobDirName);
                idFilePaths = (from idFp in jobDir
                              let ext = Path.GetExtension(idFp)
                              where ext == ".mzid" || ext == ".gz" || ext == ".zip"
                              select idFp).ToList();
                featureFilePath = (from idFp in jobDir
                                   let ext = Path.GetExtension(idFp)
                                   where ext == ".ms1ft"
                                   select idFp).FirstOrDefault();
            }
            if (rawFileNames == null || rawFileNames.Count == 0)
            {   // no data set chosen or no raw files found for data set
                _dialogService.MessageBox("No raw files found for that data set.");
                LoadingScreenViewModel.IsLoading = false;
                return;
            }
            ShowSplash = false;
            foreach (var rawFilePath in rawFileNames)
            {
                var raw = String.IsNullOrEmpty(pbfFilePath) ? rawFilePath : pbfFilePath;
                var filePaths = idFilePaths;
                await Task.Run(async () =>
                {
                    var dsVm = await ReadRawFile(raw);
                    if (selectedTool == "MSPathFinder") dsVm.XicViewModel.PrecursorViewMode = PrecursorViewMode.Charges;
                    foreach (var filePath in filePaths)
                    {
                        if (!String.IsNullOrEmpty(filePath)) ReadIdFile(filePath, dsVm.RawFileName, dsVm);   
                    }
                    if (!String.IsNullOrEmpty(featureFilePath))
                    {
                        try
                        {
                            dsVm.OpenFeatureFile(featureFilePath);
                        }
                        catch (KeyNotFoundException e)
                        {
                            _dialogService.ExceptionAlert(e);
                        }
                        catch (Exception)
                        {
                            _dialogService.ExceptionAlert(new Exception("Cannot read feature file."));
                        }   
                    }
                });
            }
        }

        /// <summary>
        /// Open settings window
        /// </summary>
        public void OpenSettings()
        {
            var settingsViewModel = new SettingsViewModel(_dialogService);
            _dialogService.OpenSettings(settingsViewModel);
        }

        /// <summary>
        /// Open about box
        /// </summary>
        private void OpenAboutBox()
        {
            _dialogService.OpenAboutBox();
        }

        private readonly IMainDialogService _dialogService;
        private readonly ITaskService _taskService;
    }
}

