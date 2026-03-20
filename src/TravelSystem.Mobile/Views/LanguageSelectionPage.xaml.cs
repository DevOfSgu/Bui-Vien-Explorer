using TravelSystem.Mobile.ViewModels;

namespace TravelSystem.Mobile.Views;

public partial class LanguageSelectionPage : ContentPage
{
    public LanguageSelectionViewModel ViewModel { get; }

    public LanguageSelectionPage(LanguageSelectionViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        BindingContext = viewModel;

        Shell.SetTabBarIsVisible(this, false);
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior   // ← thêm
        {
            IsVisible = false,
            IsEnabled = false
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, false);
    }

    private void OnLanguageSelected(object sender, TappedEventArgs e)
    {
        var langCode = e.Parameter as string;
        if (langCode == null || sender is not Border selectedBorder) return;

        // 1. Đồng bộ dữ liệu
        ViewModel.SelectLanguageCommand.Execute(langCode);

        // 2. Reset toàn bộ danh sách về trạng thái mặc định
        foreach (var child in LanguageList.Children)
        {
            if (child is Border border)
            {
                ResetBorderStyle(border);
            }
        }

        // 3. Highlight Border vừa chọn
        ApplySelectedStyle(selectedBorder);
    }

    // Hàm phụ để Reset (Giúp code chính sạch hơn)
    private void ResetBorderStyle(Border border)
    {
        var grayColor = Color.FromArgb("#8E8E93");
        var darkColor = Color.FromArgb("#1C1C1E");

        border.Stroke = Colors.Transparent;
        border.StrokeThickness = 0;

        // Dùng LINQ để tìm nhanh các thành phần
        var labels = border.GetVisualTreeDescendants().OfType<Label>();
        foreach (var lbl in labels)
        {
            // Chữ chính màu đậm, chữ phụ màu xám
            lbl.TextColor = lbl.FontSize > 14 ? darkColor : grayColor;
        }

        var ellipse = border.GetVisualTreeDescendants().OfType<Microsoft.Maui.Controls.Shapes.Ellipse>().FirstOrDefault();
        if (ellipse != null) {
            ellipse.IsVisible = false;
            if (ellipse.Parent is Border circleBorder)
            {
                circleBorder.Stroke = grayColor; // Trả viền vòng tròn về màu xám
            }
        }
            
    }

    private void ApplySelectedStyle(Border border)
    {
        var redBuiVien = Color.FromArgb("#FF4B4B");

        border.Stroke = redBuiVien;
        border.StrokeThickness = 2;

        // Đổi tất cả chữ sang đỏ
        var labels = border.GetVisualTreeDescendants().OfType<Label>();
        foreach (var lbl in labels) lbl.TextColor = redBuiVien;

        // Hiện dấu chấm tròn đỏ
        var ellipse = border.GetVisualTreeDescendants().OfType<Microsoft.Maui.Controls.Shapes.Ellipse>().FirstOrDefault();
        if (ellipse != null && border.Stroke != null)
        {
            ellipse.IsVisible = true;
            ellipse.Fill = new SolidColorBrush(redBuiVien);
            if (ellipse.Parent is Border circleBorder)
            {
                circleBorder.Stroke = redBuiVien; // Trả viền vòng tròn về màu xám
            }
        }
    }
}
