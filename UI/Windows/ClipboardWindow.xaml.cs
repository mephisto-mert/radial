using System;
using System.Windows;
using System.Windows.Input;
using RadialLauncher.Models;
using RadialLauncher.Services.Clipboard;
using RadialLauncher.Services.Localization;

namespace RadialLauncher.UI.Windows
{
    public partial class ClipboardWindow : Window
    {
        private readonly IClipboardService _clipboardService;

        public ClipboardWindow(IClipboardService clipboardService)
        {
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            InitializeComponent();
            ApplyLocalization();
            RefreshList();
        }

        private void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;
            bool isTr = loc.CurrentLanguage == "tr";

            Title = isTr ? "Pano Geçmişi" : "Clipboard History";
            TxtHeaderTitle.Text = isTr ? "📋 Pano Geçmişi" : "📋 Clipboard History";
            TxtHeaderSubtitle.Text = isTr ? "Panoya tekrar almak için bir öğeye çift tıklayın." : "Double-click an item to copy it back to your active clipboard.";
            ClearBtn.Content = isTr ? "Tümünü Temizle" : "Clear All";
            RemoveBtn.Content = isTr ? "Kaldır" : "Remove";
            CopyBtn.Content = isTr ? "Kopyala" : "Copy";
            CloseBtn.Content = isTr ? "Kapat" : "Close";
        }

        private void RefreshList()
        {
            ClipList.ItemsSource = _clipboardService.GetRecentHistory(50);
        }

        private ClipboardItem? Selected() => ClipList.SelectedItem as ClipboardItem;

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            var item = Selected();
            if (item == null)
            {
                ClipStatusText.Text = LocalizationService.Instance.CurrentLanguage == "tr" ? "Önce bir öğe seçin." : "Please select an item first.";
                return;
            }
            _clipboardService.CopyToClipboard(item.Text);
            ClipStatusText.Text = LocalizationService.Instance.CurrentLanguage == "tr" ? "Panoya kopyalandı!" : "Copied to clipboard!";
            RefreshList();
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            int idx = ClipList.SelectedIndex;
            if (idx < 0)
            {
                ClipStatusText.Text = LocalizationService.Instance.CurrentLanguage == "tr" ? "Önce bir öğe seçin." : "Please select an item first.";
                return;
            }
            _clipboardService.RemoveAt(idx);
            ClipStatusText.Text = LocalizationService.Instance.CurrentLanguage == "tr" ? "Öğe kaldırıldı." : "Item removed from history.";
            RefreshList();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            _clipboardService.ClearHistory();
            ClipStatusText.Text = LocalizationService.Instance.CurrentLanguage == "tr" ? "Pano geçmişi temizlendi." : "Clipboard history cleared.";
            RefreshList();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void ClipList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = Selected();
            if (item == null) return;
            _clipboardService.CopyToClipboard(item.Text);
            ClipStatusText.Text = LocalizationService.Instance.CurrentLanguage == "tr" ? "Panoya kopyalandı!" : "Copied to clipboard!";
            RefreshList();
        }
    }
}
