﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;

namespace ParsecVDisplay
{
    public partial class MainWindow : Window
    {
        public static bool IsMenuOpen;

        public MainWindow()
        {
            InitializeComponent();
            xAppName.Content += $" v{App.VERSION}";

            // prevent frame history
            xFrame.Navigating += (_, e) => { e.Cancel = e.NavigationMode != NavigationMode.New; };
            xFrame.Navigated += (_, e) => { xFrame.NavigationService.RemoveBackEntry(); };

            xDisplays.Children.Clear();
            xNoDisplay.Visibility = Visibility.Hidden;

            // setup tray context menu
            ContextMenu.DataContext = this;
            ContextMenu.Resources = App.Current.Resources;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            Helper.EnableDropShadow(hwnd);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;

            if (xFrame.Content != null)
            {
                xFrame.Visibility = Visibility.Hidden;
                xFrame.Content = null;
                xDisplays.Visibility = Visibility.Visible;
                xButtons.Visibility = Visibility.Visible;
            }
            else
            {
                this.Hide();
            }
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= Window_Loaded;

            Tray.Init(this, ContextMenu);
            ContextMenu = null;

            if (App.Silent)
                Hide();

            CheckUpdate(null, null);

            var defaultLang = Config.Language;
            foreach (var item in App.Languages)
            {
                var mi = new MenuItem
                {
                    Header = item,
                    IsCheckable = true,
                    IsChecked = item == defaultLang
                };

                mi.Click += delegate
                {
                    foreach (MenuItem item2 in xLanguageMenu.Items)
                        item2.IsChecked = false;

                    mi.IsChecked = true;
                    App.SetLanguage(mi.Header.ToString());
                };

                xLanguageMenu.Items.Add(mi);
            }

            ParsecVDD.DisplayChanged += DisplayChanged;
            ParsecVDD.Invalidate();
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            ParsecVDD.DisplayChanged -= DisplayChanged;
            Tray.Uninit();
        }

        private void DisplayChanged(List<Display> displays, bool noMonitors)
        {
            xDisplays.Children.Clear();
            xNoDisplay.Visibility = displays.Count <= 0 ? Visibility.Visible : Visibility.Hidden;

            foreach (var display in displays)
            {
                var item = new Components.DisplayItem(display);
                xDisplays.Children.Add(item);
            }

            xAdd.IsEnabled = true;

            if (noMonitors && Config.FallbackDisplay)
            {
                AddDisplay(null, EventArgs.Empty);
            }
        }

        private void AddDisplay(object sender, EventArgs e)
        {
            if (ParsecVDD.DisplayCount >= ParsecVDD.MAX_DISPLAYS)
            {
                MessageBox.Show(this, App.GetTranslation("t_msg_exceeded_display_limit", ParsecVDD.MAX_DISPLAYS),
                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                ParsecVDD.AddDisplay();
                xAdd.IsEnabled = false;
            }
        }

        private void RemoveLastDisplay(object sender, EventArgs e)
        {
            xAdd.IsEnabled = false;
            ParsecVDD.RemoveLastDisplay();
        }

        private void OpenCustom(object sender, EventArgs e)
        {
            xDisplays.Visibility = Visibility.Hidden;
            xButtons.Visibility = Visibility.Hidden;
            xFrame.Content = new Components.CustomPage();
            xFrame.Visibility = Visibility.Visible;
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            Helper.ShellExec("ms-settings:display");
        }

        private void SyncSettings(object sender, EventArgs e)
        {
            xAdd.IsEnabled = false;
            xDisplays.Children.Clear();

            ParsecVDD.Invalidate();
        }

        private void QueryStatus(object sender, EventArgs e)
        {
            if (e is MouseEventArgs mbe)
                mbe.Handled = true;

            Tray.ShowApp();

            var status = ParsecVDD.QueryStatus();
            var version = ParsecVDD.QueryVersion();

            MessageBox.Show(this, $"Parsec Virtual Display v{version}\n" +
                $"{App.GetTranslation("t_msg_driver_status")}: {status}",
                App.NAME, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExitApp(object sender, EventArgs e)
        {
            if (ParsecVDD.DisplayCount > 0)
                if (MessageBox.Show(this, App.GetTranslation("t_msg_prompt_leave_all"),
                    App.NAME, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

            Tray.Uninit();
            Application.Current.Shutdown();
        }

        private void OpenRepoLink(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Helper.OpenLink($"https://github.com/{App.GITHUB_REPO}");
        }

        private async void CheckUpdate(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = null;
            if (sender is MenuItem)
            {
                menuItem = sender as MenuItem;
                menuItem.IsEnabled = false;
            }

            var newVersion = await Updater.CheckUpdate();
            if (!string.IsNullOrEmpty(newVersion))
            {
                var ret = MessageBox.Show(this, App.GetTranslation("t_msg_update_available", newVersion),
                    App.NAME, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ret == MessageBoxResult.Yes)
                {
                    Helper.OpenLink(Updater.DOWNLOAD_URL);
                }
            }
            else if (sender != null)
            {
                MessageBox.Show(this, App.GetTranslation("t_msg_up_to_date"),
                    App.NAME, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (menuItem != null)
            {
                menuItem.IsEnabled = true;
            }
        }

        private void LanguageText_MouseEvent(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (e.LeftButton == MouseButtonState.Released)
                (sender as TextBlock).ContextMenu.IsOpen = true;
        }
    }
}