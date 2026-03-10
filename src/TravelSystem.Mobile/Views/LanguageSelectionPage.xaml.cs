using Microsoft.Maui.Controls;
using TravelSystem.Shared.Models;
using System.Diagnostics;
using TravelSystem.Mobile.Services;

namespace TravelSystem.Mobile.Views;

public partial class LanguageSelectionPage : ContentPage
{
    private string _selectedLanguage = "vi";
    private readonly DatabaseService _dbService;

    public LanguageSelectionPage()
    {
        InitializeComponent();
        // Nạp thủ công Service từ DI Container vì RegisterRoute của MAUI không hỗ trợ Constructor chứa tham số
        _dbService = IPlatformApplication.Current?.Services.GetService<DatabaseService>();
    }

    public LanguageSelectionPage(DatabaseService dbService)
    {
        InitializeComponent();
        _dbService = dbService;
    }

    private void OnLanguageSelected(object sender, TappedEventArgs e)
    {
        var langCode = e.Parameter as string;
        if (langCode == null) return;

        _selectedLanguage = langCode;

        // Reset tất cả các Frame về trạng thái chưa chọn
        foreach (var view in LanguageList.Children)
        {
            if (view is Frame frame)
            {
                // Frame border
                frame.BorderColor = Colors.Transparent;
                
                // Mờ khung Pháp nếu chưa code
                if (frame.Opacity < 1) continue; 
                
                frame.BackgroundColor = Colors.White;

                // Các element bên trong Grid
                if (frame.Content is Grid grid)
                {
                    // Lấy VStack chứa Text Tiếng Việt / Tiếng Anh
                    if (grid.Children[1] is VerticalStackLayout vStack)
                    {
                        if (vStack.Children[0] is Label titleLabel)
                            titleLabel.TextColor = Color.FromArgb("#1C1C1E");
                        if (vStack.Children[1] is Label subLabel)
                            subLabel.TextColor = Color.FromArgb("#8E8E93");
                    }

                    // Lấy Box đánh dấu (ở vị trí cột số 2)
                    foreach (var child in grid.Children)
                    {
                        if (child is Frame checkFrame && Grid.GetColumn((BindableObject)child) == 2)
                        {
                            checkFrame.BorderColor = Color.FromArgb("#E5E5EA");
                            checkFrame.Content = null; // Xoá chấm tròn
                        }
                    }
                }
            }
        }

        // Highlight Frame vừa được chọn
        var selectedFrame = (Frame)sender;
        selectedFrame.BorderColor = Color.FromArgb("#FF4B4B");
        selectedFrame.BackgroundColor = Colors.White; // UI mới: vẫn màu trắng, chỉ đổi viền
        selectedFrame.Opacity = 1;

        if (selectedFrame.Content is Grid selectedGrid)
        {
            if (selectedGrid.Children[1] is VerticalStackLayout vStack)
            {
                if (vStack.Children[0] is Label titleLabel)
                    titleLabel.TextColor = Color.FromArgb("#FF4B4B");
                if (vStack.Children[1] is Label subLabel)
                    subLabel.TextColor = Color.FromArgb("#8E8E93");
            }

            foreach (var child in selectedGrid.Children)
            {
                if (child is Frame checkFrame && Grid.GetColumn((BindableObject)child) == 2)
                {
                    checkFrame.BorderColor = Color.FromArgb("#FF4B4B");
                    // Vẽ chấm tròn bên trong
                    checkFrame.Content = new Microsoft.Maui.Controls.Shapes.Ellipse
                    {
                        Fill = new SolidColorBrush(Color.FromArgb("#FF4B4B")),
                        Margin = new Thickness(4)
                    };
                }
            }
        }
    }

    private async void OnGetStartedClicked(object sender, EventArgs e)
    {
        // 1. Lưu ngôn ngữ cào DB (AppSetting)
        if (_dbService != null)
        {
            await _dbService.SetSettingAsync("Language", _selectedLanguage);
        }

        // 2. Chuyển sang màn hình chính AppShell (Dùng File Định tuyến Shell chuẩn xác của MAUI)
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.GoToAsync("//MainPage");
        });
    }
}
