using Microsoft.UI.Xaml.Controls;
using PrivacyGuard.ViewModels;

namespace PrivacyGuard.Views;

public sealed partial class CustomProfileEditorView : UserControl
{
    public CustomProfileEditorViewModel ViewModel { get; }

    public CustomProfileEditorView(CustomProfileEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public void CommitPendingEdits()
    {
        ViewModel.Name = NameBox.Text ?? string.Empty;
    }
}
