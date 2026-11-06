using Microsoft.Maui.Controls;

namespace MusicPlayer.Features.Importer;

public partial class ImportPage : ContentPage
{
    public ImportPage(Importer.ImportViewModel importViewModel)
    {
        InitializeComponent();

        BindingContext = importViewModel;
    }
}