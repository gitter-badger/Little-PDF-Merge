﻿using iTextSharp.text.pdf;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
namespace SuicSoft.LittlesPDFMerge.Windows
{
    class MergerViewModel
    {
        #region Commands
        /// <summary>
        /// This command merges all the pdfs in the listbox
        /// </summary>
        public static DelegateCommand MergeCommand { get; set; }
        /// <summary>
        /// This command adds a file to the listbox
        /// </summary>
        public static ICommand AddFileCommand { get; set; }
        /// <summary>
        /// This command moves the selected listbox item up.
        /// </summary>
        public static DelegateCommand MoveUpCommand { get; set; }
        /// <summary>
        /// This command moves the selected listbox item down
        /// </summary>
        public static DelegateCommand MoveDownCommand { get; set; }
        /// <summary>
        /// This command removes the selected listbox item.
        /// </summary>
        public static DelegateCommand RemoveCommand { get; set; }
        #endregion

        #region Constructor (.ctor)
        bool istopped = false;

        public MergerViewModel()
        {
            Files = new List<PDFItem>();
            MergeCommand = new DelegateCommand(Save, CanMerge);
            #region AddFileComand
            AddFileCommand = new DelegateCommand(() =>
            {
                //To get the button click animation to show. We need to open the Microsoft.Win32.OpenFileDialog in a new thread.
                new System.Threading.Thread(new System.Threading.ThreadStart(delegate()
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog();
                    openFileDialog.DefaultExt = ".pdf";
                    openFileDialog.Filter = "Portable Document Format (*.pdf)|*.pdf|Text files (*.txt)|*.txt";
                    openFileDialog.Multiselect = true;
                    if (openFileDialog.ShowDialog() == true)
                        //Add the files async.)
                        for (int i = 0; i < openFileDialog.FileNames.Length; i++)
                            if (!istopped)
                                AddInputFile(openFileDialog.FileNames[i]);
                        
                })) { Name = "Open file dialog thread." }.Start();
            });
            #endregion
            MoveUpCommand = new DelegateCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new DelegateCommand(MoveDown, CanMoveDown);
            RemoveCommand = new DelegateCommand(Remove, CanMerge);
        }
        #endregion

        #region Variables
        int _si = 0;
        /// <summary>
        /// The selected listbox index.
        /// </summary>
        public int SelectedIndex { get { return _si; } set { _si = value; MoveDownCommand.RaiseCanExecuteChanged(); MoveUpCommand.RaiseCanExecuteChanged(); } }
        /// <summary>
        /// A list of all the pdf files added.
        /// </summary>
        public List<PDFItem> Files { get; set; }
        #endregion

        #region Boolean
        public bool CanMerge()
        {
            return Files.Count > 0;
        }
        public bool CanMoveUp()
        {
            return CanMerge() & SelectedIndex != 0;
        }
        public bool CanMoveDown()
        {
            return CanMerge() & SelectedIndex + 1 != Files.Count;
        }
        #endregion

        #region Methods
          #region Move Up / Down
        public void MoveUp()
        {
            Move(false);
            Application.Current.Dispatcher.Invoke(new Action(() => ((MainWindow)Application.Current.MainWindow).merger.f.Items.Refresh()));
        }
        public void MoveDown()
        {
            Move(true);
            Application.Current.Dispatcher.Invoke(new Action(() => ((MainWindow)Application.Current.MainWindow).merger.f.Items.Refresh()));
        }
        private void Move (bool up)
        {
            //The old index
            int indexold = SelectedIndex;
            //The selected item
            PDFItem dataItem = Files[SelectedIndex];
            int index = SelectedIndex;
            if (!up) index--;
            if (up) index++;
            Files.Remove(dataItem);
            Files.Insert(index, dataItem);
            SelectedIndex = 0;
            SelectedIndex = index;
            MoveDownCommand.RaiseCanExecuteChanged();
            MoveUpCommand.RaiseCanExecuteChanged();
        }
        #endregion

