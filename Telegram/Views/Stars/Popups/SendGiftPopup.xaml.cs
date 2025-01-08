﻿using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Stars;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class SendGiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly Gift _gift;

        private readonly PremiumGiftCodePaymentOption _option;

        private readonly long _userId;

        public SendGiftPopup(IClientService clientService, INavigationService navigationService, Gift gift, long userId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _gift = gift;
            _userId = userId;

            base.Title = Strings.Gift2Title;

            clientService.TryGetChatFromUser(clientService.Options.MyId, out Chat chat);

            var content = new MessageGift(gift, new FormattedText(string.Empty, Array.Empty<TextEntity>()), gift.DefaultSellStarCount, 0, false, false, false, false, false, false, 0);
            var message = new Message(0, new MessageSenderUser(clientService.Options.MyId), 0, null, null, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, 0, 0, null, 0, 0, 0, 0, 0, string.Empty, 0, 0, false, string.Empty, content, null);

            var playback = TypeResolver.Current.Playback;
            var settings = TypeResolver.Current.Resolve<ISettingsService>(clientService.SessionId);

            var delegato = new ChatMessageDelegate(clientService, settings, chat);
            var viewModel = new MessageViewModel(clientService, playback, delegato, chat, message, true);

            BackgroundControl.Update(clientService, null);
            Message.UpdateMessage(viewModel);

            var emoji = EmojiDrawerViewModel.Create(clientService.SessionId);
            EmojiPanel.DataContext = emoji;
            CaptionInput.DataContext = emoji;
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.MaxLength = (int)clientService.Options.GiftTextLengthMax;
            CaptionInput.PlaceholderText = Strings.Gift2Message;
            CaptionInput.AllowedEntities = FormattedTextEntity.Bold
                | FormattedTextEntity.Italic
                | FormattedTextEntity.Underline
                | FormattedTextEntity.Strikethrough
                | FormattedTextEntity.Spoiler
                | FormattedTextEntity.CustomEmoji;

            CaptionInfo.Visibility = Visibility.Collapsed;

            if (clientService.Options.MyId == userId)
            {
                HideMyName.Content = Strings.Gift2HideSelf;
                HideMyNameInfo.Text = Strings.Gift2HideSelfInfo;

                UpgradeableRoot.Visibility = Visibility.Collapsed;
            }
            else if (clientService.TryGetUser(userId, out User user))
            {
                HideMyNameInfo.Text = string.Format(Strings.Gift2HideInfo, user.FirstName);

                if (gift.UpgradeStarCount > 0)
                {
                    UpgradeableInfo.Text = string.Format(Strings.Gift2UpgradeInfo, user.FirstName);

                    var text = string.Format(Strings.Gift2Upgrade, gift.UpgradeStarCount);
                    var index = text.IndexOf("\u2B50\uFE0F");

                    if (index != -1)
                    {
                        UpgradeableTextPart1.Text = text.Substring(0, index);
                        UpgradeableTextPart2.Text = text.Substring(index + 2);
                    }
                }
                else
                {
                    UpgradeableRoot.Visibility = Visibility.Collapsed;
                }
            }

            PurchaseText.Text = Locale.Declension(Strings.R.Gift2Send, gift.StarCount).Replace("\u2B50", Icons.Premium);
        }

        public SendGiftPopup(IClientService clientService, INavigationService navigationService, PremiumGiftCodePaymentOption option, long userId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _option = option;
            _userId = userId;

            base.Title = Strings.Gift2Title;

            clientService.TryGetChatFromUser(clientService.Options.MyId, out Chat chat);

            var content = new MessageGiftedPremium(_clientService.Options.MyId, _userId, new FormattedText(string.Empty, Array.Empty<TextEntity>()), _option.Currency, _option.Amount, string.Empty, 0, _option.MonthCount, _option.Sticker);
            var message = new Message(0, new MessageSenderUser(clientService.Options.MyId), 0, null, null, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, 0, 0, null, 0, 0, 0, 0, 0, string.Empty, 0, 0, false, string.Empty, content, null);

            var playback = TypeResolver.Current.Playback;
            var settings = TypeResolver.Current.Resolve<ISettingsService>(clientService.SessionId);

            var delegato = new ChatMessageDelegate(clientService, settings, chat);
            var viewModel = new MessageViewModel(clientService, playback, delegato, chat, message, true);

            BackgroundControl.Update(clientService, null);
            Message.UpdateMessage(viewModel);

            var emoji = EmojiDrawerViewModel.Create(clientService.SessionId);
            EmojiPanel.DataContext = emoji;
            CaptionInput.DataContext = emoji;
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.MaxLength = (int)clientService.Options.GiftTextLengthMax;
            CaptionInput.PlaceholderText = Strings.Gift2MessageOptional;
            CaptionInput.AllowedEntities = FormattedTextEntity.Bold
                | FormattedTextEntity.Italic
                | FormattedTextEntity.Underline
                | FormattedTextEntity.Strikethrough
                | FormattedTextEntity.Spoiler
                | FormattedTextEntity.CustomEmoji;

            HideMyNameRoot.Visibility = Visibility.Collapsed;
            UpgradeableRoot.Visibility = Visibility.Collapsed;

            if (clientService.TryGetUser(userId, out User user))
            {
                CaptionInfo.Text = string.Format(Strings.Gift2MessagePremiumInfo, user.FirstName);
            }

            PurchaseText.Text = string.Format(Strings.Gift2SendPremium, Formatter.FormatAmount(option.Amount, option.Currency));
        }

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            var text = CaptionInput.GetFormattedText();

            MessageContent content;
            if (_gift != null)
            {
                content = new MessageGift(_gift, text, _gift.DefaultSellStarCount, Upgradeable.IsChecked is true ? _gift.UpgradeStarCount : 0, false, false, false, false, false, false, 0);

                PurchaseText.Text = Locale.Declension(Strings.R.Gift2Send, _gift.StarCount + (Upgradeable.IsChecked is true ? _gift.UpgradeStarCount : 0)).Replace("\u2B50", Icons.Premium);
            }
            else if (_option != null)
            {
                content = new MessageGiftedPremium(_clientService.Options.MyId, _userId, text, _option.Currency, _option.Amount, string.Empty, 0, _option.MonthCount, _option.Sticker);

                PurchaseText.Text = string.Format(Strings.Gift2SendPremium, Formatter.FormatAmount(_option.Amount, _option.Currency));
            }
            else
            {
                return;
            }

            _clientService.TryGetChatFromUser(_clientService.Options.MyId, out Chat chat);
            var message = new Message(0, new MessageSenderUser(_clientService.Options.MyId), 0, null, null, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, 0, 0, null, 0, 0, 0, 0, 0, string.Empty, 0, 0, false, string.Empty, content, null);

            var playback = TypeResolver.Current.Playback;
            var settings = TypeResolver.Current.Resolve<ISettingsService>(_clientService.SessionId);

            var delegato = new ChatMessageDelegate(_clientService, settings, chat);
            var viewModel = new MessageViewModel(_clientService, playback, delegato, chat, message, true);

            Message.UpdateMessage(viewModel);
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

        private bool _submitted;

        private async void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (_submitted)
            {
                return;
            }

            _submitted = true;

            PurchaseRing.Visibility = Windows.UI.Xaml.Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(PurchaseText);
            var visual2 = ElementComposition.GetElementVisual(PurchaseRing);

            ElementCompositionPreview.SetIsTranslationEnabled(PurchaseText, true);
            ElementCompositionPreview.SetIsTranslationEnabled(PurchaseRing, true);

            var translate1 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate1.InsertKeyFrame(0, 0);
            translate1.InsertKeyFrame(1, -32);

            var translate2 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate2.InsertKeyFrame(0, 32);
            translate2.InsertKeyFrame(1, 0);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);

            //await Task.Delay(2000);

            var result = await SubmitAsync();
            if (result != PayResult.Failed)
            {
                Hide(result == PayResult.Succeeded
                    ? ContentDialogResult.Primary
                    : ContentDialogResult.Secondary);

                if (result == PayResult.StarsNeeded)
                {
                    await _navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(_gift.StarCount, 0));
                }

                return;
            }

            _submitted = false;

            translate1.InsertKeyFrame(0, 32);
            translate1.InsertKeyFrame(1, 0);

            translate2.InsertKeyFrame(0, 0);
            translate2.InsertKeyFrame(1, -32);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);

            //Hide();
            //ViewModel.Submit();
        }

        private Task<PayResult> SubmitAsync()
        {
            if (_gift != null)
            {
                return SubmitGiftAsync();
            }
            else if (_option != null)
            {
                return SubmitGiftCodeAsync();
            }

            return Task.FromResult(PayResult.Failed);
        }

        public Task<PayResult> SubmitGiftCodeAsync()
        {
            var text = CaptionInput.GetFormattedText();

            _navigationService.NavigateToInvoice(new InputInvoiceTelegram(new TelegramPaymentPurposePremiumGiftCodes(0, _option.Currency, _option.Amount, new[] { _userId }, _option.MonthCount, text)));
            return Task.FromResult(PayResult.Succeeded);
        }

        public async Task<PayResult> SubmitGiftAsync()
        {
            if (_clientService.OwnedStarCount.StarCount < _gift.StarCount)
            {
                var updated = await _clientService.GetStarTransactionsAsync(_clientService.MyId, string.Empty, null, string.Empty, 1) as StarTransactions;
                if (updated is null || updated.StarAmount.StarCount < _gift.StarCount)
                {
                    return PayResult.StarsNeeded;
                }
            }

            var text = CaptionInput.GetFormattedText();

            var response = await _clientService.SendAsync(new SendGift(_gift.Id, _userId, text, HideMyName.IsChecked is true, Upgradeable.IsChecked is true));
            if (response is Ok result)
            {
                //var user = ClientService.GetUser(PaymentForm.SellerBotUserId);
                //var extended = Locale.Declension(Strings.R.StarsPurchaseCompletedInfo, stars.StarCount, PaymentForm.ProductInfo.Title, user.FullName());

                //var message = Strings.StarsPurchaseCompleted + Environment.NewLine + extended;
                //var entity = new TextEntity(0, Strings.StarsPurchaseCompleted.Length, new TextEntityTypeBold());

                //var text = new FormattedText(message, new[] { entity });
                //var formatted = ClientEx.ParseMarkdown(text);

                //Aggregator.Publish(new UpdateConfetti());
                ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.StarsGiftCompleted, Locale.Declension(Strings.R.StarsGiftCompletedText, _gift.StarCount)), new DelayedFileSource(_clientService, _gift.Sticker));

                return PayResult.Succeeded;
            }
            else if (response is Error error)
            {
                if (error.Message == "STARGIFT_USAGE_LIMITED")
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2SoldOutTitle, Locale.Declension(Strings.R.Gift2SoldOutHint, _gift.TotalCount)), new DelayedFileSource(_clientService, _gift.Sticker));
                }
                else
                {
                    ToastPopup.ShowError(XamlRoot, error);
                }
            }

            return PayResult.Failed;
        }

        private void UpgradeableInfo_Click(object sender, TextUrlClickEventArgs e)
        {

        }
    }
}
