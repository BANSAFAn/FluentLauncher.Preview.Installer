using CommunityToolkit.Mvvm.ComponentModel;
using FluentLauncher.UniversalInstaller.Utils;
using System.Windows.Controls;

namespace FluentLauncher.UniversalInstaller.Pages;

public partial class ComponentPage : Page
{
    public ComponentPage()
    {
        InitializeComponent();
    }

    private void TreeViewItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        tipsText.Visibility = System.Windows.Visibility.Visible;
        TreeViewItem treeViewItem = sender as TreeViewItem;

        tipsText.Text = treeViewItem.Tag.ToString();
    }

    private void TreeViewItem_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        tipsText.Visibility = System.Windows.Visibility.Collapsed;
    }
}

partial class ComponentPageVM : ObservableRecipient, IBaseStepViewModel
{
    public bool CanNext => true;

    public bool CanBack => true;

    public bool? RootChecked => ConnectXExtensionChecked && DotNet9Checked ? true : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RootChecked))]
    public partial bool ConnectXExtensionChecked { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RootChecked))]
    public partial bool DotNet9Checked { get; set; }
}
