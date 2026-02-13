namespace TravelSystem.Mobile.Views;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			CounterBtn.Text = $"Clicked {count} time";
		else
			CounterBtn.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}

	private async void OnTestApiClicked(object? sender, EventArgs e)
	{
		try
		{
			ApiResultLabel.Text = "Testing...";
			var apiService = new Services.ApiService();
			var result = await apiService.TestConnectionAsync();
			ApiResultLabel.Text = $"✅ Success: {result}";
		}
		catch (Exception ex)
		{
			ApiResultLabel.Text = $"❌ Error: {ex.Message}";
		}
	}
}
