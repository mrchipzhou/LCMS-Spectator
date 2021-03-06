﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OpenDataWindowViewModel.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   View model for selecting raw file path, feature file path, and ID file path.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using InformedProteomics.Backend.MassSpecData;
using LcmsSpectator.DialogServices;
using LcmsSpectator.Models;
using LcmsSpectator.Utils;
using ReactiveUI;

namespace LcmsSpectator.ViewModels.FileSelectors
{
    /// <summary>
    /// View model for selecting raw file path, feature file path, and ID file path.
    /// </summary>
    public class OpenDataWindowViewModel : ReactiveObject
    {
        /// <summary>
        /// Dialog service for opening dialogs from view model.
        /// </summary>
        private readonly IDialogService dialogService;

        /// <summary>
        /// The selectedMSPF parameter file path.
        /// </summary>
        private string paramFilePath;

        /// <summary>
        /// The selected raw file path.
        /// </summary>
        private string rawFilePath;

        /// <summary>
        /// The selected feature file path.
        /// </summary>
        private string featureFilePath;

        /// <summary>
        /// The selected ID file path.
        /// </summary>
        private string idFilePath;

        /// <summary>
        /// The selected FASTA file path.
        /// </summary>
        private string fastaFilePath;

        /// <summary>
        /// A value indicating whether a parameter file has been selected.
        /// </summary>
        private bool paramFileSelected;

        /// <summary>
        /// A value indicating whether a dataset has been selected to be opened.
        /// </summary>
        private bool datasetSelected;

        /// <summary>
        /// Default constructor to support WPF design-time use
        /// </summary>
        [Obsolete("For WPF Design-time use only.", true)]
        public OpenDataWindowViewModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenDataWindowViewModel"/> class.
        /// </summary>
        /// <param name="dialogService">Dialog service for opening dialogs from view model.</param>
        public OpenDataWindowViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            this.WhenAnyValue(x => x.ParamFilePath).Subscribe(ReadParamFile);

            BrowseParamFilesCommand = ReactiveCommand.Create(BrowseParamFilesImplementation);
            BrowseRawFilesCommand = ReactiveCommand.Create(BrowseRawFilesImplementation);
            BrowseFeatureFilesCommand = ReactiveCommand.Create(BrowseFeatureFilesImplementation);
            BrowseIdFilesCommand = ReactiveCommand.Create(BrowseIdFilesImplementation);
            BrowseFastaFilesCommand = ReactiveCommand.Create(BrowseFastaFilesImplementation);

            ParamFileSelected = true;
            DatasetSelected = false;

            // Ok button should be enabled if RawFilePath isn't null or empty
            OkCommand = ReactiveCommand.Create(OkImplementation, this.WhenAnyValue(x => x.RawFilePath).Select(x => !string.IsNullOrWhiteSpace(x)));
            CancelCommand = ReactiveCommand.Create(CancelImplementation);

            Status = false;
        }

        /// <summary>
        /// Event that is triggered when the window is ready to be closed.
        /// </summary>
        public event EventHandler ReadyToClose;

        /// <summary>
        /// Gets a command that prompts the user for a MSPF parameter file path.
        /// </summary>
        public ReactiveCommand<Unit, Unit> BrowseParamFilesCommand { get; }

        /// <summary>
        /// Gets a command that prompts the user for a raw file path.
        /// </summary>
        public ReactiveCommand<Unit, Unit> BrowseRawFilesCommand { get; }

        /// <summary>
        /// Gets a command that prompts the user for a feature file path.
        /// </summary>
        public ReactiveCommand<Unit, Unit> BrowseFeatureFilesCommand { get; }

        /// <summary>
        /// Gets a command that prompts the user for an ID file path.
        /// </summary>
        public ReactiveCommand<Unit, Unit> BrowseIdFilesCommand { get; }

        /// <summary>
        /// Gets a command that prompts the user for a FASTA file path.
        /// </summary>
        public ReactiveCommand<Unit, Unit> BrowseFastaFilesCommand { get; }

        /// <summary>
        /// Gets a command that validates the selected raw file path and trigger ReadyToClose.
        /// </summary>
        public ReactiveCommand<Unit, Unit> OkCommand { get; }

        /// <summary>
        /// Gets a command that triggers ReadyToClose
        /// </summary>
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        /// <summary>
        /// Gets a value indicating whether a valid dataset or raw file path was selected.
        /// </summary>
        public bool Status { get; private set; }

        /// <summary>
        /// Gets or sets the selected MSPF parameter file path.
        /// </summary>
        public string ParamFilePath
        {
            get => paramFilePath;
            set => this.RaiseAndSetIfChanged(ref paramFilePath, value);
        }

        /// <summary>
        /// Gets or sets the selected raw file path.
        /// </summary>
        public string RawFilePath
        {
            get => rawFilePath;
            set => this.RaiseAndSetIfChanged(ref rawFilePath, value);
        }

