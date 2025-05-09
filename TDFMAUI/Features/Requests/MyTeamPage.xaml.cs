using System;
using System.Collections.ObjectModel;
using TDFMAUI.Services;
using Microsoft.Maui.Controls;
using TDFMAUI.ViewModels;
using TDFShared.DTOs.Users;
using TDFMAUI.Helpers;
using System.Threading.Tasks;

namespace TDFMAUI.Pages
{
    public partial class MyTeamPage : ContentPage
    {
        private readonly MyTeamViewModel _viewModel;
        public MyTeamPage(MyTeamViewModel vm)
        {
            InitializeComponent();
            _viewModel = vm;
            BindingContext = _viewModel;
            SizeChanged += OnPageSizeChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_viewModel.LoadTeamCommand.CanExecute(null))
            {
                await _viewModel.LoadTeamCommand.ExecuteAsync(null);
            }
            AdjustCollectionViewLayout();
        }

        private void OnPageSizeChanged(object sender, EventArgs e)
        {
            AdjustCollectionViewLayout();
        }

        private void AdjustCollectionViewLayout()
        {
            if (TeamMembersCollectionView == null) return;

            int optimalColumns = DeviceHelper.GetOptimalColumnCount();

            if (optimalColumns > 1)
            {
                TeamMembersCollectionView.ItemsLayout = new GridItemsLayout(optimalColumns, ItemsLayoutOrientation.Vertical)
                {
                    VerticalItemSpacing = 10, 
                    HorizontalItemSpacing = 10 
                };
            }
            else
            {
                TeamMembersCollectionView.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
                {
                    ItemSpacing = 5 
                };
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("..?", true);
            }
            else
            {
                await Navigation.PopAsync();
            }
        }

        private async void OnRefreshing(object sender, EventArgs e)
        {
            if (_viewModel.LoadTeamCommand.CanExecute(null))
            {
                await _viewModel.LoadTeamCommand.ExecuteAsync(null);
            }
        }
    }
} 