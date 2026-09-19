using System;
using System.Windows;

namespace BeamNGModManager
{
    public partial class MainWindow
    {
        private async void CatalogSortComboBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CatalogSortComboBox?.SelectedItem is not
                System.Windows.Controls.ComboBoxItem selectedItem)
            {
                return;
            }

            string mode =
                selectedItem.Tag?.ToString() ??
                "date";

            catalogSortMode =
                mode;

            if (!IsLoaded)
            {
                catalogServerOrder =
                    mode == "downloads"
                        ? "download_count"
                        : "resource_date";

                return;
            }

            if (mode == "size")
            {
                ApplyCatalogSearch();
                return;
            }

            string desiredServerOrder =
                mode == "downloads"
                    ? "download_count"
                    : "resource_date";

            if (!desiredServerOrder.Equals(
                catalogServerOrder,
                StringComparison.OrdinalIgnoreCase))
            {
                catalogServerOrder =
                    desiredServerOrder;

                await LoadCatalogAsync();
                return;
            }

            ApplyCatalogSearch();
        }
    }
}
