//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml.Controls;
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Views.Popups
{
    public sealed partial class SendFilesPopup : ContentPopup, IViewWithAutocomplete, INotifyPropertyChanged, IDiffHandler<StorageMedia>
    {
        public ComposeViewModel ViewModel { get; private set; }
        public MvxObservableCollection<StorageMedia> Items { get; private set; }

        public DiffObservableCollection<StorageMedia> ItemsView { get; private set; }

        private IAutocompleteCollection _autocomplete;
        public IAutocompleteCollection Autocomplete
        {
            get => _autocomplete;
            set
            {
                if (_autocomplete != value)
                {
                    _autocomplete = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Autocomplete)));
                }
            }
        }

        private bool _photoAllowed;
        private bool _videoAllowed;
        private bool _audioAllowed;
        private bool _documentAllowed;

        public bool IsMediaAllowed
        {
            get
            {
                if (_photoAllowed && Items.Any(x => x is StoragePhoto))
                {
                    return true;
                }
                else if (_videoAllowed && Items.Any(x => x is StorageVideo))
                {
                    return true;
                }
                else if (_audioAllowed && Items.Any(x => x is StorageAudio))
                {
                    return true;
                }

                return false;
            }
        }

        private bool _editing;

        private bool _ttlAllowed;
        public bool IsTtlAvailable => _ttlAllowed && Items.Count == 1;

        public bool HasPaidMediaAllowed { get; set; }

        private bool _isMediaSelected;
        public bool IsMediaSelected
        {
            get => _isMediaSelected;
            set
            {
                if (_isMediaSelected != value)
                {
                    _isMediaSelected = value;
                    _isFilesSelected = !value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMediaSelected)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFilesSelected)));
                }
            }
        }

        private bool _isFilesSelected;
        public bool IsFilesSelected
        {
            get => _isFilesSelected;
            set
            {
                if (_isFilesSelected != value)
                {
                    _isFilesSelected = value;
                    _isMediaSelected = !value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFilesSelected)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMediaSelected)));
                }
            }
        }

        public string TitleText
        {
            get
            {
                if (IsMediaSelected)
                {
                    if (Items.All(x => x is StoragePhoto))
                    {
                        return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Photos, Items.Count));
                    }
                    else if (Items.All(x => x is StorageVideo))
                    {
                        return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Videos, Items.Count));
                    }
                    else if (Items.All(x => x is StoragePhoto or StorageVideo))
                    {
                        return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Media, Items.Count));
                    }
                }

                return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Files, Items.Count));
            }
        }

        public bool IsAlbum { get; private set; } = true;

        private bool _showCaptionAboveMedia;
        public bool ShowCaptionAboveMedia
        {
            get => _showCaptionAboveMedia;
            set
            {
                if (_showCaptionAboveMedia != value)
                {
                    _showCaptionAboveMedia = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCaptionAboveMedia)));
                }
            }
        }

        private bool _sendWithSpoiler;
        public bool SendWithSpoiler
        {
            get => _sendWithSpoiler;
            set
            {
                if (_sendWithSpoiler != value)
                {
                    _sendWithSpoiler = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendWithSpoiler)));
                }
            }
        }

        private long _starCount;
        public long StarCount
        {
            get => _starCount;
            set
            {
                if (_starCount != value)
                {
                    _starCount = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StarCount)));
                }
            }
        }

        public FormattedText Caption
        {
            get => CaptionInput.GetFormattedText(false);
            set => CaptionInput.SetText(value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool CanSchedule { get; set; }
        public bool IsSavedMessages { get; set; }

        public bool? Schedule { get; private set; }
        public bool? Silent { get; private set; }

        public SendFilesPopup(ComposeViewModel viewModel, IEnumerable<StorageMedia> items, bool media, ChatPermissions permissions, bool ttlAllowed, bool schedule, bool savedMessages, bool editing)
        {
            InitializeComponent();

            var builder = new StringBuilder();

            foreach (var item in items)
            {
                switch (item)
                {
                    case StoragePhoto photo:
                        builder.Prepend(string.Format("photo {0}x{1}", photo.Width, photo.Height), ", ");
                        break;
                    case StorageVideo video:
                        builder.Prepend(string.Format("video {0}x{1}", video.Width, video.Height), ", ");
                        break;
                    default:
                        builder.Prepend("file", ", ");
                        break;
                }
            }

            Logger.Info(builder);

            IsSavedMessages = savedMessages;
            CanSchedule = schedule;

            _editing = editing;
            _ttlAllowed = ttlAllowed;
            _photoAllowed = permissions.CanSendPhotos;
            _videoAllowed = permissions.CanSendVideos;
            _audioAllowed = permissions.CanSendAudios;
            _documentAllowed = permissions.CanSendDocuments;

            DataContext = viewModel;
            ViewModel = viewModel;

            ItemsView = new DiffObservableCollection<StorageMedia>(this, Constants.DiffOptions);

            Items = new MvxObservableCollection<StorageMedia>(items);
            Items.CollectionChanged += OnCollectionChanged;
            IsMediaSelected = media && IsMediaAllowed;
            IsFilesSelected = !IsMediaSelected;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(viewModel.SessionId);
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.ViewModel = viewModel;

            UpdateView();
            UpdatePanel();
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleText)));

            if (Items.Count > 0)
            {
                UpdateView();
                UpdatePanel();
            }
            else
            {
                Hide();
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SetResult(ContentDialogResult.Primary);
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SetResult(ContentDialogResult.Secondary);
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            CaptionInput.Document.GetText(TextGetOptions.None, out string text);

            var query = text.Substring(0, Math.Min(CaptionInput.Document.Selection.EndPosition, text.Length));
            var entity = AutocompleteEntityFinder.Search(query, out string result, out int index);

            if (e.ClickedItem is User user && entity == AutocompleteEntity.Username)
            {
                // TODO: find username
                var adjust = 0;
                var username = user.ActiveUsername(result);

                string insert;
                if (string.IsNullOrEmpty(username))
                {
                    insert = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                    adjust = 1;
                }
                else
                {
                    insert = username;
                }

                var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - result.Length - adjust, CaptionInput.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                if (string.IsNullOrEmpty(username))
                {
                    range.Link = $"\"tg-user://{user.Id}\"";
                }

                CaptionInput.Document.GetRange(range.EndPosition, range.EndPosition).SetText(TextSetOptions.None, " ");
                CaptionInput.Document.Selection.StartPosition = range.EndPosition + 1;
            }
            else if (e.ClickedItem is EmojiData or Sticker && entity == AutocompleteEntity.Emoji)
            {
                if (e.ClickedItem is EmojiData emoji)
                {
                    var insert = $"{emoji.Value} ";
                    var start = CaptionInput.Document.Selection.StartPosition - 1 - result.Length + insert.Length;
                    var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - 1 - result.Length, CaptionInput.Document.Selection.StartPosition);
                    range.SetText(TextSetOptions.None, insert);

                    CaptionInput.Document.Selection.StartPosition = start;
                }
                else if (e.ClickedItem is Sticker sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                {
                    var start = CaptionInput.Document.Selection.StartPosition - 1 - result.Length + 1;
                    var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - 1 - result.Length, CaptionInput.Document.Selection.StartPosition);

                    CaptionInput.InsertEmoji(range, sticker.Emoji, customEmoji.CustomEmojiId);
                    CaptionInput.Document.Selection.StartPosition = range.EndPosition + 1;
                }
            }

            Autocomplete = null;
        }

        private void Autocomplete_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is User user)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.FullName();

                if (user.HasActiveUsername(out string usernameValue))
                {
                    username.Text = $" @{usernameValue}";
                }
                else
                {
                    username.Text = string.Empty;
                }

                photo.SetUser(ViewModel.ClientService, user, 32);
            }
            else if (args.Item is Sticker sticker)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var animated = content.Children[0] as AnimatedImage;
                animated.Source = new DelayedFileSource(ViewModel.ClientService, sticker);

                AutomationProperties.SetName(args.ItemContainer, sticker.Emoji);
            }
            else if (args.Item is EmojiData emoji)
            {
                AutomationProperties.SetName(args.ItemContainer, emoji.Value);
            }

            args.Handled = true;
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var above = ShowCaptionAboveMedia;
            var visible = e.NewSize.Height > 0;

            CaptionInput.CornerRadius = new CornerRadius(above ? 2 : visible ? 0 : 2, above ? 2 : visible ? 0 : 2, above ? visible ? 0 : 2 : 2, above ? visible ? 0 : 2 : 2);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            args.Handled = true;

            var storage = args.Item as StorageMedia;
            if (storage == null)
            {
                return;
            }

            var root = args.ItemContainer.ContentTemplateRoot as Grid;
            if (root == null)
            {
                return;
            }

            if (root.Children[0] is StorageAlbumPanel albumPanel && storage is StorageAlbum album)
            {
                UpdatePaidMedia(root);

                albumPanel.UpdateMessage(album);
                return;
            }

            if (root is AspectView aspect)
            {
                aspect.Constraint = new Size(storage.Width, storage.Height);
            }

            var glyph = root.FindName("Glyph");
            if (glyph is AnimatedGlyphButton animated)
            {
                animated.Tag = storage;
                animated.Glyph = storage is StoragePhoto
                    ? Icons.ImageFilled24
                    : storage is StorageVideo or StorageAudio
                    ? Icons.PlayFilled24
                    : Icons.DocumentFilled24;
            }
            else if (glyph is TextBlock text)
            {
                text.Text = storage is StoragePhoto
                    ? Icons.ImageFilled24
                    : storage is StorageVideo or StorageAudio
                    ? Icons.PlayFilled24
                    : Icons.DocumentFilled24;
            }

            var title = root.FindName("Title") as TextBlock;
            var titleTrim = root.FindName("TitleTrim") as TextBlock;
            var subtitle = root.FindName("Subtitle") as TextBlock;

            if (title == null || titleTrim == null || subtitle == null)
            {
                return;
            }

            if (storage is StorageAudio audio)
            {
                if (string.IsNullOrEmpty(audio.Performer) || string.IsNullOrEmpty(audio.Title))
                {
                    var index = storage.File.Name.LastIndexOf('.');
                    if (index > 0)
                    {
                        title.Text = storage.File.Name.Substring(0, index + 1);
                        titleTrim.Text = storage.File.Name.Substring(index + 1);
                    }
                    else
                    {
                        title.Text = storage.File.Name;
                        titleTrim.Text = string.Empty;
                    }
                }
                else
                {
                    title.Text = $"{audio.Performer} - {audio.Title}";
                    titleTrim.Text = string.Empty;
                }

                subtitle.Text = audio.Duration;
                subtitle.Visibility = Visibility.Visible;
            }
            else
            {
                var index = storage.File.Name.LastIndexOf('.');
                if (index > 0)
                {
                    title.Text = storage.File.Name.Substring(0, index + 1);
                    titleTrim.Text = storage.File.Name.Substring(index + 1);
                }
                else
                {
                    title.Text = storage.File.Name;
                    titleTrim.Text = string.Empty;
                }

                if (storage.Size > 0)
                {
                    subtitle.Text = FileSizeConverter.Convert((long)storage.Size);
                    subtitle.Visibility = Visibility.Visible;
                }
                else
                {
                    subtitle.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void FileItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;
            var storage = ScrollingHost.ItemFromContainer(content) as StorageMedia;

            var glyph = content.FindName("Glyph") as AnimatedGlyphButton;
            glyph.Glyph = Icons.DeleteFilled24;
        }

        private void FileItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;
            var storage = content.DataContext as StorageMedia;

            var glyph = content.FindName("Glyph") as AnimatedGlyphButton;
            if (glyph != null)
            {
                glyph.Glyph = storage is StoragePhoto
                    ? Icons.ImageFilled24
                    : storage is StorageVideo or StorageAudio
                    ? Icons.PlayFilled24
                    : Icons.DocumentFilled24;
            }
        }

        private void MediaItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;
            var rootGrid = content.FindName("RootGrid") as Grid;

            rootGrid.Opacity = 1;
        }

        private void MediaItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;
            var rootGrid = content.FindName("RootGrid") as Grid;

            rootGrid.Opacity = 0;
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.Parent is Grid root && root.DataContext is StorageMedia storage)
            {
                UpdateTemplate(root, storage);
            }
        }

        private void Grid_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender.Parent is Grid root && args.NewValue is StorageMedia storage)
            {
                UpdateTemplate(root, storage);
            }
        }

        private void UpdateTemplate(Grid root, StorageMedia storage)
        {
            var overlay = root.FindName("Overlay") as Border;
            overlay.Visibility = storage is StorageVideo ? Visibility.Visible : Visibility.Collapsed;

            var mute = root.FindName("Mute") as ToggleButton;
            var crop = root.FindName("Crop") as ToggleButton;
            var ttl = root.FindName("Ttl") as ToggleButton;

            if (mute == null)
            {
                return;
            }

            if (storage is StorageVideo video)
            {
                mute.IsChecked = video.IsMuted;
                mute.Visibility = Visibility.Visible;
            }
            else
            {
                mute.Visibility = Visibility.Collapsed;
            }

            crop.Visibility = storage is StoragePhoto ? Visibility.Visible : Visibility.Collapsed;
            ttl.Visibility = IsTtlAvailable ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Accept()
        {
            if (CaptionInput.HandwritingView.IsOpen)
            {
                void handler(object s, RoutedEventArgs args)
                {
                    CaptionInput.HandwritingView.Unloaded -= handler;

                    Caption = CaptionInput.GetFormattedText();
                    Hide(ContentDialogResult.Primary);
                }

                CaptionInput.HandwritingView.Unloaded += handler;
                CaptionInput.HandwritingView.TryClose();
            }
            else
            {
                Caption = CaptionInput.GetFormattedText();
                Hide(ContentDialogResult.Primary);
            }
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            var content = Clipboard.GetContent();
            if (content.AvailableFormats.Contains(StandardDataFormats.Text))
            {
                e.Handled = true;
            }
            else
            {
                e.Handled = true;
                await HandlePackageAsync(content);
            }
        }

        private void ListView_DragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void ListView_Drop(object sender, DragEventArgs e)
        {
            await HandlePackageAsync(e.DataView);
        }

        public async Task HandlePackageAsync(DataPackageView package)
        {
            try
            {
                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmap = await package.GetBitmapAsync();

                    var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
                    var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                    using (var source = await bitmap.OpenReadAsync())
                    using (var destination = await cache.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await RandomAccessStream.CopyAsync(
                            source.GetInputStreamAt(0),
                            destination.GetOutputStreamAt(0));
                    }

                    var photo = await StorageMedia.CreateAsync(cache);
                    if (photo != null)
                    {
                        photo.IsScreenshot = true;

                        if (_editing)
                        {
                            Items.ReplaceWith(new[] { photo });
                        }
                        else
                        {
                            Items.Add(photo);
                        }

                        UpdateView();
                        UpdatePanel();
                    }
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await package.GetStorageItemsAsync();
                    var results = await StorageMedia.CreateAsync(items);

                    if (_editing)
                    {
                        Items.ReplaceWith(results.Take(1));
                    }
                    else
                    {
                        Items.AddRange(results);
                    }

                    UpdateView();
                    UpdatePanel();
                }
            }
            catch { }
        }

        private void UpdateView()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMediaAllowed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleText)));

            if (IsMediaSelected && !IsMediaAllowed && _documentAllowed)
            {
                IsMediaSelected = false;
                IsFilesSelected = true;
            }

            MoreButton.Visibility = Items.Any(x => x is StoragePhoto or StorageVideo or StorageAudio)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateCollection()
        {
            var view = new List<StorageMedia>();

            if (IsMediaSelected)
            {
                var album = new List<StorageMedia>();

                void AddAlbum()
                {
                    if (album.Count > 0)
                    {
                        view.Add(new StorageAlbum(album));
                        album = new List<StorageMedia>();
                    }
                }

                foreach (var item in Items)
                {
                    if ((item is StoragePhoto && _photoAllowed) || (item is StorageVideo && _videoAllowed))
                    {
                        if (album.Count > 9)
                        {
                            AddAlbum();
                        }

                        album.Add(item);
                    }
                    else
                    {
                        AddAlbum();

                        if (item is StorageDocument || (item is StoragePhoto && _photoAllowed) || (item is StorageVideo && _videoAllowed) || (item is StorageAudio && _audioAllowed))
                        {
                            view.Add(item);
                        }
                        else
                        {
                            view.Add(new StorageDocument(item));
                        }
                    }
                }

                AddAlbum();
            }
            else
            {
                foreach (var item in Items)
                {
                    if (item is StorageDocument)
                    {
                        view.Add(item);
                    }
                    else
                    {
                        view.Add(new StorageDocument(item));
                    }
                }
            }

            ItemsView.CollectionChanged -= ItemsView_CollectionChanged;
            ItemsView.ReplaceDiff(view);
            ItemsView.CollectionChanged += ItemsView_CollectionChanged;
        }

        private async void UpdatePanel()
        {
            UpdateCollection();

            if (ScrollingHost.ItemsPanelRoot is ItemsStackPanel panel && IsAlbum)
            {
                void UpdateSelectorItem(Grid content)
                {
                    if (content.Children[0] is StorageAlbumPanel album)
                    {
                        UpdatePaidMedia(content);

                        foreach (var child in album.Children)
                        {
                            if (child is ContentControl { ContentTemplateRoot: Grid inner })
                            {
                                UpdateSelectorItem(inner);
                            }
                        }

                        return;
                    }

                    UpdateTemplate(content, content.DataContext as StorageMedia);

                    var particles = content.FindName("Particles") as AnimatedImage;
                    if (particles != null)
                    {
                        particles.Source = SendWithSpoiler || StarCount > 0
                            ? new ParticlesImageSource()
                            : null;
                    }

                    var border = content.FindName("BackDrop") as Border;
                    if (border != null)
                    {
                        if (SendWithSpoiler || StarCount > 0)
                        {
                            var graphicsEffect = new GaussianBlurEffect
                            {
                                Name = "Blur",
                                BlurAmount = 3,
                                BorderMode = EffectBorderMode.Hard,
                                Source = new CompositionEffectSourceParameter("Backdrop")
                            };

                            var compositor = BootStrapper.Current.Compositor;
                            var effectFactory = compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
                            var effectBrush = effectFactory.CreateBrush();
                            var backdrop = compositor.CreateBackdropBrush();
                            effectBrush.SetSourceParameter("Backdrop", backdrop);

                            var blurVisual = compositor.CreateSpriteVisual();
                            blurVisual.RelativeSizeAdjustment = Vector2.One;
                            blurVisual.Brush = effectBrush;

                            ElementCompositionPreview.SetElementChildVisual(border, blurVisual);
                        }
                        else
                        {
                            ElementCompositionPreview.SetElementChildVisual(border, null);
                        }
                    }
                }

                await ScrollingHost.UpdateLayoutAsync();

                ScrollingHost.ForEach<StorageMedia>((selector, item) =>
                {
                    if (item is StoragePhoto or StorageVideo or StorageAlbum && selector.ContentTemplateRoot is Grid content)
                    {
                        UpdateSelectorItem(content);
                    }
                });
            }
        }

        private void UpdatePaidMedia(Grid root)
        {
            var PaidMediaButton = root.FindName("PaidMediaButton") as FrameworkElement;

            if (StarCount > 0)
            {
                var text = Locale.Declension(Strings.R.UnlockPaidContent, StarCount);
                var index = text.IndexOf("\u2B50\uFE0F");

                var TextPart1 = root.FindName("TextPart1") as Run;
                var TextPart2 = root.FindName("TextPart2") as Run;

                TextPart1.Text = text.Substring(0, index);
                TextPart2.Text = text.Substring(index + 2);

                PaidMediaButton.Visibility = Visibility.Visible;
            }
            else
            {
                PaidMediaButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ItemsView_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var items = new List<StorageMedia>();

            foreach (var item in ItemsView)
            {
                if (item is StorageAlbum album)
                {
                    items.AddRange(album.Media);
                }
                else if (item is StorageDocument document)
                {
                    items.Add(document.Original ?? document);
                }
                else
                {
                    items.Add(item);
                }
            }

            Items.CollectionChanged -= OnCollectionChanged;
            Items.ReplaceWith(items);
            Items.CollectionChanged += OnCollectionChanged;

            UpdateCollection();
        }

        private void PivotRadioButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateView();
            UpdatePanel();
        }

        private void Album_Click(object sender, RoutedEventArgs e)
        {
            //_wasAlbum = AlbumButton.IsChecked == true;
            //IsAlbum = _wasAlbum;

            UpdateView();
            UpdatePanel();
        }

        private void SendFilesAlbumPanel_Loading(FrameworkElement sender, object args)
        {
            UpdatePanel();
        }

        private void Ttl_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            var media = button.Tag as StorageMedia;

            var flyout = new MenuFlyout();

            flyout.Items.Add(new MenuFlyoutLabel
            {
                Text = Strings.TimerPeriodHint,
                Padding = new Thickness(12, 4, 12, 8),
                MaxWidth = 178,
            });

            flyout.Items.Add(new MenuFlyoutSeparator());

            void Update(MessageSelfDestructType ttl)
            {
                media.Ttl = ttl;
                ToastPopup.Show(XamlRoot,
                    media is StorageVideo
                        ? ttl is MessageSelfDestructTypeTimer timer1
                        ? Locale.Declension(Strings.R.TimerPeriodVideoSetSeconds, timer1.SelfDestructTime)
                        : ttl is MessageSelfDestructTypeImmediately
                        ? Strings.TimerPeriodVideoSetOnce
                        : Strings.TimerPeriodVideoKeep
                        : ttl is MessageSelfDestructTypeTimer timer2
                        ? Locale.Declension(Strings.R.TimerPeriodPhotoSetSeconds, timer2.SelfDestructTime)
                        : ttl is MessageSelfDestructTypeImmediately
                        ? Strings.TimerPeriodPhotoSetOnce
                        : Strings.TimerPeriodPhotoKeep,
                    ttl == null
                        ? ToastPopupIcon.AutoRemoveOff
                        : ToastPopupIcon.AutoRemoveOn);

                UpdateView();
                UpdatePanel();
            }

            var command = new RelayCommand<MessageSelfDestructType>(Update);

            void CreateToggle(MessageSelfDestructType value, string text)
            {
                var toggle = new ToggleMenuFlyoutItem
                {
                    Text = text,
                    IsChecked = value.AreTheSame(media.Ttl),
                    Command = command,
                    CommandParameter = value
                };

                flyout.Items.Add(toggle);
            }

            CreateToggle(new MessageSelfDestructTypeImmediately(), Strings.TimerPeriodOnce);
            CreateToggle(new MessageSelfDestructTypeTimer(3), Locale.Declension(Strings.R.Seconds, 3));
            CreateToggle(new MessageSelfDestructTypeTimer(10), Locale.Declension(Strings.R.Seconds, 10));
            CreateToggle(new MessageSelfDestructTypeTimer(30), Locale.Declension(Strings.R.Seconds, 30));
            CreateToggle(new MessageSelfDestructTypeTimer(60), Locale.Declension(Strings.R.Seconds, 60));
            CreateToggle(null, Strings.TimerPeriodDoNotDelete);

            flyout.ShowAt(button.Parent, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            if (button.Tag is StorageVideo video)
            {
                button.IsChecked = !button.IsChecked == true;
                video.IsMuted = button.IsChecked == true;
            }
        }

        private async void Crop_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            if (button.Tag is StorageMedia media)
            {
                var parent = button.GetParent<AspectView>();
                if (parent != null)
                {
                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("EditMediaPopup", parent);
                }

                var popup = new EditMediaPopup(media);

                var confirm = await popup.ShowAsync(XamlRoot);
                if (confirm == ContentDialogResult.Primary)
                {
                    media.Refresh();

                    UpdateView();
                    UpdatePanel();
                }
            }
        }

        private async void Album_ItemClick(object sender, StorageMedia args)
        {
            var parent = sender as UIElement;
            if (parent != null)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("EditMediaPopup", parent);
            }

            var popup = new EditMediaPopup(args);

            var confirm = await popup.ShowAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                args.Refresh();

                UpdateView();
                UpdatePanel();
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is StorageMedia media)
            {
                Items.Remove(media);
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            Accept();
        }

        private void Send_ContextRequested(object sender, ContextRequestedEventArgs args)
        {
            if (CanSchedule)
            {
                var self = IsSavedMessages;

                var flyout = new MenuFlyout();

                flyout.CreateFlyoutItem(SendWithoutGrouping, Strings.SendWithoutGrouping, "\uE90C");
                flyout.CreateFlyoutItem(SendWithoutSound, Strings.SendWithoutSound, Icons.AlertOff);
                flyout.CreateFlyoutItem(SendScheduled, self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);

                flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.TopEdgeAlignedRight);
            }
        }

        private void SendWithoutGrouping()
        {
            IsAlbum = false;
            Hide(ContentDialogResult.Primary);
        }

        private void SendWithoutSound()
        {
            Silent = true;
            Hide(ContentDialogResult.Primary);
        }

        private void SendScheduled()
        {
            Schedule = true;
            Hide(ContentDialogResult.Primary);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CaptionInput.Focus(FocusState.Keyboard);
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0)
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsControl(character[0]))
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox and not Button and not MenuFlyoutItem))
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);

                foreach (var popup in popups)
                {
                    if (popup.Child is not SendFilesPopup and not Rectangle)
                    {
                        return;
                    }
                }

                if (character == "\u0016" && CaptionInput.CanPasteClipboardContent)
                {
                    CaptionInput.Focus(FocusState.Keyboard);
                    CaptionInput.PasteFromClipboard();
                }
                else if (character == "\r")
                {
                    Accept();
                }
                else
                {
                    CaptionInput.Focus(FocusState.Keyboard);
                    CaptionInput.InsertText(character);
                }

                args.Handled = true;
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionPanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private int ConvertCaptionRow(bool above)
        {
            ListAutocomplete.VerticalAlignment = above
                ? VerticalAlignment.Top
                : VerticalAlignment.Bottom;

            ListAutocomplete.Margin = new Thickness(0);
            ListAutocomplete.BorderThickness = new Thickness(1, above ? 0 : 1, 1, above ? 1 : 0);
            ListAutocomplete.CornerRadius = new CornerRadius(above ? 0 : 2, above ? 0 : 2, above ? 2 : 0, above ? 2 : 0);

            CaptionBorder.BorderThickness = new Thickness(0, 1, 0, above ? 1 : 0);

            return above ? 0 : 2;
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            if (StarCount > 0)
            {
                flyout.CreateFlyoutItem(MakeContentPaid, Strings.PaidMediaPriceButton, Icons.Coin);
            }
            else
            {
                if (_documentAllowed && IsMediaAllowed)
                {
                    var withCompressionText =
                        Items.All(x => x is StoragePhoto)
                        ? Items.Count != 1 ? Strings.SendAsPhotos : Strings.SendAsPhoto
                        : Items.All(x => x is StorageVideo) ? Items.Count != 1 ? Strings.SendAsVideo : Strings.SendAsVideos
                        : Strings.SendAsMedia;

                    flyout.CreateFlyoutItem(ToggleIsFilesSelected, false, withCompressionText, IsFilesSelected ? null : Icons.Checkmark, Windows.System.VirtualKey.P, Windows.System.VirtualKeyModifiers.Control);
                    flyout.CreateFlyoutItem(ToggleIsFilesSelected, true, Items.Count != 1 ? Strings.SendAsFiles : Strings.SendAsFile, IsFilesSelected ? Icons.Checkmark : null, Windows.System.VirtualKey.F, Windows.System.VirtualKeyModifiers.Control);
                }

                if (IsMediaSelected && Items.All(x => x is StoragePhoto or StorageVideo))
                {
                    flyout.CreateFlyoutSeparator();

                    flyout.CreateFlyoutItem(ToggleSendWithSpoiler, SendWithSpoiler ? Strings.DisablePhotoSpoiler : Strings.EnablePhotoSpoiler, Icons.SpoilerMedia);
                    flyout.CreateFlyoutItem(ToggleShowCaptionAboveMedia, ShowCaptionAboveMedia ? Strings.CaptionBelow : Strings.CaptionAbove, ShowCaptionAboveMedia ? Icons.MoveDown : Icons.MoveUp);

                    if (HasPaidMediaAllowed)
                    {
                        flyout.CreateFlyoutItem(MakeContentPaid, Strings.PaidMediaButton, Icons.Coin);
                    }
                }
            }

            flyout.ShowAt(sender as DependencyObject, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void ToggleIsFilesSelected(bool value)
        {
            IsFilesSelected = value;
            UpdateView();
            UpdatePanel();

            if (value)
            {
                ShowCaptionAboveMedia = false;
            }
        }

        private void ToggleShowCaptionAboveMedia()
        {
            ShowCaptionAboveMedia = !ShowCaptionAboveMedia;
        }

        private void ToggleSendWithSpoiler()
        {
            SendWithSpoiler = !SendWithSpoiler;
            UpdatePanel();
        }

        private async void MakeContentPaid()
        {
            var popup = new InputTeachingTip(InputPopupType.Stars);
            popup.Value = StarCount;
            popup.Maximum = ViewModel.ClientService.Options.PaidMediaMessageStarCountMax;

            popup.Title = Strings.PaidContentTitle;
            popup.Header = Strings.PaidContentPriceTitle;
            popup.ActionButtonContent = Strings.PaidContentUpdateButton;
            popup.ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            popup.CloseButtonContent = Strings.Cancel;
            popup.PreferredPlacement = TeachingTipPlacementMode.Center;
            popup.IsLightDismissEnabled = true;
            popup.ShouldConstrainToRootBounds = true;

            //popup.Validating += (s, args) =>
            //{
            //    if (args.Value < ClientService.Options.StarWithdrawalCountMin)
            //    {
            //        ToastPopup.Show(Locale.Declension(Strings.R.BotStarsWithdrawMinLimit, ClientService.Options.StarWithdrawalCountMin), ToastPopupIcon.Info);
            //        args.Cancel = true;
            //    }
            //};

            var confirm = await popup.ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            StarCount = (long)popup.Value;
            IsFilesSelected = false;
            IsAlbum = true;

            UpdateView();
            UpdatePanel();
        }

        public bool CompareItems(StorageMedia oldItem, StorageMedia newItem)
        {
            if (oldItem is StorageAlbum oldAlbum && newItem is StorageAlbum newAlbum)
            {
                if (oldAlbum.Media.Count != newAlbum.Media.Count)
                {
                    return false;
                }

                for (int i = 0; i < oldAlbum.Media.Count; i++)
                {
                    if (CompareItems(oldAlbum.Media[i], newAlbum.Media[i]))
                    {
                        continue;
                    }

                    return false;
                }

                return true;
            }
            if (oldItem is StoragePhoto oldPhoto && newItem is StoragePhoto newPhoto)
            {
                // Compare crop etc
                return oldPhoto.File.Path == newPhoto.File.Path;
            }
            else if (oldItem is StorageVideo oldVideo && newItem is StorageVideo newVideo)
            {
                // Compare crop etc
                return oldVideo.File.Path == newVideo.File.Path;
            }
            else
            {
                return oldItem.File?.Path == newItem.File?.Path
                    && oldItem.GetType() == newItem.GetType();
            }
        }

        public void UpdateItem(StorageMedia oldItem, StorageMedia newItem)
        {
            if (oldItem is StorageAlbum album)
            {
                var container = ScrollingHost.ContainerFromItem(album) as SelectorItem;
                var content = container?.ContentTemplateRoot as Grid;

                if (content != null && content.Children[0] is StorageAlbumPanel panel)
                {
                    album.Invalidate();
                    panel.Invalidate();
                }
            }
        }
    }

    public class StorageMediaTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FileTemplate { get; set; }

        public DataTemplate AlbumTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                StorageAlbum => AlbumTemplate,
                _ => FileTemplate
            };
        }
    }

    public sealed partial class StorageAlbumPanel : Grid
    {
        private StorageAlbum _album;

        public StorageAlbumPanel()
        {
            // I don't like this much, but it's the easier way to add margins between children
            Margin = new Thickness(0, 0, -MessageAlbum.ITEM_MARGIN, -MessageAlbum.ITEM_MARGIN);
        }

        private (Rect[], Size) _positions;

        public void Invalidate()
        {
            _positions = default;

            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var album = _album;
            if (album == null || album.Media.Count < 1)
            {
                return base.MeasureOverride(availableSize);
            }

            var positions = album.GetPositionsForWidth(availableSize.Width);

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Measure(positions.Item1[i].ToSize());
            }

            _positions = positions;
            return positions.Item2;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var album = _album;
            if (album == null || album.Media.Count < 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            var positions = _positions;
            if (positions.Item1 == null || positions.Item1.Length < 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Arrange(positions.Item1[i]);
            }

            return finalSize;
        }

        public event EventHandler<StorageMedia> ItemClick;

        public void UpdateMessage(StorageAlbum album)
        {
            _album = album;
            Children.Clear();

            foreach (var pos in album.Media)
            {
                var element = new Button
                {
                    ContentTemplate = ItemTemplate,
                    Content = pos,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    MinWidth = 0,
                    MinHeight = 0,
                    MaxWidth = MessageAlbum.MAX_WIDTH,
                    MaxHeight = MessageAlbum.MAX_HEIGHT,
                    Margin = new Thickness(0, 0, MessageAlbum.ITEM_MARGIN, MessageAlbum.ITEM_MARGIN),
                    Padding = new Thickness(0),
                    Style = BootStrapper.Current.Resources["EmptyButtonStyle"] as Style
                };

                element.Click += Element_Click;

                Children.Add(element);
            }
        }

        private void Element_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button element && element.Content is StorageMedia item)
            {
                ItemClick?.Invoke(sender, item);
            }
        }

        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(StorageAlbumPanel), new PropertyMetadata(null));
    }
}
