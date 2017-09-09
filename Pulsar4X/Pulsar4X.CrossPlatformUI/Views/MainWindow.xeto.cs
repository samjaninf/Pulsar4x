﻿using Eto.Forms;
using Eto.Serialization.Xaml;
using Pulsar4X.ECSLib;

namespace Pulsar4X.CrossPlatformUI.Views {
    public class MainWindow : Panel {
        #region Form Controls
        protected StackLayout TopButtonBar;
        protected StackLayout adv_buttons;
        protected DropDown dd_subpulse;
        protected TabControl view_tabs;
        protected TimeControlView TimeControlV;

        #region Advance Time Buttons
        protected Button btn_time_5sec;
        protected Button btn_time_30sec;
        protected Button btn_time_2min;
        protected Button btn_time_5min;
        protected Button btn_time_20min;
        protected Button btn_time_1hr;
        protected Button btn_time_3hrs;
        protected Button btn_time_8hrs;
        protected Button btn_time_1day;
        protected Button btn_time_5day;
        protected Button btn_time_30day;
        #endregion
        #endregion

        private GameVM _gameVM;

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the MainWindow view
        /// </summary>
        public MainWindow(GameVM gameVM) {
            _gameVM = gameVM;
            DataContext = _gameVM;
            XamlReader.Load(this);
            _gameVM.PropertyChanged += GameVMPropertyChanged;
            _gameVM.TimeControl.PropertyChanged += _timeControl_PropertyChanged;
        }
        #endregion

        #region Events
        /// <summary>
        /// Enables/Disables Timing control, when game has loaded/unloaded;
        /// </summary>
        private void GameVMPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HasGame")
            {
                TimeControlV.Enabled = _gameVM.HasGame;
                TimeControlV.btnPausePlay.isPaused = _gameVM.TimeControl.IsPaused;
            }
        }

        /// <summary>
        /// Changes image of Pause/Play button when the game is paused or resumed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _timeControl_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsPaused")
            {
                TimeControlV.btnPausePlay.isPaused = _gameVM.TimeControl.IsPaused;
            }

        }
        #endregion

        /// <summary>
        /// Adds additional Views as Panels to the TabPage
        /// </summary>
        public void AddOrSelectTabPanel(string title, Container c, bool activate = true, bool force_new = false) {
            if (!force_new) {
                foreach (TabPage p in view_tabs.Pages) {
                    if (p.Text == title) {
                        if (activate) { view_tabs.SelectedPage = p; }
                        return;
                    }
                }
            }
            TabPage tp = new TabPage();
            tp.Content = c;
            tp.Text = title;
            view_tabs.Pages.Add(tp);
            if (activate) { view_tabs.SelectedPage = tp; }
        }
    }
}
