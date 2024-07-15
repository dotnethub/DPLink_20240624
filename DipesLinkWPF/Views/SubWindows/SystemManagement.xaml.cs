﻿using DipesLink.Languages;
using DipesLink.ViewModels;
using DipesLink.Views.Extension;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using static DipesLink.Views.Enums.ViewEnums;


namespace DipesLink.Views.SubWindows
{
    /// <summary>
    /// Interaction logic for SystemManagement
    /// </summary>
    public partial class SystemManagement : Window
    {
        public static bool IsInitializing = true;
        private bool _isStoptopAll;
        public SystemManagement(bool isStopAll)
        {
            _isStoptopAll = isStopAll;
            InitializeComponent();
            setCurrentLanguage();
        }

        private void setCurrentLanguage()
        {
            IsInitializing = true;
            try
            {
                string languageCode = ViewModelSharedValues.Settings.Language;

                if (languageCode == "en-US")
                {
                    ComboBoxLanguages.SelectedIndex = 0;
                }
                else
                { 
                    ComboBoxLanguages.SelectedIndex = 1;
                }
            }
            catch (Exception)
            {
            }
            IsInitializing = false;
        }


        private void ComboBoxLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitializing) return;
            if (!_isStoptopAll)
            {
                CusAlert.Show($"Please stop all running stations!", ImageStyleMessageBox.Warning);
                setCurrentLanguage();
                return;  // Prevent the window from closing
            }
           
            var comboBox = sender as ComboBox;
            if(comboBox == null) return;

            var selectedIndex = comboBox.SelectedIndex;
            var languageModel = new LanguageModel();
            string selectedLanguage = selectedIndex == 0 ? "en-US" : "vi-VN";

            if (isChangeLanguageAccepted())
            {
                languageModel.UpdateApplicationLanguage(selectedLanguage);
                restartLanguageSelection();
            }
            else
            {
                setCurrentLanguage();
            }
        }

        private void restartLanguageSelection()
        {
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            Application.Current.Shutdown();
        }

        private bool isChangeLanguageAccepted()
        {
            var res = CusMsgBox.Show("Do you want to change language and logout?", "Change Language", Enums.ViewEnums.ButtonStyleMessageBox.OKCancel, Enums.ViewEnums.ImageStyleMessageBox.Warning);
            return res.Result;
        }
    }
}
