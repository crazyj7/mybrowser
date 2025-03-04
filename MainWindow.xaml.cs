using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Media;

namespace WpfBrowser
{
    public partial class MainWindow : Window
    {
        private List<Bookmark> bookmarks = new List<Bookmark>();
        private string homeUrl = "https://www.google.com";  // 기본값 설정
        private readonly string bookmarksPath;
        private readonly string settingsPath;
        private ObservableCollection<BrowserTab> tabs;
        private bool isBookmarksPanelVisible = true;
        private BrowserTab? tabToClose = null;  // 클래스 멤버 변수로 추가
        private BrowserSettings settings = new BrowserSettings();  // 설정 객체 추가

        public MainWindow()
        {
            // 파일 경로 초기화
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WpfBrowser"
            );
            bookmarksPath = Path.Combine(appDataPath, "bookmarks.json");
            settingsPath = Path.Combine(appDataPath, "settings.json");

            InitializeComponent();
            tabs = new ObservableCollection<BrowserTab>();
            browserTabs.ItemsSource = tabs;
            
            // 설정 불러오기
            LoadSettings();
            LoadBookmarks();
            
            // 윈도우 크기와 위치 복원
            if (this.WindowState == WindowState.Normal)
            {
                this.Width = settings.WindowWidth;
                this.Height = settings.WindowHeight;
                this.Left = settings.WindowLeft;
                this.Top = settings.WindowTop;
            }
            this.WindowState = settings.WindowState;
            
            // 저장된 탭 복원
            if (settings.OpenTabs.Count > 0)
            {
                foreach (var tabInfo in settings.OpenTabs)
                {
                    AddNewTab(tabInfo.Url, false, tabInfo.Title);  // 타이틀도 함께 전달
                }
            }
            else
            {
                // 저장된 탭이 없으면 기본 탭 추가
                AddNewTab(homeUrl);
            }

            // 탭 선택 변경 이벤트 핸들러 추가
            browserTabs.SelectionChanged += BrowserTabs_SelectionChanged;
        }