          #region Misc
        /// <summary>
        /// Shows a password box for a pdf
        /// </summary>
        /// <param name="file">The path of the pdf</param>
        private async void ShowPasswordBox(string file)
        {
            //Show the user the password dialog.
            string result = await ((MetroWindow)Application.Current.MainWindow).ShowInputAsync("PDF not allowed", "Enter the password for " + Path.GetFileName(file) + " to open the file. Enter the password 'hack' to crack the password", new LoginDialogSettings { UsernameWatermark = "Password", PasswordWatermark = "Enter Password Again", AffirmativeButtonText = "Ok", ColorScheme = MetroDialogColorScheme.Accented });
            if (!String.IsNullOrWhiteSpace(result))
            {
                try
                {
                    //Use iTextSharp.text.pdf.PdfReader to open the pdf.
                    new PdfReader(file, System.Text.Encoding.Default.GetBytes(result)).Dispose();
                    //Add the file to the listbox as a secure string. Won't reach here if password is wrong.
                    Files.Add(new PDFItem(file, null));
                }
                catch
                {
                    //If it isn't. Ask for the password again and return
                    ShowPasswordBox(file);
                }

                //Get GCHandle
                GCHandle gchandle = GCHandle.Alloc(result, GCHandleType.Pinned);
                //Clear the object with junk.
                //ClearIntPtr(gchandle.AddrOfPinnedObject(), result.Length * 2 /* x2 because of unicode.*/);
                //Free GCHandle
                gchandle.Free();
                MessageBox.Show(result);
            }
        }
        /// <summary>
        /// Add a input file.
        /// </summary>
        /// <param name="file">The path of the file</param>
        private async Task AddInputFile(string file)
        {
            switch (Combiner.TestSourceFile(File.ReadAllBytes(file)))
            {
                //File is corrupt pdf.
                case Combiner.SourceTestResult.Unreadable:
                    //Tell the user the pdf is corrupt.
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => ((MetroWindow)Application.Current.MainWindow).ShowMessageAsync("The file " + Path.GetFileName(file) + " could not be opened as a PDF or image", "Some thing went wrong when opening " + Path.GetFileName(file))));
                    break;
                //File is a protected pdf.
                case Combiner.SourceTestResult.Protected:
                    PdfReader.unethicalreading = false;
                    try
                    {
                        using (PdfReader reader = new PdfReader(file))
                            if (!reader.IsOpenedWithFullPermissions)
                                Application.Current.Dispatcher.Invoke(new Action(async () =>
                                {
                                    istopped = true;
                                    if (MessageDialogResult.Affirmative == await ((MetroWindow)Application.Current.MainWindow).DonNotShowAgainDialog("The file " + " is a protected file", "Opening protected files may not be allowed by the pdf author", "Lawyer"))
                                        //That dog wants to open protected files.
                                        PdfReader.unethicalreading = true;
                                    else
                                        //Exit the method
                                        return;
                                    istopped = false;
                                }));
                    }
                    catch
                    {
                        Application.Current.Dispatcher.Invoke(new Action(() => ShowPasswordBox(file)));
                        return;
                    }
                    break;
                //File is a valid pdf.
                case Combiner.SourceTestResult.Ok:
                    //Add the pdf to the ListBox.
                    Files.Add(new PDFItem(file, null));
                    Application.Current.Dispatcher.Invoke(new Action(() => ((MainWindow)Application.Current.MainWindow).merger.f.Items.Refresh()));
                    //Update Commands
                    MergeCommand.RaiseCanExecuteChanged();
                    MoveUpCommand.RaiseCanExecuteChanged();
                    MoveDownCommand.RaiseCanExecuteChanged();
                    RemoveCommand.RaiseCanExecuteChanged();
                    break;
                //File is a image (maybe not valid!).
                case Combiner.SourceTestResult.Image:
                    break;
                //File is unknown
                case Combiner.SourceTestResult.Unknown:
                    //Show a metro dialog
                    try
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => ((MetroWindow)Application.Current.MainWindow).ShowMessageAsync("Invalid format", "The file you selected is not a supported format. More supported formats coming soon.")));
                    }
                    catch { }
                    break;
            }
            //Update Commands
            MergeCommand.RaiseCanExecuteChanged();
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
            RemoveCommand.RaiseCanExecuteChanged();
            Application.Current.Dispatcher.Invoke(new Action(() => ((MainWindow)Application.Current.MainWindow).merger.f.Items.Refresh()));
        }
        #endregion

          #region Merging
        public void Remove()
        {
            Try(() =>Files.RemoveAt(SelectedIndex));
            RemoveCommand.RaiseCanExecuteChanged();
            Application.Current.Dispatcher.Invoke(new Action(() => ((MainWindow)Application.Current.MainWindow).merger.f.Items.Refresh()));
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private static void Try(Action m){try{m();}catch{}}
        public void Save()
        {
            //To get the button click animation to show. We need to open the Microsoft.Win32.SaveFileDialog in a new thread.
            new System.Threading.Thread(new System.Threading.ThreadStart(async () =>
            {
                //Initailize the open file dialog and title.
                SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog() { Title = Files.Count > 1 ? String.Format("Merging {0} File(s)", Files.Count) : String.Format("Converting {0}", Path.GetFileName(Files[0].path)) };
                //If user clicks ok.
                if (saveFileDialog.ShowDialog() == true)
                    using (Combiner comb = new Combiner())
                    {
                        comb.Output = saveFileDialog.FileName;
                        comb.Password = Files.Cast<PDFItem>().Any(x => x.password != null) ? MessageDialogResult.Affirmative == await ((MetroWindow)Application.Current.MainWindow).ShowMessageAsync("Password protected pdf", "One or more of the pdfs you are merging are password protected. Do you want to protect the merged pdf with a pasword?", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings() { AffirmativeButtonText = "Yes", NegativeButtonText = "No" }) ? (await ((MetroWindow)Application.Current.MainWindow).ShowInputAsync("Enter the password for the merged pdf", "Password contain anything")).ToSecureString() : null : null;
                        foreach (var item in Files)
                            comb.AddFile(File.ReadAllBytes(item.path), null);

                    }
                if (!String.IsNullOrEmpty(saveFileDialog.FileName))
                    System.Diagnostics.Process.Start(saveFileDialog.FileName);
            })).Start();
        }
        #endregion
        #endregion
    }
}
