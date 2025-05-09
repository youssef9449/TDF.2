using Microsoft.Maui.Controls;
using TDFMAUI.ViewModels;
using TDFShared.DTOs.Requests;
using System.Threading.Tasks;
using TDFMAUI.Helpers;

namespace TDFMAUI.Pages;

public partial class RequestsPage : ContentPage
{
    private bool _isInitialized = false;

    public RequestsPage(RequestsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        SizeChanged += OnPageSizeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (!_isInitialized && BindingContext is RequestsViewModel vm)
        {            
            await vm.InitializeAsync();
            _isInitialized = true;
        }
        AdjustCollectionViewLayout();
    }

    private void OnPageSizeChanged(object sender, System.EventArgs e)
    {
        AdjustCollectionViewLayout();
    }

    private void AdjustCollectionViewLayout()
    {
        if (RequestsCollectionView == null) return;

        int optimalColumns = DeviceHelper.GetOptimalColumnCount();

        if (optimalColumns > 1)
        {
            RequestsCollectionView.ItemsLayout = new GridItemsLayout(optimalColumns, ItemsLayoutOrientation.Vertical)
            {
                VerticalItemSpacing = 10,
                HorizontalItemSpacing = 10
            };
        }
        else
        {
            RequestsCollectionView.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
            {
                ItemSpacing = 5
            };
        }
    }
}