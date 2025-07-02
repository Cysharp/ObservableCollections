namespace WinUI3App;

public sealed partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private MainPageViewModel ViewModel { get; } = new MainPageViewModel(new SampleService());
}
