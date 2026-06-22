using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using PicDispatch.Models;
using PicDispatch.Services;
using PicDispatch.ViewModels;

namespace PicDispatch
{
    public partial class TrashWindow : Window
    {
        private readonly TrashViewModel _viewModel;

        public TrashWindow(IEnumerable<TargetFolder> targets, MainViewModel mainViewModel, ImageLoaderService imageLoaderService)
        {
            InitializeComponent();
            _viewModel = new TrashViewModel(mainViewModel.FileActions, mainViewModel, imageLoaderService, targets);
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowEffects.ApplyWindows11Chrome(this);
            _viewModel.Refresh();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.Dispose();
        }
    }
}
