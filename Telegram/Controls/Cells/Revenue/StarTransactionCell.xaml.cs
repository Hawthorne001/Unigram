﻿using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Revenue
{
    public sealed partial class StarTransactionCell : Grid
    {
        public StarTransactionCell()
        {
            InitializeComponent();
        }

        private long _media1Token;
        private long _media2Token;

        public void UpdateInfo(IClientService clientService, StarTransaction transaction)
        {
            UpdateManager.Unsubscribe(this, ref _media1Token, true);
            UpdateManager.Unsubscribe(this, ref _media2Token, true);

            if (transaction.Type is StarTransactionTypePremiumBotDeposit)
            {
                MediaPreview.Visibility = Visibility.Collapsed;
                Photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                Title.Text = Strings.StarsTransactionBot;
                Subtitle.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeFragmentWithdrawal or StarTransactionTypeFragmentDeposit)
            {
                MediaPreview.Visibility = Visibility.Collapsed;
                Photo.Source = new PlaceholderImage(Icons.FragmentFilled, true, Colors.Black, Colors.Black);
                Subtitle.Visibility = Visibility.Collapsed;

                if (transaction.Type is StarTransactionTypeFragmentWithdrawal)
                {
                    Title.Text = Strings.StarsTransactionWithdrawFragment;
                }
                else
                {
                    Title.Text = Strings.StarsTransactionFragment;
                }
            }
            else if (transaction.Type is StarTransactionTypeAppStoreDeposit or StarTransactionTypeGooglePlayDeposit)
            {
                MediaPreview.Visibility = Visibility.Collapsed;
                Photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                Title.Text = Strings.StarsTransactionInApp;
                Subtitle.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeBotInvoicePurchase botInvoicePurchase)
            {
                var botUser = clientService.GetUser(botInvoicePurchase.UserId);

                Subtitle.Text = botUser.FullName();
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = botInvoicePurchase.ProductInfo.Title;
                Photo.SetUser(clientService, botUser, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeBotPaidMediaPurchase botPaidMediaPurchase)
            {
                var botUser = clientService.GetUser(botPaidMediaPurchase.UserId);

                Subtitle.Text = botUser.FullName();
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, botPaidMediaPurchase.Media, botUser, null);

            }
            else if (transaction.Type is StarTransactionTypeBotInvoiceSale botInvoiceSale)
            {
                var botUser = clientService.GetUser(botInvoiceSale.UserId);

                Subtitle.Text = botUser.FullName();
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = botInvoiceSale.ProductInfo.Title;
                Photo.SetUser(clientService, botUser, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeBotPaidMediaSale botPaidMediaSale)
            {
                var botUser = clientService.GetUser(botPaidMediaSale.UserId);

                Subtitle.Text = botUser.FullName();
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, botPaidMediaSale.Media, botUser, null);
            }
            else if (transaction.Type is StarTransactionTypeGiftSale giftSale)
            {
                var user = clientService.GetUser(giftSale.UserId);

                Subtitle.Visibility = Visibility.Visible;
                Photo.SetUser(clientService, user, 36);
                MediaPreview.Visibility = Visibility.Collapsed;

                Title.Text = user.FullName();
                Subtitle.Text = transaction.StarAmount.IsNegative()
                    ? Strings.Gift2TransactionRefundedConverted
                    : Strings.Gift2TransactionConverted;
            }
            else if (transaction.Type is StarTransactionTypeUserDeposit userDeposit)
            {
                var user = clientService.GetUser(userDeposit.UserId);

                Subtitle.Visibility = Visibility.Visible;
                MediaPreview.Visibility = Visibility.Collapsed;

                Title.Text = transaction.StarAmount.IsNegative()
                    ? Strings.StarsGiftSent
                    : Strings.StarsGiftReceived;

                if (user != null)
                {
                    Photo.SetUser(clientService, user, 36);
                    Subtitle.Text = user.FullName();
                }
                else
                {
                    Photo.Source = new PlaceholderImage(Icons.FragmentFilled, true, Colors.Black, Colors.Black);
                    Subtitle.Text = Strings.StarsTransactionUnknown;
                }
            }
            else if (transaction.Type is StarTransactionTypeGiftPurchase giftPurchase)
            {
                var user = clientService.GetUser(giftPurchase.UserId);

                Subtitle.Visibility = Visibility.Visible;
                Photo.SetUser(clientService, user, 36);
                MediaPreview.Visibility = Visibility.Collapsed;

                Title.Text = user.FullName();
                Subtitle.Text = transaction.StarAmount.IsNegative()
                    ? Strings.Gift2TransactionSent
                    : Strings.Gift2TransactionRefundedSent;
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidMediaPurchase channelPaidMediaPurchase)
            {
                var chat = clientService.GetChat(channelPaidMediaPurchase.ChatId);

                Subtitle.Text = chat.Title;
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, channelPaidMediaPurchase.Media, null, chat);
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidReactionSend channelPaidReactionSend)
            {
                var chat = clientService.GetChat(channelPaidReactionSend.ChatId);

                Subtitle.Text = chat.Title;
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarsReactionsSent;
                Photo.SetChat(clientService, chat, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeChannelSubscriptionPurchase channelSubscriptionPurchase)
            {
                var chat = clientService.GetChat(channelSubscriptionPurchase.ChatId);

                Subtitle.Text = chat.Title;
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarsTransactionSubscriptionMonthly;
                Photo.SetChat(clientService, chat, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidMediaSale channelPaidMediaSale)
            {
                var user = clientService.GetUser(channelPaidMediaSale.UserId);

                Subtitle.Text = user.FullName();
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, channelPaidMediaSale.Media, user, null);
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidReactionReceive channelPaidReactionReceive)
            {
                var user = clientService.GetUser(channelPaidReactionReceive.UserId);

                Subtitle.Text = user.FullName();
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarsReactionsSent;
                Photo.SetUser(clientService, user, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeChannelSubscriptionSale channelSubscriptionSale)
            {
                var user = clientService.GetUser(channelSubscriptionSale.UserId);

                Subtitle.Text = user.FullName();
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarsTransactionSubscriptionMonthly;
                Photo.SetUser(clientService, user, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeGiveawayDeposit giveawayDeposit)
            {
                var chat = clientService.GetChat(giveawayDeposit.ChatId);

                Subtitle.Text = chat.Title;
                Subtitle.Visibility = Visibility.Visible;

                Title.Text = Strings.StarsGiveawayPrizeReceived;
                Photo.SetChat(clientService, chat, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeTelegramApiUsage telegramApiUsage)
            {
                Title.Text = Strings.StarsTransactionFloodskip;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.ChatStarsFilled, 3);

                MediaPreview.Visibility = Visibility.Collapsed;

                Subtitle.Text = Locale.Declension(Strings.R.StarsTransactionFloodskipMessages, telegramApiUsage.RequestCount);
                Subtitle.Visibility = Visibility.Visible;
            }
            else
            {
                MediaPreview.Visibility = Visibility.Collapsed;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                Title.Text = Strings.StarsTransactionUnsupported;
                Subtitle.Visibility = Visibility.Collapsed;
            }

            Date.Text = Formatter.DateAt(transaction.Date);

            if (transaction.Type is StarTransactionTypeFragmentWithdrawal)
            {
                if (transaction.IsRefund)
                {
                    Date.Text += string.Format(" — {0}", Strings.StarsRefunded);
                }
                else if (transaction.Type is StarTransactionTypeFragmentWithdrawal { WithdrawalState: RevenueWithdrawalStateFailed })
                {
                    Date.Text += string.Format(" — {0}", Strings.StarsFailed);
                }
                else if (transaction.Type is StarTransactionTypeFragmentWithdrawal { WithdrawalState: RevenueWithdrawalStatePending })
                {
                    Date.Text += string.Format(" — {0}", Strings.StarsPending);
                }
            }

            StarCount.Text = transaction.StarAmount.ToValue(true);
            StarCount.Foreground = BootStrapper.Current.Resources[transaction.StarAmount.IsNegative() ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;
        }

        private void UpdatePaidMedia(IClientService clientService, IList<PaidMedia> paidMedia, User fallbackUser, Chat fallbackChat)
        {
            if (paidMedia.Count > 0)
            {
                MediaPreview.Visibility = Visibility.Visible;

                UpdateMedia(clientService, paidMedia[0], Media1, ref _media1Token);

                if (paidMedia.Count > 1)
                {
                    UpdateMedia(clientService, paidMedia[1], Media2, ref _media2Token);

                    Media2.Visibility = Visibility.Visible;
                }
                else
                {
                    Media2.Visibility = Visibility.Collapsed;
                }
            }
            else if (fallbackUser != null)
            {
                Photo.SetUser(clientService, fallbackUser, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (fallbackChat != null)
            {
                Photo.SetChat(clientService, fallbackChat, 36);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateMedia(IClientService clientService, PaidMedia media, Border target, ref long token)
        {
            File file = null;
            if (media is PaidMediaPhoto photo)
            {
                file = photo.Photo.GetSmall()?.Photo;
            }
            else if (media is PaidMediaVideo video)
            {
                file = video.Video.Thumbnail?.File;
            }

            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                UpdateMedia(target, file);
            }
            else if (file.Local.CanBeDownloaded)
            {
                UpdateManager.Subscribe(this, clientService, file, ref token, target == Media1 ? UpdateMedia1 : UpdateMedia2, true);
                clientService.DownloadFile(file.Id, 16);

                target.Background = null;
            }
        }

        private void UpdateMedia1(object target, File file)
        {
            UpdateMedia(Media1, file);
        }

        private void UpdateMedia2(object target, File file)
        {
            UpdateMedia(Media2, file);
        }

        private void UpdateMedia(Border target, File file)
        {
            target.Background = new ImageBrush
            {
                ImageSource = UriEx.ToBitmap(file.Local.Path),
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
        }
    }
}