        /// <summary>
        /// Gets or sets the selected feature file path.
        /// </summary>
        public string FeatureFilePath
        {
            get => featureFilePath;
            set => this.RaiseAndSetIfChanged(ref featureFilePath, value);
        }

        /// <summary>
        /// Gets or sets the selected ID file path.
        /// </summary>
        public string IdFilePath
        {
            get => idFilePath;
            set => this.RaiseAndSetIfChanged(ref idFilePath, value);
        }

        /// <summary>
        /// Gets or sets the selected FASTA file path.
        /// </summary>
        public string FastaFilePath
        {
            get => fastaFilePath;
            set => this.RaiseAndSetIfChanged(ref fastaFilePath, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether a parameter file has been selected to be opened.
        /// </summary>
        public bool ParamFileSelected
        {
            get => paramFileSelected;
            set => this.RaiseAndSetIfChanged(ref paramFileSelected, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether a dataset has been selected to be opened.
        /// </summary>
        public bool DatasetSelected
        {
            get => datasetSelected;
            set => this.RaiseAndSetIfChanged(ref datasetSelected, value);
        }

        /// <summary>
        /// Read a MSPF parameter file and populate raw file and feature file.
        /// </summary>
        /// <param name="filePath">The path to the parameter file.</param>
        private void ReadParamFile(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var mspfParams = MsPfParameters.ReadFromFile(filePath);
                var dirPath = Path.GetDirectoryName(filePath);

                var raw = string.Format("{0}\\{1}", dirPath, mspfParams.PuSpecFile);
                if (!string.IsNullOrWhiteSpace(mspfParams.PuSpecFile) && File.Exists(raw))
                {
                    RawFilePath = raw;
                }

                var feature = string.Format("{0}\\{1}", dirPath, mspfParams.FeatureFile);
                if (!string.IsNullOrWhiteSpace(mspfParams.FeatureFile) && File.Exists(feature))
                {
                    FeatureFilePath = feature;
                }

                var fasta = string.Format("{0}\\{1}", dirPath, mspfParams.DatabaseFile);
                if (!string.IsNullOrWhiteSpace(mspfParams.DatabaseFile) && File.Exists(fasta))
                {
                    FastaFilePath = fasta;
                }
            }
        }

        /// <summary>
        /// Implementation for <see cref="BrowseParamFilesCommand"/>.
        /// A command that prompts the user for a MSPF parameter file path.
        /// </summary>
        private void BrowseParamFilesImplementation()
        {
            var path = dialogService.OpenFile(".param", FileConstants.ParamFileFormatString);
            if (!string.IsNullOrWhiteSpace(path))
            {
                ParamFilePath = path;
            }
        }

        /// <summary>
        /// Implementation for <see cref="BrowseRawFilesCommand"/>.
        /// Prompts the user for a raw file path.
        /// </summary>
        private void BrowseRawFilesImplementation()
        {
            var path = dialogService.OpenFile(".raw", MassSpecDataReaderFactory.MassSpecDataTypeFilterString);
            if (!string.IsNullOrWhiteSpace(path))
            {
                RawFilePath = path;
            }
        }

        /// <summary>
        /// Implementation for <see cref="BrowseFeatureFilesCommand"/>.
        /// Prompts the user for a feature file path.
        /// </summary>
        private void BrowseFeatureFilesImplementation()
        {
            var path = dialogService.OpenFile(".ms1ft", FileConstants.FeatureFileFormatString);
            if (!string.IsNullOrWhiteSpace(path))
            {
                FeatureFilePath = path;
            }
        }

        /// <summary>
        /// Implementation for <see cref="BrowseIdFilesCommand"/>.
        /// Prompts the user for an ID file path.
        /// </summary>
        private void BrowseIdFilesImplementation()
        {
            var path = dialogService.OpenFile(".txt", FileConstants.IdFileFormatString);
            if (!string.IsNullOrWhiteSpace(path))
            {
                IdFilePath = path;
            }
        }

        /// <summary>
        /// Implementation for <see cref="BrowseFastaFilesCommand"/>.
        /// Prompts the user for a FASTA file path.
        /// </summary>
        private void BrowseFastaFilesImplementation()
        {
            var path = dialogService.OpenFile(".fasta", FileConstants.FastaFileFormatString);
            if (!string.IsNullOrWhiteSpace(path))
            {
                FastaFilePath = path;
            }
        }

        /// <summary>
        /// Implementation for OkCommand.
        /// Validates the selected raw file path and trigger ReadyToClose.
        /// </summary>
        private void OkImplementation()
        {
            Status = true;
            ReadyToClose?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Implementation for CancelCommand.
        /// Triggers ReadyToClose
        /// </summary>
        private void CancelImplementation()
        {
            Status = false;
            ReadyToClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