        private void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 탭이 닫히는 중이 아닐 때만 URL 업데이트
            if (browserTabs.SelectedItem is BrowserTab selectedTab && selectedTab != tabToClose)
            {
                txtUrl.Text = selectedTab.Url;
                txtStatus.Text = "로딩 완료";
            }
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            txtStatus.Text = "페이지 로딩 중...";
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (sender is WebView2 webView)
            {
                var tab = webView.DataContext as BrowserTab;
                if (tab != null && tab == browserTabs.SelectedItem)  // 현재 선택된 탭인 경우에만 URL 업데이트
                {
                    tab.Title = webView.CoreWebView2.DocumentTitle;
                    tab.Url = webView.CoreWebView2.Source;
                    txtUrl.Text = tab.Url;
                    txtStatus.Text = "로딩 완료";
                }
                else if (tab != null)  // 선택되지 않은 탭은 URL만 업데이트
                {
                    tab.Title = webView.CoreWebView2.DocumentTitle;
                    tab.Url = webView.CoreWebView2.Source;
                }
            }
        }

        public void AddNewTab(string? url = null, bool isEmptyTab = false, string? title = null)
        {
            var tab = new BrowserTab
            {
                Title = title ?? "새 탭",
                Url = isEmptyTab ? "about:blank" : (url ?? homeUrl)
            };
            tabs.Add(tab);
            browserTabs.SelectedIndex = tabs.Count - 1;
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is BrowserTab tab)
            {
                CloseTab(tab);
            }
        }

        private void CloseTab(BrowserTab tab)
        {
            try
            {
                int currentIndex = browserTabs.SelectedIndex;
                int tabIndex = tabs.IndexOf(tab);

                // 탭 제거 전에 다음 탭 선택
                if (tabs.Count > 1)  // 탭이 2개 이상일 때만 선택 변경
                {
                    if (currentIndex == tabIndex)  // 현재 선택된 탭을 닫는 경우
                    {
                        // 마지막 탭인 경우 이전 탭 선택
                        if (tabIndex == tabs.Count - 1)
                        {
                            browserTabs.SelectedIndex = tabIndex - 1;
                        }
                        // 그 외의 경우 다음 탭 선택
                        else
                        {
                            browserTabs.SelectedIndex = tabIndex + 1;
                        }
                    }
                }

                // 탭 제거
                tabs.Remove(tab);

                // 리소스 정리 (탭 제거 후에 수행)
                tab.Dispose();

                // 모든 탭이 닫힌 경우 새 탭 추가
                if (tabs.Count == 0)
                {
                    AddNewTab(null, true);
                }
            }
            catch (Exception)
            {
                // 오류 발생 시 처리
                if (tabs.Contains(tab))
                {
                    tabs.Remove(tab);
                }
                
                if (tabs.Count == 0)
                {
                    AddNewTab(null, true);
                }
            }
        }

        private void AddTab_Click(object sender, RoutedEventArgs e)
        {
            AddNewTab(null, true);  // 빈 탭으로 생성
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWebView?.CanGoBack == true)
                CurrentWebView.GoBack();
        }

        private void btnForward_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWebView?.CanGoForward == true)
                CurrentWebView.GoForward();
        }

        private void btnHome_Click(object sender, RoutedEventArgs e)
        {
            if (browserTabs.SelectedItem is BrowserTab currentTab)
            {
                currentTab.Url = homeUrl;
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            CurrentWebView?.Reload();
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl(txtUrl.Text);
        }

        private void txtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToUrl(txtUrl.Text);
            }
        }

        private void NavigateToUrl(string url)
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            
            if (browserTabs.SelectedItem is BrowserTab currentTab)
            {
                currentTab.Url = url;
            }
        }

        private void LoadBookmarks()
        {
            try
            {
                // 디렉토리가 없으면 생성
                Directory.CreateDirectory(Path.GetDirectoryName(bookmarksPath)!);

                if (File.Exists(bookmarksPath))
                {
                    string jsonString = File.ReadAllText(bookmarksPath);
                    var loadedBookmarks = JsonSerializer.Deserialize<List<Bookmark>>(jsonString);
                    if (loadedBookmarks != null)
                    {
                        bookmarks = loadedBookmarks;
                        UpdateBookmarksList();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"즐겨찾기를 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveBookmarks()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(bookmarks, options);
                File.WriteAllText(bookmarksPath, jsonString);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"즐겨찾기를 저장하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (browserTabs.SelectedItem is BrowserTab currentTab)
            {
                var bookmark = new Bookmark
                {
                    Title = currentTab.Title,
                    Url = currentTab.Url
                };
                
                bookmarks.Add(bookmark);
                UpdateBookmarksList();
                SaveBookmarks();
            }
        }

        // 즐겨찾기 삭제 기능 추가
        private void DeleteBookmark(int index)
        {
            if (index >= 0 && index < bookmarks.Count)
            {
                bookmarks.RemoveAt(index);
                UpdateBookmarksList();
                SaveBookmarks();
            }
        }

        // 우클릭 메뉴를 위한 컨텍스트 메뉴 추가
        private void lstBookmarks_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = (sender as ListBox)?.SelectedItem;
            if (item != null)
            {
                ContextMenu contextMenu = new ContextMenu();
                
                // 새 탭에서 열기 메뉴 아이템
                MenuItem openNewTab = new MenuItem();
                openNewTab.Header = "새 탭에서 열기";
                openNewTab.Click += (s, args) => OpenInNewTab(lstBookmarks.SelectedIndex);
                contextMenu.Items.Add(openNewTab);
                
                contextMenu.Items.Add(new Separator());
                
                MenuItem deleteItem = new MenuItem();
                deleteItem.Header = "삭제";
                deleteItem.Click += (s, args) => DeleteBookmark(lstBookmarks.SelectedIndex);
                contextMenu.Items.Add(deleteItem);
                
                lstBookmarks.ContextMenu = contextMenu;
            }
        }

        // 새 창에서 북마크 열기
        private void OpenInNewTab(int index)
        {
            if (index >= 0 && index < bookmarks.Count)
            {
                var bookmark = bookmarks[index];
                AddNewTab(bookmark.Url);
            }
        }

        private void UpdateBookmarksList()
        {
            lstBookmarks.Items.Clear();
            foreach (var bookmark in bookmarks)
            {
                lstBookmarks.Items.Add(bookmark.Title);
            }
        }

        private void lstBookmarks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int selectedIndex = lstBookmarks.SelectedIndex;
            if (selectedIndex >= 0 && browserTabs.SelectedItem is BrowserTab currentTab)
            {
                currentTab.Url = bookmarks[selectedIndex].Url;
            }
        }

        // 현재 활성화된 WebView2 가져오기
        private WebView2? CurrentWebView => (browserTabs.SelectedItem as BrowserTab)?.WebView;

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string jsonString = File.ReadAllText(settingsPath);
                    var loadedSettings = JsonSerializer.Deserialize<BrowserSettings>(jsonString);
                    if (loadedSettings != null)
                    {
                        // 전체 설정 객체를 복사
                        settings = loadedSettings;
                        
                        // 홈페이지 URL 설정
                        homeUrl = settings.HomeUrl;

                        // OpenTabs가 null이면 새 리스트 생성
                        if (settings.OpenTabs == null)
                        {
                            settings.OpenTabs = new List<TabInfo>();
                        }
                    }
                }
                else
                {
                    // 설정 파일이 없는 경우 기본값 설정
                    settings = new BrowserSettings
                    {
                        HomeUrl = "https://www.google.com",
                        OpenTabs = new List<TabInfo>()
                    };
                    homeUrl = settings.HomeUrl;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정을 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                settings = new BrowserSettings
                {
                    HomeUrl = "https://www.google.com",
                    OpenTabs = new List<TabInfo>()
                };
                homeUrl = settings.HomeUrl;
            }
        }

        private void SaveSettings()
        {
            try
            {
                // 현재 윈도우 상태 저장
                settings.WindowWidth = this.Width;
                settings.WindowHeight = this.Height;
                settings.WindowLeft = this.Left;
                settings.WindowTop = this.Top;
                settings.WindowState = this.WindowState;
                settings.HomeUrl = homeUrl;

                // 현재 열린 탭 정보 저장
                settings.OpenTabs.Clear();
                foreach (BrowserTab tab in tabs)
                {
                    settings.OpenTabs.Add(new TabInfo 
                    { 
                        Title = tab.Title, 
                        Url = tab.Url 
                    });
                }

                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsPath, jsonString);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정을 저장하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 홈페이지로 설정 메뉴 클릭 이벤트
        private void SetAsHomepage_Click(object sender, RoutedEventArgs e)
        {
            if (browserTabs.SelectedItem is BrowserTab currentTab)
            {
                homeUrl = currentTab.Url;
                SaveSettings();
                MessageBox.Show("현재 페이지가 홈페이지로 설정되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 먼저 설정 저장
                SaveSettings();

                // 그 다음 탭 정리
                foreach (var tab in tabs.ToList())
                {
                    try
                    {
                        tab.Dispose();
                    }
                    catch (Exception)
                    {
                        // 개별 탭 정리 중 발생하는 오류 무시
                    }
                }
                tabs.Clear();
            }
            catch (Exception)
            {
                // 프로그램 종료 시 발생하는 오류 무시
            }
            base.OnClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (e.Key == Key.F1)
            {
                var aboutWindow = new AboutWindow
                {
                    Owner = this
                };
                aboutWindow.ShowDialog();
            }
            else if (e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (browserTabs.SelectedItem is BrowserTab currentTab)
                {
                    CloseTab(currentTab);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleBookmarksPanel();
                e.Handled = true;
            }
        }

        // 탭 영역 더블클릭 이벤트 처리
        private void browserTabs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // TabItem 헤더 영역이 아닌 빈 공간을 더블클릭한 경우에만 새 탭 열기
            if (!(e.OriginalSource is TextBlock) && !(e.OriginalSource is Button))
            {
                AddNewTab(null, true);  // 빈 탭으로 생성
                e.Handled = true;
            }
        }

        // 탭 제목 더블클릭 이벤트 처리
        private void TabTitle_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (browserTabs.SelectedItem is BrowserTab tab)
            {
                CloseTab(tab);
                e.Handled = true;
            }
        }

        private void btnToggleBookmarks_Click(object sender, RoutedEventArgs e)
        {
            ToggleBookmarksPanel();
        }

        private void ToggleBookmarksPanel()
        {
            isBookmarksPanelVisible = !isBookmarksPanelVisible;
            bookmarksColumn.Width = new GridLength(isBookmarksPanelVisible ? 200 : 0);
        }

        private void lstBookmarks_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)  // 더블클릭이 아닐 때만 드래그 시작
            {
                ListBoxItem? listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                
                if (listBoxItem != null)
                {
                    int index = lstBookmarks.ItemContainerGenerator.IndexFromContainer(listBoxItem);
                    if (index >= 0)
                    {
                        DragDrop.DoDragDrop(listBoxItem,
                                           index,  // 인덱스를 전달
                                           DragDropEffects.Move);
                    }
                }
            }
        }

        private void lstBookmarks_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListBox listBox && e.Data.GetDataPresent(typeof(int)))
            {
                int dragIndex = (int)e.Data.GetData(typeof(int));
                Point dropPoint = e.GetPosition(listBox);
                int dropIndex = -1;

                // 드롭 위치의 아이템 찾기
                for (int i = 0; i < listBox.Items.Count; i++)
                {
                    ListBoxItem? item = listBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                    if (item != null)
                    {
                        if (dropPoint.Y < item.TranslatePoint(new Point(0, item.ActualHeight), listBox).Y)
                        {
                            dropIndex = i;
                            break;
                        }
                    }
                }

                if (dropIndex == -1)
                {
                    dropIndex = listBox.Items.Count;
                }

                if (dragIndex >= 0 && dropIndex != dragIndex)
                {
                    // 북마크 리스트에서 아이템 이동
                    var item = bookmarks[dragIndex];
                    bookmarks.RemoveAt(dragIndex);
                    
                    // 드롭 위치가 드래그 위치보다 뒤에 있는 경우 조정
                    if (dropIndex > dragIndex)
                    {
                        dropIndex--;
                    }
                    
                    bookmarks.Insert(dropIndex, item);
                    
                    // UI 업데이트 및 설정 저장
                    UpdateBookmarksList();
                    SaveBookmarks();
                }
            }
        }

        private void lstBookmarks_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(int)))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        // 헬퍼 메서드: 부모 컨트롤 찾기
        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T t)
                {
                    return t;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }

    public class Bookmark
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class TabInfo
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    // 설정 클래스 추가
    public class BrowserSettings
    {
        public string? LastUrl { get; set; }
        public string HomeUrl { get; set; } = "https://www.google.com";
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public List<TabInfo> OpenTabs { get; set; } = new List<TabInfo>();  // 열린 탭 정보 저장
    }

    public class BrowserTab : INotifyPropertyChanged, IDisposable
    {
        private string title = "새 탭";
        private string url = "";
        private WebView2 webView;
        private bool isInitialized = false;
        private string pendingUrl = "";
        private bool disposed = false;

        public BrowserTab()
        {
            webView = new WebView2();
            webView.NavigationCompleted += WebView_NavigationCompleted;
            webView.NavigationStarting += WebView_NavigationStarting;
            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                isInitialized = true;
                // 초기화 직후 명시적으로 URL 설정
                if (!string.IsNullOrEmpty(url))
                {
                    webView.CoreWebView2.Navigate(url);
                }
                else if (!string.IsNullOrEmpty(pendingUrl))
                {
                    webView.CoreWebView2.Navigate(pendingUrl);
                    pendingUrl = "";
                }
            }
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            NavigationStarting?.Invoke(this, e);
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (webView.CoreWebView2 != null)
            {
                Title = webView.CoreWebView2.DocumentTitle;
                url = webView.CoreWebView2.Source;  // url 필드를 직접 업데이트
                OnPropertyChanged(nameof(Url));
                NavigationCompleted?.Invoke(this, e);
            }
        }

        public string Title
        {
            get => title;
            set
            {
                title = value ?? "";
                OnPropertyChanged(nameof(Title));
            }
        }

        public string Url
        {
            get => url;
            set
            {
                if (url == value) return;  // 같은 URL이면 무시
                
                url = value ?? "";
                OnPropertyChanged(nameof(Url));
                
                if (string.IsNullOrEmpty(url)) return;

                if (isInitialized && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Navigate(url);
                }
                else
                {
                    pendingUrl = url;
                }
            }
        }

        public WebView2 WebView => webView;

        public event EventHandler<CoreWebView2NavigationStartingEventArgs>? NavigationStarting;
        public event EventHandler<CoreWebView2NavigationCompletedEventArgs>? NavigationCompleted;
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 이벤트 핸들러 제거
                    webView.NavigationCompleted -= WebView_NavigationCompleted;
                    webView.NavigationStarting -= WebView_NavigationStarting;
                    webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
                    NavigationStarting = null;
                    NavigationCompleted = null;
                    PropertyChanged = null;

                    // WebView2 리소스 정리
                    if (webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Stop();
                    }
                    webView.Dispose();
                }
                disposed = true;
            }
        }

        ~BrowserTab()
        {
            Dispose(false);
        }
    }
} 