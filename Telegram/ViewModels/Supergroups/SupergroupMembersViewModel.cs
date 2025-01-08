//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Telegram.Views.Supergroups.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupMembersViewModel : SupergroupMembersViewModelBase, IDelegable<ISupergroupDelegate>
    {
        public SupergroupMembersViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
        }

        public bool IsEmbedded { get; set; }

        private bool _hasHiddenMembers;
        public bool HasHiddenMembers
        {
            get => _hasHiddenMembers;
            set => SetHiddenMembers(value);
        }

        public void UpdateHiddenMembers(bool value)
        {
            Set(ref _hasHiddenMembers, value, nameof(HasHiddenMembers));
        }

        private void SetHiddenMembers(bool value)
        {
            if (Chat.Type is ChatTypeSupergroup supergroupType && ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo supergroup))
            {
                if (supergroup.CanHideMembers)
                {
                    Set(ref _hasHiddenMembers, value, nameof(HasHiddenMembers));
                    ClientService.Send(new ToggleSupergroupHasHiddenMembers(supergroupType.SupergroupId, value));
                }
                else
                {
                    Set(ref _hasHiddenMembers, false, nameof(HasHiddenMembers));
                }
            }
        }

        public async void Add()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
            {
                var header = chat.Type is ChatTypeSupergroup { IsChannel: true }
                    ? Strings.AddSubscriber
                    : Strings.AddMember;

                var selectionMode = chat.Type is ChatTypeBasicGroup
                    ? ListViewSelectionMode.Single
                    : ListViewSelectionMode.Multiple;

                var selected = await ChooseChatsPopup.PickUsersAsync(ClientService, NavigationService, header, selectionMode);
                if (selected == null || selected.Count == 0)
                {
                    return;
                }

                if (selected[0].Type is UserTypeBot && chat.Type is ChatTypeSupergroup { IsChannel: true })
                {
                    var admin = await ShowPopupAsync(Strings.AddBotAsAdmin, Strings.AddBotAdminAlert, Strings.AddAsAdmin, Strings.Cancel);
                    if (admin == ContentDialogResult.Primary)
                    {
                        _ = NavigationService.ShowPopupAsync(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, new MessageSenderUser(selected[0].Id)));
                    }

                    return;
                }

                string title = Locale.Declension(Strings.R.AddManyMembersAlertTitle, selected.Count);
                string message;

                if (selected.Count <= 5)
                {
                    var names = string.Join(", ", selected.Select(x => x.FullName()));
                    message = string.Format(Strings.AddMembersAlertNamesText, names, chat.Title);
                }
                else
                {
                    message = Locale.Declension(Strings.R.AddManyMembersAlertNamesText, selected.Count, chat.Title);
                }

                var confirm = await ShowPopupAsync(message, title, Strings.Add, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (chat.Type is ChatTypeBasicGroup)
                {
                    var response = await ClientService.SendAsync(new AddChatMember(chat.Id, selected[0].Id, 100));
                    if (response is FailedToAddMembers failed && failed.FailedToAddMembersValue.Count > 0)
                    {
                        var popup = new ChatInviteFallbackPopup(ClientService, chat.Id, failed.FailedToAddMembersValue);
                        await ShowPopupAsync(popup);
                    }
                    else if (response is Error error)
                    {
                        ShowPopup(error.Message, Strings.AppName);
                    }
                }
                else
                {
                    var response = await ClientService.SendAsync(new AddChatMembers(chat.Id, selected.Select(x => x.Id).ToArray()));
                    if (response is FailedToAddMembers failed && failed.FailedToAddMembersValue.Count > 0)
                    {
                        var popup = new ChatInviteFallbackPopup(ClientService, chat.Id, failed.FailedToAddMembersValue);
                        await ShowPopupAsync(popup);
                    }
                    else if (response is Error error)
                    {
                        ShowPopup(error.Message, Strings.AppName);
                    }
                }
            }
        }

        #region Context menu

        public void PromoteMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public void RestrictMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditRestrictedPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public async void RemoveMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var index = Members.IndexOf(member);

            Members.Remove(member);

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusBanned()));
            if (response is Error)
            {
                Members.Insert(index, member);
            }
        }

        #endregion
    }
}
