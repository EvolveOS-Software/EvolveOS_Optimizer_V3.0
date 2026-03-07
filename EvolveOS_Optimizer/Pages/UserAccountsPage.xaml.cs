using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Animation;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class UserAccountsPage : Page
    {
        public UserAccountsViewModel ViewModel { get; }

        public UserAccountsPage()
        {
            this.InitializeComponent();

            ViewModel = new UserAccountsViewModel();

            this.DataContext = ViewModel;
        }

        private void FormUsername_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
            }
        }

        private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                FactoryAnimation.AnimateCardScale(card, 1.01);

                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
            }
        }

        private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border card)
            {
                FactoryAnimation.AnimateCardScale(card, 1.0);

                card.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            }
        }

        private void UserGridView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var clickedElement = e.OriginalSource as FrameworkElement;

            if (clickedElement?.DataContext is not UserAccount)
            {
                ViewModel.SelectedUser = null;
            }
        }
    }
}